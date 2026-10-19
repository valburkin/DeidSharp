using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Fhir.Known;
using DeidSharp.Fhir.References;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Text;

namespace DeidSharp.Fhir;

// Design pattern: Factory.
// The pattern: one class whose only job is to build a ready-to-use object, so the code
// that uses the object doesn't also have to know how to assemble it.
// Here: a run needs a pseudonymizer keyed for this run, the date strategy for the mode,
// the free-text pipeline, the record's known identifiers, a reference rewriter and a
// subject resolver, all set up from the options. The factory builds them;
// FhirDeidentifier only runs the steps.

/// <summary>Builds the services of one de-identification run from the options and the resource.</summary>
internal static class FhirSessionFactory
{
    /// <param name="root">The copy being de-identified; read before any rule changes it.</param>
    public static FhirDeidentificationSession Create(FhirDeidentificationOptions options, Resource root, CancellationToken cancellationToken)
    {
        // Without a key (Safe Harbor), each run gets a fresh random one, so its pseudonyms
        // cannot be linked to anything outside the run.
        var pseudonymizer = new HmacPseudonymizer(options.Key ?? PseudonymKey.Generate());
        var dates = DateStrategy(options, pseudonymizer);
        return new FhirDeidentificationSession(
            options,
            pseudonymizer,
            dates,
            new ReferenceRewriter(pseudonymizer, root),
            FreeText(options, pseudonymizer, dates),
            FhirKnownIdentifierExtractor.Extract(root),
            new SubjectResolver(root),
            options.ReferenceDate ?? DateOnly.FromDateTime(options.TimeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
    }

    private static IDateTransformer DateStrategy(FhirDeidentificationOptions options, IPseudonymizer pseudonymizer) =>
        options.Mode == FhirDeidentificationMode.SafeHarbor
            ? YearOnlyDateTransformer.Instance
            : new ShiftingDateTransformer(new DateShifter(pseudonymizer, options.MaxDateShiftDays));

    private static TextDeidentifier FreeText(FhirDeidentificationOptions options, IPseudonymizer pseudonymizer, IDateTransformer dates)
    {
        var detector = options.FreeTextDetector ?? new CompositePhiDetector(DeterministicDetectors.Create());
        IReplacementStrategy replacement = options.FreeTextStyle == FreeTextStyle.Surrogates
            ? new SurrogateReplacement(pseudonymizer, dates, options.ZipCodes, new AgeGeneralizer(topCodeAge: options.TopCodeAge))
            : new TagReplacement();
        return new TextDeidentifier(detector, replacement, new TextDeidentifierOptions { VerifyOutput = options.VerifyOutput });
    }
}
