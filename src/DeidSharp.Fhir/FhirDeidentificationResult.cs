using Hl7.Fhir.Model;
using DeidSharp.Verification;

namespace DeidSharp.Fhir;

/// <summary>The de-identified resource, what the run did, and what the output check found.</summary>
/// <param name="Verification">The output check; clean unless verification was turned off and something remained.</param>
public sealed record FhirDeidentificationResult(Resource Resource, FhirDeidentificationStatistics Statistics, LeakReport Verification);
