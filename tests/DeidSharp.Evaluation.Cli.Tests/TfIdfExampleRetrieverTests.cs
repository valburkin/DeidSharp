using DeidSharp.Llm;

namespace DeidSharp.Evaluation.Cli.Tests;

public class TfIdfExampleRetrieverTests
{
    private static readonly LlmExample Vent = new("PS 12/5, FiO2 40%. Lungs coarse, suctioned.", []);
    private static readonly LlmExample Family = new("Daughter Marisol called, will visit tomorrow.", [new LlmFinding("Marisol", "name")]);
    private static readonly LlmExample Transfer = new("Transfer to Lakeview Rehab when bed available.", [new LlmFinding("Lakeview Rehab", "location")]);

    [Fact]
    public void Picks_the_examples_whose_words_are_most_alike_first()
    {
        var retriever = new TfIdfExampleRetriever([Vent, Family, Transfer]);

        Assert.Equal([Family, Vent], retriever.Retrieve("Son called from home, will visit tonight. FiO2 weaned.", 2));
        Assert.Equal([Transfer], retriever.Retrieve("Plan transfer to rehab, bed pending", 1));
    }

    [Fact]
    public void Keeps_the_pools_order_for_texts_with_no_word_in_common_and_never_returns_more_than_it_has()
    {
        var retriever = new TfIdfExampleRetriever([Vent, Family, Transfer]);

        Assert.Equal([Vent, Family, Transfer], retriever.Retrieve("xyzzy", 10));
        Assert.Empty(retriever.Retrieve("FiO2", 0));
    }
}
