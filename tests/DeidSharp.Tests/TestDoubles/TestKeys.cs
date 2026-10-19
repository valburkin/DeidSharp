using DeidSharp.Pseudonymization;

namespace DeidSharp.Tests.TestDoubles;

/// <summary>Fixed pseudonym keys, so test output is the same on every run.</summary>
internal static class TestKeys
{
    public static PseudonymKey Fixed { get; } = PseudonymKey.FromBytes([.. Enumerable.Range(1, 32).Select(i => (byte)i)]);

    public static PseudonymKey Other { get; } = PseudonymKey.FromBytes([.. Enumerable.Range(101, 32).Select(i => (byte)i)]);
}
