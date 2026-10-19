using System.Globalization;

namespace DeidSharp.Fhir;

/// <summary>
/// The input could not be read as FHIR. The message gives only how many problems the
/// parser found: the parser's own messages quote the input's values, which may be PHI.
/// </summary>
public sealed class FhirInputException : Exception
{
    public FhirInputException()
        : this("The input is not valid FHIR JSON.")
    {
    }

    public FhirInputException(string message)
        : base(message)
    {
    }

    public FhirInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal FhirInputException(int problems)
        : base(string.Create(CultureInfo.InvariantCulture, $"The input is not valid FHIR JSON ({problems} problem(s); details withheld because they quote the input)."))
    {
    }
}
