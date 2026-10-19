using System.Collections.Frozen;
using DeidSharp.Detection;
using DeidSharp.Fhir.Rules;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;

namespace DeidSharp.Fhir;

// Design pattern: Parameter Object.
// The pattern: settings that travel together are grouped into one object, instead of
// a long list of separate arguments.
// Here: everything a FHIR run can be told. An immutable record with Safe Harbor
// defaults, so a caller changes only what it needs ("new() { Mode = Pseudonymized,
// Key = key }") and the object checks its own values.

/// <summary>How <see cref="FhirDeidentifier"/> treats a resource.</summary>
public sealed record FhirDeidentificationOptions
{
    /// <summary>US Core extensions kept by default: race, ethnicity and birth sex carry no identifier.</summary>
    public static IReadOnlySet<string> DefaultAllowedExtensions { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-race",
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity",
        "http://hl7.org/fhir/us/core/StructureDefinition/us-core-birthsex");

    public FhirDeidentificationMode Mode { get; init; } = FhirDeidentificationMode.SafeHarbor;

    /// <summary>
    /// The pseudonym key. Required for <see cref="FhirDeidentificationMode.Pseudonymized"/>.
    /// For Safe Harbor, leave it null: each run then uses a fresh random key, so its
    /// pseudonyms cannot be linked to anything outside the run.
    /// </summary>
    public PseudonymKey? Key { get; init; }

    /// <summary>
    /// Finds PHI in free text (notes, comments, text attachments). Null uses the
    /// deterministic stack: the record's known identifiers plus the pattern library. Add a
    /// Presidio detector here for NER on names and places that the record doesn't list.
    /// </summary>
    public IPhiDetector? FreeTextDetector { get; init; }

    public FreeTextStyle FreeTextStyle { get; init; } = FreeTextStyle.Tags;

    /// <summary>
    /// Extension URLs to keep; every other extension is removed (fail-closed: an unknown
    /// extension may hold anything). Sub-extensions of a kept extension are kept.
    /// </summary>
    public IReadOnlySet<string> AllowedExtensions { get; init; } = DefaultAllowedExtensions;

    /// <summary>
    /// How many free-text values are de-identified at once. Free text is queued while the
    /// resource is walked and run together at the end, so the notes of a large bundle wait
    /// on a detector service a few at a time, not one after another. Default 8; 1 runs them
    /// one at a time. Every detector in <see cref="FreeTextDetector"/> must then be safe to
    /// call concurrently, as <see cref="IPhiDetector"/> requires.
    /// </summary>
    public int MaxFreeTextConcurrency { get; init; } = 8;

    /// <summary>The date ages are computed at. Null: today (UTC) from <see cref="TimeProvider"/>.</summary>
    public DateOnly? ReferenceDate { get; init; }

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>Patients this old or older lose their birth date entirely. Safe Harbor: 90.</summary>
    public int TopCodeAge { get; init; } = 90;

    /// <summary>The largest date shift in pseudonymized mode, in days either way. Default 365.</summary>
    public int MaxDateShiftDays { get; init; } = 365;

    public ZipCodeGeneralizer ZipCodes { get; init; } = ZipCodeGeneralizer.SafeHarbor;

    /// <summary>
    /// Checks the output after de-identifying and throws
    /// <see cref="DeidSharp.Verification.ResidualPhiException"/> on any leftover. Default true.
    /// </summary>
    public bool VerifyOutput { get; init; } = true;

    /// <summary>Rules that run before the built-in ones, e.g. to remove a local extension's content differently.</summary>
    public IReadOnlyList<IFhirElementRule> AdditionalRules { get; init; } = [];

    /// <exception cref="ArgumentException">Pseudonymized mode has no key.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The top-code age, the date shift or the free-text concurrency is out of range.</exception>
    /// <exception cref="ArgumentNullException">A collection or service is null.</exception>
    internal void Validate()
    {
        if (this is { Mode: FhirDeidentificationMode.Pseudonymized, Key: null })
        {
            throw new ArgumentException("Pseudonymized mode needs a PseudonymKey: without one, pseudonyms could not be linked across runs.", nameof(Key));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(TopCodeAge, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(TopCodeAge, 150);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxDateShiftDays, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxDateShiftDays, 3650);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxFreeTextConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaxFreeTextConcurrency, 256);
        ArgumentNullException.ThrowIfNull(AllowedExtensions);
        ArgumentNullException.ThrowIfNull(AdditionalRules);
        ArgumentNullException.ThrowIfNull(ZipCodes);
        ArgumentNullException.ThrowIfNull(TimeProvider);
    }
}

/// <summary>What found PHI in free text becomes.</summary>
public enum FreeTextStyle
{
    /// <summary>"[NAME]", "[DATE]": obvious and unambiguous.</summary>
    Tags,

    /// <summary>Realistic fakes, consistent per patient (see <see cref="Text.SurrogateReplacement"/>).</summary>
    Surrogates,
}
