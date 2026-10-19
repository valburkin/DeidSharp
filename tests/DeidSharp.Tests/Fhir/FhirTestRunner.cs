using System.Collections;
using System.Text;
using Hl7.Fhir.Model;
using DeidSharp.Fhir;
using DeidSharp.Tests.TestDoubles;

namespace DeidSharp.Tests.Fhir;

/// <summary>Runs the FHIR de-identifier with test defaults and walks results.</summary>
internal static class FhirTestRunner
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    public static FhirDeidentificationOptions SafeHarbor(Func<FhirDeidentificationOptions, FhirDeidentificationOptions>? configure = null) =>
        Options(FhirDeidentificationMode.SafeHarbor, configure);

    public static FhirDeidentificationOptions Pseudonymized(Func<FhirDeidentificationOptions, FhirDeidentificationOptions>? configure = null) =>
        Options(FhirDeidentificationMode.Pseudonymized, configure);

    public static Task<FhirDeidentificationResult> Run(Resource resource, FhirDeidentificationOptions? options = null) =>
        new FhirDeidentifier(options ?? SafeHarbor()).DeidentifyAsync(resource, TestContext.Current.CancellationToken);

    public static async Task<T> RunAs<T>(Resource resource, FhirDeidentificationOptions? options = null)
        where T : Resource => Assert.IsType<T>((await Run(resource, options)).Resource);

    /// <summary>Every element in the tree, depth first.</summary>
    public static IEnumerable<Base> All(Base root)
    {
        yield return root;
        foreach (var (_, value) in root.EnumerateElements())
        {
            var children = value switch
            {
                Base b => [b],
                IEnumerable list and not string => list.OfType<Base>(),
                _ => [],
            };
            foreach (var child in children)
            {
                foreach (var node in All(child))
                {
                    yield return node;
                }
            }
        }
    }

    public static T Entry<T>(Bundle bundle)
        where T : Resource => bundle.Entry.Select(e => e.Resource).OfType<T>().Single();

    /// <summary>The text of a document's only attachment.</summary>
    public static string AttachmentText(DocumentReference document) =>
        Encoding.UTF8.GetString(document.Content.Single().Attachment.Data!);

    // A fixed reference date and key, so every run gives the same output.
    private static FhirDeidentificationOptions Options(
        FhirDeidentificationMode mode, Func<FhirDeidentificationOptions, FhirDeidentificationOptions>? configure)
    {
        var options = new FhirDeidentificationOptions { Mode = mode, ReferenceDate = Today, Key = TestKeys.Fixed };
        return configure is null ? options : configure(options);
    }
}
