using System.Diagnostics;
using Hl7.Fhir.Model;
using DeidSharp.Detection;
using DeidSharp.Diagnostics;
using DeidSharp.Fhir.Rules;
using DeidSharp.Fhir.Verification;
using DeidSharp.Verification;

namespace DeidSharp.Fhir;

// Design pattern: Facade.
// The pattern: one simple entry point in front of several cooperating parts, so
// callers don't have to wire them together themselves.
// Here: de-identifying a bundle needs a pseudonymizer, a date strategy, identifier
// extraction, free-text de-identification, the rule chain, a walker and an output
// check, all configured for one mode. Callers pass options and a resource.

/// <summary>
/// De-identifies FHIR R4 resources and bundles. The input is never modified: the
/// result is a de-identified deep copy. By default the output is verified and
/// <see cref="ResidualPhiException"/> is thrown if anything identifying remains.
/// </summary>
public sealed class FhirDeidentifier
{
    private readonly FhirDeidentificationOptions _options;
    private readonly IReadOnlyList<IFhirElementRule> _rules;

    public FhirDeidentifier(FhirDeidentificationOptions? options = null)
    {
        _options = options ?? new FhirDeidentificationOptions();
        _options.Validate();
        _rules = [.. _options.AdditionalRules, .. DefaultFhirRules.All];
    }

    /// <exception cref="ResidualPhiException">Verification is on and the output still holds possible PHI.</exception>
    /// <exception cref="PhiDetectionException">A free-text detector failed.</exception>
    public async Task<FhirDeidentificationResult> DeidentifyAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);

        // The resource type and counts only: names, ids and values stay out of traces.
        using var activity = DeidSharpTelemetry.Source.CreateActivity("deidsharp.fhir.deidentify", ActivityKind.Internal)?.Start();
        activity?.SetTag("deidsharp.fhir.resource_type", resource.TypeName);
        activity?.SetTag("deidsharp.fhir.mode", _options.Mode.ToString());
        try
        {
            return await DeidentifyCopyAsync(resource, activity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (activity is not null)
        {
            // The status says only that it failed: the exception's message is not recorded.
            activity.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    private async Task<FhirDeidentificationResult> DeidentifyCopyAsync(Resource resource, Activity? activity, CancellationToken cancellationToken)
    {
        var copy = resource.DeepCopy();
        var session = FhirSessionFactory.Create(_options, copy, cancellationToken);

        await new FhirElementWalker(_rules, session).WalkAsync(copy).ConfigureAwait(false);
        await session.DeidentifyQueuedTextAsync().ConfigureAwait(false);

        var report = FhirLeakVerifier.Verify(copy, session.Known, _options, cancellationToken);
        activity?.SetTag("deidsharp.fhir.free_text_values", session.Statistics.FreeTextValues);
        if (!_options.VerifyOutput || report.IsClean)
        {
            return new FhirDeidentificationResult(copy, session.Statistics, report);
        }

        DeidSharpTelemetry.RecordResidualRefusal("fhir");
        throw new ResidualPhiException(report);
    }

    /// <summary>De-identifies a resource or bundle given as FHIR JSON and returns FHIR JSON.</summary>
    /// <exception cref="FhirInputException">The input is not valid FHIR JSON.</exception>
    public async Task<string> DeidentifyJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var result = await DeidentifyAsync(FhirJson.Parse(json), cancellationToken).ConfigureAwait(false);
        return FhirJson.Serialize(result.Resource);
    }
}
