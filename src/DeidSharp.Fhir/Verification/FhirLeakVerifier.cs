using Hl7.Fhir.Model;
using DeidSharp.Dates;
using DeidSharp.Detection;
using DeidSharp.Detection.Known;
using DeidSharp.Fhir.References;
using DeidSharp.Fhir.Rules;
using DeidSharp.Verification;

namespace DeidSharp.Fhir.Verification;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each structural check is one row (its name, the category it reports, the test
// an element fails). A new check is a new row; the walk that applies them never changes.

/// <summary>
/// Checks a de-identified resource independently of the rules that produced it:
/// structurally (no names, contact points, signatures or narrative left; addresses cut
/// down; in Safe Harbor mode no identifiers, no instants, no date finer than a year;
/// only allowed extensions, at any depth, including inside codings and on primitives;
/// only defining URIs; no server part other than the placeholder in references and
/// full URLs; only readable inline text attachments) and by content (none of the
/// input's known identifiers appears in any string, markdown or text attachment).
///
/// Content is checked for known identifiers only. Pattern detection would flag the
/// surrogates the run itself wrote ("(555) 555-0123" looks like a phone number), and
/// URIs and pseudonymized ids are skipped: a UUID can contain any four hex digits. Values
/// the rules keep on purpose are not scanned either: vocabulary in codings and
/// quantities, and an address's state and country (a patient named Georgia may live in
/// Georgia).
/// </summary>
public static class FhirLeakVerifier
{
    private static readonly LeakScanner KnownScanner = new([new KnownIdentifierDetector()]);

    private static readonly StructureCheck[] Checks =
    [
        new("name", PhiCategory.Name, e => e.Node is HumanName),
        new("contact-point", PhiCategory.Phone, e => e.Node is ContactPoint),
        new("signature-or-narrative", PhiCategory.OtherIdentifier, e => e.Node is Signature or Narrative),
        new("address", PhiCategory.StreetAddress, e => e.Node is Address address && KeepsMoreThanStateAndZip(address)),
        new("identifier", PhiCategory.OtherIdentifier, e => e is { SafeHarbor: true, Node: Identifier }),
        new("instant", PhiCategory.Date, e => e is { SafeHarbor: true, Node: Instant }),
        new("date-precision", PhiCategory.Date, e => e.SafeHarbor && IsFinerThanYear(e.Node)),
        new("extension", PhiCategory.OtherIdentifier, e => e is { Node: Extension extension, Parent: not Extension } && !IsAllowed(extension, e.Options)),
        new("attachment", PhiCategory.OtherIdentifier, e => e.Node is Attachment attachment && !IsInspectable(attachment, out _)),
        new("server", PhiCategory.Url, e => ServerAddress(e) is { } value && NamesSourceServer(value)),
        new("uri", PhiCategory.Url, IsDescribingUri),
    ];

    public static LeakReport Verify(
        Resource resource, KnownIdentifiers known, FhirDeidentificationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(options);
        var walk = new Walk(options);
        Visit(resource, null, string.Empty, resource.TypeName, scanText: true, walk);
        if (known.Count > 0)
        {
            walk.Findings.AddRange(KnownScanner.Scan(walk.Texts, known, cancellationToken: cancellationToken).Findings);
        }

        return new LeakReport(walk.Findings);
    }

    private static void Visit(Base node, Base? parent, string name, string path, bool scanText, Walk walk)
    {
        var element = new CheckedElement(node, parent, name, walk.Options);
        walk.Findings.AddRange(Checks
            .Where(check => check.Fails(element))
            .Select(check => new LeakFinding(path, 0, 0, check.Category, [$"structure:{check.Name}"])));

        if (scanText && TextOf(element, path) is { } text)
        {
            walk.Texts.Add(text);
        }

        // Kept vocabulary is not scanned for names, but its structure still is.
        var scanChildren = scanText && node is not (Coding or Quantity or Address);
        foreach (var child in FhirChildren.Of(node))
        {
            Visit(child.Value, node, child.Name, child.PathFrom(path), scanChildren, walk);
        }
    }

    // The text a rule left in place, to scan for the input's known identifiers.
    private static ScannedValue? TextOf(CheckedElement element, string path) => element.Node switch
    {
        Markdown markdown => new ScannedValue(path, markdown.Value ?? string.Empty),
        FhirString text when element.Name != "id" => new ScannedValue(path, text.Value ?? string.Empty),
        Attachment attachment when IsInspectable(attachment, out var decoded) => new ScannedValue(path + ".data", decoded),
        _ => null,
    };

    private static bool KeepsMoreThanStateAndZip(Address address) =>
        address.Line.Any() || address.City is not null || address.District is not null
        || address.Text is not null || address.PostalCode is { Length: > 3 };

    private static bool IsFinerThanYear(Base node) => node switch
    {
        Date date => !IsYearOnly(date.Value),
        FhirDateTime dateTime => !IsYearOnly(dateTime.Value),
        _ => false,
    };

    private static bool IsYearOnly(string? value) =>
        FhirDates.TryParse(value, out var date, out var timestamp) && timestamp is null && date.Precision == DatePrecision.Year;

    private static bool IsAllowed(Extension extension, FhirDeidentificationOptions options) =>
        extension.Url is { } url && options.AllowedExtensions.Contains(url);

    // Only inline text, with no URL to fetch more from and no hash of the original, can
    // be checked.
    private static bool IsInspectable(Attachment attachment, out string text)
    {
        text = string.Empty;
        return attachment is { Url: null, Hash: null } && InlineText.TryDecode(attachment, out text);
    }

    // The value of an element that can name a server: a bundle entry's full URL or
    // request URL, or a reference.
    private static string? ServerAddress(CheckedElement element) => element switch
    {
        { Node: FhirUri uri, Parent: Bundle.EntryComponent, Name: "fullUrl" } => uri.Value,
        { Node: FhirUri uri, Parent: Bundle.RequestComponent, Name: "url" } => uri.Value,
        { Node: FhirString reference, Parent: ResourceReference, Name: "reference" } => reference.Value,
        _ => null,
    };

    // An absolute reference or full URL must point at the placeholder server, never the source's.
    private static bool NamesSourceServer(string value) =>
        value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        && !value.StartsWith(ReferenceRewriter.PlaceholderBase, StringComparison.Ordinal);

    // A URI the rules should have removed: one that describes (a device's url, an
    // endpoint's address) rather than defines, outside the elements whose systems and
    // URLs other checks cover.
    private static bool IsDescribingUri(CheckedElement element) =>
        element.Node is FhirUri or FhirUrl or Oid or Uuid
        && element.Parent is not (null or Coding or Identifier or Quantity or Attachment)
        && element.Name != "system"
        && ServerAddress(element) is null
        && !UriRule.IsDefining(element.Parent, element.Name);

    /// <summary>One element as the checks see it: itself, where it sits, and the run's options.</summary>
    private sealed record CheckedElement(Base Node, Base? Parent, string Name, FhirDeidentificationOptions Options)
    {
        public bool SafeHarbor => Options.Mode == FhirDeidentificationMode.SafeHarbor;
    }

    /// <summary>One structural check: reported as "structure:{Name}" when an element fails it.</summary>
    private sealed record StructureCheck(string Name, PhiCategory Category, Func<CheckedElement, bool> Fails);

    /// <summary>What the walk has found so far.</summary>
    private sealed class Walk(FhirDeidentificationOptions options)
    {
        public FhirDeidentificationOptions Options { get; } = options;

        public List<LeakFinding> Findings { get; } = [];

        public List<ScannedValue> Texts { get; } = [];
    }
}
