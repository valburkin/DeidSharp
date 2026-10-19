using System.Collections.Frozen;
using Hl7.Fhir.Model;

namespace DeidSharp.Fhir.References;

/// <summary>
/// Works out which patient each resource belongs to, so all of one patient's dates move
/// by the same shift and surrogates stay consistent for that patient. A Patient is its
/// own subject; another resource belongs to the Patient its subject, patient or
/// beneficiary reference points at; a contained resource belongs to its container's
/// patient; anything else belongs to the bundle's only patient.
///
/// A subject's key is the patient's id, however the patient is referred to
/// ("Patient/123", "urn:uuid:123", "https://server/fhir/Patient/123/_history/2", or a
/// patient outside the bundle), so the same patient gets the same shift in every export
/// with the same key. A resource that belongs to no identifiable patient gets a key of
/// its own, never one shared with other such resources.
///
/// Every resource present at construction is resolved up front, because
/// de-identification rewrites the very references this reads.
/// </summary>
public sealed class SubjectResolver
{
    private static readonly FrozenSet<string> SubjectElements =
        FrozenSet.Create(StringComparer.Ordinal, "subject", "patient", "beneficiary", "individual");

    // Every form a bundle's patients are referred to by (id, full URL) → subject key.
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);
    private readonly Dictionary<Resource, string> _resolved = new(ReferenceEqualityComparer.Instance);
    private readonly string? _onlyPatient;

    public SubjectResolver(Resource root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var keys = new List<string>();
        foreach (var (patient, fullUrl) in Patients(root))
        {
            var key = patient.Id is { Length: > 0 } id ? id
                : fullUrl is { Length: > 0 } ? Normalize(fullUrl)
                : $"patient-{keys.Count}";
            keys.Add(key);
            _resolved[patient] = key;
            _aliases[key] = key;
            if (fullUrl is { Length: > 0 })
            {
                _aliases[Normalize(fullUrl)] = key;
            }
        }

        _onlyPatient = keys.Distinct(StringComparer.Ordinal).Count() == 1 ? keys[0] : null;
        ResolveAll(root, inherited: null);
    }

    public string SubjectOf(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Remember(resource, inherited: null);
    }

    // The subject resolved before, or else (in order) the patient the resource points at,
    // its container's patient, the bundle's only patient, or a key of its own.
    private string Remember(Resource resource, string? inherited)
    {
        if (_resolved.TryGetValue(resource, out var subject))
        {
            return subject;
        }

        subject = Resolve(resource) ?? inherited ?? _onlyPatient ?? Unattributed(resource);
        _resolved[resource] = subject;
        return subject;
    }

    private void ResolveAll(Resource resource, string? inherited)
    {
        var subject = Remember(resource, inherited);
        switch (resource)
        {
            case DomainResource domain:
                foreach (var contained in domain.Contained)
                {
                    ResolveAll(contained, subject);
                }

                break;
            case Bundle bundle:
                foreach (var entryResource in bundle.Entry.Select(e => e.Resource).OfType<Resource>())
                {
                    ResolveAll(entryResource, inherited: null);
                }

                break;
        }
    }

    // The patient this resource's own subject-like reference points at, or null.
    private string? Resolve(Resource resource)
    {
        if (resource is Patient patient)
        {
            return patient.Id is { Length: > 0 } id ? id : null;
        }

        foreach (var (name, value) in resource.EnumerateElements())
        {
            if (!SubjectElements.Contains(name))
            {
                continue;
            }

            foreach (var reference in References(value))
            {
                if (reference.Reference is { } target && Lookup(target) is { } key)
                {
                    return key;
                }
            }
        }

        return null;
    }

    private string? Lookup(string reference)
    {
        var normalized = Normalize(reference);
        if (_aliases.TryGetValue(normalized, out var key))
        {
            return key;
        }

        // A patient outside this bundle: keyed by its id, as it would be inside one.
        var isPatient = reference.StartsWith("Patient/", StringComparison.Ordinal)
            || reference.Contains("/Patient/", StringComparison.Ordinal);
        return isPatient ? normalized : null;
    }

    // "urn:uuid:123", "Patient/123", "https://s/fhir/Patient/123/_history/2" → "123".
    private static string Normalize(string reference)
    {
        var value = reference.Trim();
        var history = value.IndexOf("/_history/", StringComparison.Ordinal);
        if (history >= 0)
        {
            value = value[..history];
        }

        if (value.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
        {
            return value["urn:uuid:".Length..];
        }

        var marker = value.LastIndexOf("Patient/", StringComparison.Ordinal);
        return marker >= 0 ? value[(marker + "Patient/".Length)..] : value;
    }

    // A key no other resource shares: dates in it still move together, but cannot be
    // compared with another unattributed resource's to learn a common shift.
    private static string Unattributed(Resource resource) =>
        $"unattributed:{resource.TypeName}/{(resource.Id is { Length: > 0 } id ? id : Guid.NewGuid().ToString("N"))}";

    private static IEnumerable<ResourceReference> References(object? value) => value switch
    {
        ResourceReference single => [single],
        IEnumerable<ResourceReference> many => many,
        _ => [],
    };

    private static IEnumerable<(Patient Patient, string? FullUrl)> Patients(Resource root)
    {
        switch (root)
        {
            case Patient patient:
                yield return (patient, null);
                break;
            case Bundle bundle:
                foreach (var entry in bundle.Entry)
                {
                    if (entry.Resource is Patient p)
                    {
                        yield return (p, entry.FullUrl);
                    }
                }

                break;
        }
    }
}
