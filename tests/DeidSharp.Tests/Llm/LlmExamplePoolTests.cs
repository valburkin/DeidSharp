using DeidSharp.Llm;

namespace DeidSharp.Tests.Llm;

public class LlmExamplePoolTests
{
    [Fact]
    public void Ships_a_pool_of_fictional_notes_whose_answers_are_in_their_text()
    {
        var pool = LlmExamplePool.Default;

        Assert.True(pool.Count >= 40);
        Assert.Contains(pool, e => e.Phi.Count == 0);
        Assert.All(pool.SelectMany(e => e.Phi.Select(p => (e.Text, p.Text))), pair => Assert.Contains(pair.Item2, pair.Item1, StringComparison.Ordinal));
    }
}
