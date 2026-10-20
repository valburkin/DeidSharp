// De-identifies a FHIR R4 resource or bundle and prints it as JSON, with what the run did.
//
//   dotnet run --project samples/FhirBundle                         # Safe Harbor, the bundled fictional patient
//   dotnet run --project samples/FhirBundle -- my-bundle.json
//   DEIDSHARP_KEY=<base64 key> dotnet run --project samples/FhirBundle -- --pseudonymized
//
// A key is at least 32 random bytes, in base64: openssl rand -base64 32. Keep it in a secret store.
using DeidSharp.Fhir;
using DeidSharp.Pseudonymization;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

var pseudonymized = args.Contains("--pseudonymized");
var path = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Path.Combine(AppContext.BaseDirectory, "patient-bundle.json");

var options = pseudonymized
    ? new FhirDeidentificationOptions
    {
        // Pseudonyms and date shifts that stay the same across runs with the same key, so
        // exports can be linked. Not Safe Harbor: use it under a data use agreement.
        Mode = FhirDeidentificationMode.Pseudonymized,
        Key = PseudonymKey.FromBase64(Environment.GetEnvironmentVariable("DEIDSHARP_KEY")
            ?? throw new InvalidOperationException("Set DEIDSHARP_KEY to a base64 key of at least 32 bytes.")),
        FreeTextStyle = FreeTextStyle.Surrogates,
    }
    : new FhirDeidentificationOptions();

Resource input;
try
{
    input = new FhirJsonDeserializer().Deserialize<Resource>(await File.ReadAllTextAsync(path));
}
catch (Exception e) when (e is DeserializationFailedException or System.Text.Json.JsonException or FormatException)
{
    // The parser's messages quote the values it couldn't read, which may be PHI: report only how many.
    var problems = e is DeserializationFailedException failed ? failed.Exceptions.Count : 1;
    Console.Error.WriteLine($"{path} isn't valid FHIR R4 JSON ({problems} problem(s)).");
    return 1;
}

var (resource, stats, _) = await new FhirDeidentifier(options).DeidentifyAsync(input);

Console.WriteLine(new FhirJsonSerializer().SerializeToString(resource));

// Counts only: safe to log.
Console.Error.WriteLine(
    $"removed {stats.ElementsRemoved}, replaced {stats.ElementsReplaced}, ids pseudonymized {stats.IdsPseudonymized}, " +
    $"free-text values {stats.FreeTextValues} with {stats.FreeTextFindings} findings, modifier extensions removed {stats.ModifierExtensionsRemoved}");
return 0;
