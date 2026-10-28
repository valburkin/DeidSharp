using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DeidSharp.Evaluation.Cli.Corpora.Surrogates;

/// <summary>
/// A small random number generator (SplitMix64) whose sequence is fixed by its seed on every
/// machine and runtime, so a generated corpus is the same wherever it is rebuilt.
/// <see cref="Random"/> promises no such thing across .NET versions.
/// </summary>
internal sealed class SeededRandom(ulong seed)
{
    private ulong _state = seed;

    /// <summary>A generator seeded from a purpose and a key ("deidsharp-mimic-surrogates-3", a note's id), so each key gets its own sequence.</summary>
    public static SeededRandom For(string purpose, string key) =>
        new(BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(purpose + ":" + key))));

    public ulong NextUInt64()
    {
        var z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A number in [0, <paramref name="maxExclusive"/>). The modulo's bias is below one in 10^9 for the ranges used here.</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

    /// <summary>A number in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}
