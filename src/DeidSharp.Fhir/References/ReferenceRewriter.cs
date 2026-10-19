using System.Text.RegularExpressions;
using Hl7.Fhir.Model;
using DeidSharp.Pseudonymization;

namespace DeidSharp.Fhir.References;

/// <summary>
/// Replaces resource ids with pseudonyms and rewrites every way of pointing at a
/// resource the same way, so links inside the output still resolve: "Patient/123",
/// "urn:uuid:123", "https://server/fhir/Patient/123/_history/2" and a resource whose id
/// is "123" all map to the same UUID-shaped pseudonym. An absolute URL's server part is
/// replaced by <see cref="PlaceholderBase"/>: a host name, an IP address or a path can
/// identify (".../patients/jane.doe.1950/Patient/123"), and every absolute reference
/// gets the same base so references still resolve against full URLs. Local references
/// ("#med1") to contained resources are left alone; contained ids are local and identify
/// nothing.
///
/// A conditional reference ("Practitioner?identifier=http://hl7.org/fhir/sid/us-npi|99…",
/// as Synthea writes them) is resolved against the bundle when an entry carries that
/// identifier, and then points at that entry's pseudonym. The identifier itself never
/// reaches the output.
/// </summary>
public sealed partial class ReferenceRewriter
{
    private const string Domain = "fhir-resource-id";

    /// <summary>The server part every absolute reference and full URL is given (".invalid" is a reserved name).</summary>
    public const string PlaceholderBase = "https://deidentified.invalid/fhir/";

    private readonly IPseudonymizer _pseudonymizer;

    // "Type?identifier=system|value" (and "Type?identifier=value") → the entry it names.
    private readonly Dictionary<string, string> _conditionalTargets = new(StringComparer.Ordinal);

    /// <param name="root">The resource being de-identified; a bundle's entries are indexed for conditional references.</param>
    public ReferenceRewriter(IPseudonymizer pseudonymizer, Resource? root = null)
    {
        _pseudonymizer = pseudonymizer ?? throw new ArgumentNullException(nameof(pseudonymizer));
        if (root is Bundle bundle)
        {
            IndexConditionalTargets(bundle);
        }
    }

    /// <summary>The pseudonym for a resource id.</summary>
    public string PseudonymizeId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return PseudonymFormats.Uuid(_pseudonymizer.Digest(Domain, id)).ToString("D");
    }

    /// <summary>
    /// The rewritten form of a reference or full URL, or null when it cannot be kept
    /// safely (a conditional reference whose query holds identifiers is replaced by a
    /// literal reference to a pseudonym of the whole query).
    /// </summary>
    public string? Rewrite(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var value = reference.Trim();
        if (value.StartsWith('#'))
        {
            return value;
        }

        if (value.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
        {
            return "urn:uuid:" + PseudonymizeId(value["urn:uuid:".Length..]);
        }

        if (value.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
        {
            return "urn:uuid:" + PseudonymizeId(value);
        }

        var conditional = ConditionalPattern().Match(value);
        if (conditional.Success)
        {
            var type = conditional.Groups["type"].Value;

            // Not in this bundle: the query holds identifiers, so point at a pseudonym of
            // the whole query instead; the same query maps the same way.
            return _conditionalTargets.TryGetValue(NormalizeQuery(conditional.Groups["query"].Value, type), out var target)
                ? Rewrite(target)
                : $"{type}/{PseudonymizeId(value)}";
        }

        var literal = LiteralPattern().Match(value);
        if (literal.Success)
        {
            var serverPart = literal.Groups["base"].Length > 0 ? PlaceholderBase : string.Empty;
            return $"{serverPart}{literal.Groups["type"].Value}/{PseudonymizeId(literal.Groups["id"].Value)}";
        }

        // A bare type ("Patient", as in a transaction request URL) holds no identifier.
        return TypeOnlyPattern().IsMatch(value) ? value : null;
    }

    private void IndexConditionalTargets(Bundle bundle)
    {
        foreach (var entry in bundle.Entry)
        {
            if (entry.Resource is not DomainResource resource)
            {
                continue;
            }

            var target = entry.FullUrl is { Length: > 0 } fullUrl ? fullUrl
                : resource.Id is { Length: > 0 } id ? $"{resource.TypeName}/{id}"
                : null;
            if (target is null)
            {
                continue;
            }

            var identifiers = resource.EnumerateElements()
                .Where(e => e.Key == "identifier")
                .SelectMany(e => e.Value switch
                {
                    Identifier single => [single],
                    IEnumerable<Identifier> many => many,
                    _ => [],
                });
            foreach (var identifier in identifiers.Where(i => !string.IsNullOrWhiteSpace(i.Value)))
            {
                _conditionalTargets.TryAdd(NormalizeQuery($"identifier={identifier.System}|{identifier.Value}", resource.TypeName), target);
                _conditionalTargets.TryAdd(NormalizeQuery($"identifier={identifier.Value}", resource.TypeName), target);
            }
        }
    }

    private static string NormalizeQuery(string query, string type) => $"{type}?{Uri.UnescapeDataString(query).Trim()}";

    // [base/]Type/id[/_history/version]: the version is dropped (it can be a timestamp).
    [GeneratedRegex(@"^(?<base>(?:https?://[^\s?#]+/)?)(?<type>[A-Z][A-Za-z]+)/(?<id>[A-Za-z0-9\-.]{1,64})(?:/_history/[^/\s]+)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex LiteralPattern();

    [GeneratedRegex(@"^(?:https?://[^\s?#]+/)?(?<type>[A-Z][A-Za-z]+)\?(?<query>.+)$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ConditionalPattern();

    [GeneratedRegex("^[A-Z][A-Za-z]+$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex TypeOnlyPattern();
}
