using DeidSharp.Evaluation.Cli.Corpora.Surrogates;

namespace DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

/// <summary>The generator a surrogate corpus is drawn from: the same sequence on every machine.</summary>
public class SeededRandomTests
{
    [Fact]
    public void Gives_splitmix64s_published_sequence()
    {
        // The reference implementation's first outputs for seed 1234567.
        var random = new SeededRandom(1234567);

        Assert.Equal(6457827717110365317UL, random.NextUInt64());
        Assert.Equal(3203168211198807973UL, random.NextUInt64());
    }

    [Fact]
    public void Gives_each_key_its_own_sequence_and_the_same_one_each_time()
    {
        Assert.Equal(SeededRandom.For("test", "note 1").NextUInt64(), SeededRandom.For("test", "note 1").NextUInt64());
        Assert.NotEqual(SeededRandom.For("test", "note 1").NextUInt64(), SeededRandom.For("test", "note 2").NextUInt64());
    }

    [Fact]
    public void Stays_in_its_ranges()
    {
        var random = new SeededRandom(42);
        for (var i = 0; i < 1_000; i++)
        {
            Assert.InRange(random.Next(3, 7), 3, 6);
            Assert.InRange(random.NextDouble(), 0.0, 0.999_999_999_9);
        }
    }
}
