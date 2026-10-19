using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DeidSharp.Fhir;

/// <summary>
/// Reads and writes FHIR JSON. A parse error never carries the parser's own messages,
/// which quote the input.
/// </summary>
internal static class FhirJson
{
    /// <exception cref="FhirInputException">The input is not valid FHIR JSON.</exception>
    public static Resource Parse(string json)
    {
        try
        {
            return new FhirJsonDeserializer().Deserialize<Resource>(json);
        }
        catch (Exception ex) when (ex is DeserializationFailedException or System.Text.Json.JsonException or FormatException)
        {
            // The parser's messages quote the offending values ("'John Smith 1950-13-45'
            // is not a correct literal for a date"), so neither the message nor the inner
            // exception is passed on.
            throw new FhirInputException(ex is DeserializationFailedException failed ? failed.Exceptions.Count : 1);
        }
    }

    public static string Serialize(Resource resource) => new FhirJsonSerializer().SerializeToString(resource);
}
