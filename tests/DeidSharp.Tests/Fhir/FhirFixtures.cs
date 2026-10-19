using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DeidSharp.Tests.Fhir;

/// <summary>Loads the FHIR test fixtures (fictional data shaped like Synthea output).</summary>
internal static class FhirFixtures
{
    /// <summary>
    /// One 93-year-old patient with every kind of identifier: names with accents,
    /// relatives, MRN/SSN/license, phones, addresses, a discharge note as a text
    /// attachment, conditional references, a device, coverage and a contained resource.
    /// </summary>
    public static Bundle RichPatientBundle() => Load<Bundle>("rich-patient-bundle.json");

    public static string RichPatientBundleJson() => Read("rich-patient-bundle.json");

    public static string ToJson(Resource resource) => new FhirJsonSerializer().SerializeToString(resource);

    public static T FromJson<T>(string json)
        where T : Resource => new FhirJsonDeserializer().Deserialize<T>(json);

    private static T Load<T>(string name)
        where T : Resource => FromJson<T>(Read(name));

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fhir", "Fixtures", name));

    /// <summary>The literal identifying strings in the rich bundle that must never appear in output.</summary>
    public static readonly string[] RichBundleSecrets =
    [
        "Rosa", "Álvarez", "Alvarez", "Núñez", "Nunez", "María", "Carmen", "Lucia", "Ferreira", "Amelia", "Hart",
        "4471-882-19", "123-45-6789", "S99923841", "9999912345", "617-555-0198", "617-555-0142", "413-555-0100",
        "rosa.alvarez", "amelia.hart", "Willow Creek", "Hospital Way", "Springfield", "Ponce", "01103", "01199",
        "1931-03-04", "03/04/1931", "Brendan", "MBR-55512-ZX", "10987654d321", "7654321D", "00844588003288",
        "ehr.st-brendan.example", "org-77", "5b2f1a52-6a9c-4b8e-9b1e-2a3f4c5d6e7f", "2jmj7l5rSw0yVb",
    ];
}
