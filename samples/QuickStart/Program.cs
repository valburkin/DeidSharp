// De-identifies a note with the deterministic detectors: the record's own identifiers and
// the pattern library. No model, no network, no container.
//
//   dotnet run --project samples/QuickStart
using DeidSharp;
using DeidSharp.Detection;
using DeidSharp.Pseudonymization;
using DeidSharp.Structured;
using DeidSharp.Text;
using DeidSharp.Verification;

const string note = "Rosa Alvarez (MRN-48213) was seen on 03/04/2024 by Dr. Hart. Call her at 617-555-0142.";

// What the structured record already knows about the patient. Notes repeat exactly these,
// so looking for them directly is the most reliable layer.
var known = KnownIdentifiers.Empty
    .AddName("Rosa Alvarez")
    .AddDate(new DateOnly(1931, 3, 4))
    .AddIdentifier("MRN-48213", PhiCategory.MedicalRecordNumber);
var context = new TextContext(subjectId: "patient-1", known);
var detector = new CompositePhiDetector(DeterministicDetectors.Create());

// 1. Tags: "[NAME]", "[DATE]". Nothing in the output can be mistaken for real data.
var tagged = await new TextDeidentifier(detector, new TagReplacement()).DeidentifyAsync(note, context);
Console.WriteLine(tagged.Text);

// What was replaced: positions, categories and detectors, never the values, so it is safe to log.
foreach (var finding in tagged.Findings)
{
    Console.WriteLine($"  {finding.Category} at [{finding.InputStart}, {finding.InputEnd}) by {string.Join("+", finding.Sources)}");
}

// 2. Surrogates: realistic fakes, the same for the same patient under the same key. A real
//    key comes from a secret store (PseudonymKey.FromBase64); a fresh one links nothing
//    outside this run.
var pseudonymizer = new HmacPseudonymizer(PseudonymKey.Generate());
var surrogates = new TextDeidentifier(detector, new SurrogateReplacement(pseudonymizer, YearOnlyDateTransformer.Instance));
Console.WriteLine((await surrogates.DeidentifyAsync(note, context)).Text);

// 3. Check text produced elsewhere, such as a model's summary, before it leaves.
var report = LeakScanner.CreateDefault().Scan("Summary: Rosa Alvarez, CKD stage 3, SSN 123-45-6789.", known);
Console.WriteLine(report.IsClean ? "clean" : string.Join(Environment.NewLine, report.Describe()));
