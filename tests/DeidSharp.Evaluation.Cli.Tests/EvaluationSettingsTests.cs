using DeidSharp.Evaluation.Cli.Corpora;
using DeidSharp.Evaluation.Cli.Predictions;
using DeidSharp.Evaluation.Cli.Reporting;

namespace DeidSharp.Evaluation.Cli.Tests;

public class EvaluationSettingsTests
{
    [Fact]
    public void Defaults_to_the_bundled_asq_phi_copy_and_the_safe_harbor_scope()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi"]);

        Assert.Equal("asq-phi", settings.Corpus.Id);
        Assert.Equal(EvaluationScope.SafeHarbor, settings.Scope);
        Assert.Null(settings.Presidio);
        Assert.Null(settings.MissesPath);
    }

    [Fact]
    public void Reads_endpoints_and_the_scope()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--scope", "all", "--clinical", "http://127.0.0.1:5003/"]);

        Assert.Equal(EvaluationScope.All, settings.Scope);
        Assert.Equal(new Uri("http://127.0.0.1:5003/"), settings.Clinical);
    }

    [Theory]
    [InlineData(new[] { "--corpus", "i2b2" }, "--corpus must be one of")]
    [InlineData(new[] { "--corpus", "physionet-deid", "--gold", "id-phi.phrase" }, "--text is required")]
    [InlineData(new[] { "--corpus", "asq-phi", "--scope", "hipaa" }, "--scope must be one of")]
    [InlineData(new[] { "--corpus", "asq-phi", "--presidio", "localhost:5002" }, "--presidio must be an absolute")]
    [InlineData(new[] { "--corpus", "asq-phi", "--text", "id.text" }, "--text is not an option here")]
    [InlineData(new[] { "--corpus", "asq-phi", "--corpus", "asq-phi" }, "given twice")]
    [InlineData(new[] { "--corpus" }, "--corpus needs a value")]
    [InlineData(new[] { "asq-phi" }, "should be an option")]
    public void Refuses_a_command_line_it_cannot_follow(string[] args, string message)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(args));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("misses")]
    [InlineData("false-positives")]
    public void Refuses_a_review_file_inside_the_repository(string option)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", $"--{option}", RepositoryFiles.Path("review.txt")]));

        Assert.Contains($"--{option} must point outside any Git working tree", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_review_file_that_another_option_names()
    {
        using var temp = new TempDirectory();
        var queries = temp.File("queries.txt", "");

        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--queries", queries, "--misses", queries]));

        Assert.Contains("--misses names a file another option uses too", e.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("two\nlines")]
    public void Refuses_a_note_that_is_not_one_line_of_text(string note)
    {
        Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--note", note]));
    }

    [Fact]
    public void Reads_the_dropped_sources()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--drop-sources", "clinical:SpacyRecognizer, presidio:UrlRecognizer"]);

        Assert.Equal(["clinical:SpacyRecognizer", "presidio:UrlRecognizer"], settings.DroppedSources.Order(StringComparer.Ordinal));
        Assert.Empty(EvaluationSettings.Parse(["--corpus", "asq-phi"]).DroppedSources);
    }

    [Theory]
    [InlineData("SpacyRecognizer")]
    [InlineData("clinical:")]
    [InlineData(":SpacyRecognizer")]
    public void Refuses_a_dropped_source_without_its_service(string source)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--drop-sources", source]));

        Assert.Contains("--drop-sources takes recognizers", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_publish_with_dropped_sources()
    {
        using var temp = new TempDirectory();

        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--drop-sources", "clinical:SpacyRecognizer", "--publish", temp.Path]));

        Assert.Contains("can't be published", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_named_prediction_files()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", "deid=a.tsv, philter-ucsf=b.tsv"]);

        Assert.Equal([("deid", "a.tsv"), ("philter-ucsf", "b.tsv")], settings.Predictions);
        Assert.Empty(EvaluationSettings.Parse(["--corpus", "asq-phi"]).Predictions);
    }

    [Theory]
    [InlineData("a.tsv", "takes name=file pairs")]
    [InlineData("deid=", "takes name=file pairs")]
    [InlineData("de id=a.tsv", "takes name=file pairs")]
    [InlineData("deid=a.tsv,deid=b.tsv", "twice, or like one of DeidSharp's")]
    [InlineData("deterministic=a.tsv", "twice, or like one of DeidSharp's")]
    public void Refuses_prediction_files_it_cannot_tell_apart(string value, string message)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", value]));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_prediction_files_to_compare_every_stack_with()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", "deid=a.tsv,philter=b.tsv", "--compare-with", "deid, philter"]);

        Assert.Equal(["deid", "philter"], settings.CompareWith);
        Assert.Empty(EvaluationSettings.Parse(["--corpus", "asq-phi"]).CompareWith);
    }

    [Theory]
    [InlineData("philter")]
    [InlineData("deid,deid")]
    public void Refuses_a_comparison_with_a_file_it_was_not_given_once(string value)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", "deid=a.tsv", "--compare-with", value]));

        Assert.Contains("--compare-with takes names given to --predictions", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_an_export_with_the_corpus_and_its_split_only()
    {
        using var temp = new TempDirectory();

        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--split", "all", "--export", temp.Path]);
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--export", temp.Path, "--clinical", "http://127.0.0.1:5003/"]));

        Assert.Equal(temp.Path, Assert.IsType<CorpusExport>(settings.Output).Destination);
        Assert.Contains("--clinical is not an option here", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_a_gold_review_with_the_corpus_and_its_split_only()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "gold.txt");

        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--split", "tune", "--gold-review", path]);
        var other = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--gold-review", path, "--clinical", "http://127.0.0.1:5003/"]));
        var both = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--gold-review", path, "--export", temp.Path]));

        Assert.Equal(path, Assert.IsType<GoldReviewFile>(settings.Output).Destination);
        Assert.Contains("--clinical is not an option here", other.Message, StringComparison.Ordinal);
        Assert.Contains("separate runs", both.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_gold_review_that_would_overwrite_a_corpus_file()
    {
        using var temp = new TempDirectory();
        var queries = Path.Combine(temp.Path, "queries.txt");

        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--queries", queries, "--gold-review", queries]));

        Assert.Contains("--gold-review names a file another option uses too", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_gold_review_inside_the_repository()
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--gold-review", RepositoryFiles.Path("gold.txt")]));

        Assert.Contains("--gold-review must point outside any Git working tree", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_mimic_iv_note_corpus_with_its_notes()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "mimic-iv-note", "--notes", "discharge.csv.gz"]);
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "mimic-iv-note"]));

        Assert.Equal("mimic-iv-note", settings.Corpus.Id);
        Assert.Contains("--notes", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_an_export_inside_the_repository()
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--export", RepositoryFiles.Path("export")]));

        Assert.Contains("--export must point outside any Git working tree", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_review_file_that_would_overwrite_a_prediction_file()
    {
        using var temp = new TempDirectory();
        var predictions = temp.File("deid.tsv", "");

        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--predictions", $"deid={predictions}", "--misses", predictions]));

        Assert.Contains("--misses names a file another option uses too", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_variants_to_measure()
    {
        Assert.Equal(["repeated-names"], EvaluationSettings.Parse(["--corpus", "asq-phi", "--variants", "repeated-names"]).Variants);
        Assert.Empty(EvaluationSettings.Parse(["--corpus", "asq-phi"]).Variants);
    }

    [Theory]
    [InlineData("lexicon")]
    [InlineData("repeated-names,repeated-names")]
    public void Refuses_variants_it_does_not_know_or_repeats(string value)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--variants", value]));

        Assert.Contains("--variants takes a list of: repeated-names", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_second_opinions_and_the_ablation_and_refuses_to_publish_an_ablation()
    {
        using var temp = new TempDirectory();

        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--llm", "m", "--llm-second-opinion", "on,self", "--ablation", "on"]);
        var noModel = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--llm-second-opinion", "on"]));
        var unknown = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--llm", "m", "--llm-second-opinion", "veto"]));
        var published = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--ablation", "on", "--publish", temp.Path]));

        Assert.Equal(["on", "self"], settings.SecondOpinions);
        Assert.True(settings.Ablation);
        Assert.False(EvaluationSettings.Parse(["--corpus", "asq-phi"]).Ablation);
        Assert.Contains("needs --llm", noModel.Message, StringComparison.Ordinal);
        Assert.Contains("takes a list of: on, self", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("can't be published", published.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_clinical_places_stack_only_with_the_clinical_sidecar()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--clinical", "http://127.0.0.1:5003/", "--clinical-places", "on"]);
        var noSidecar = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--clinical-places", "on"]));

        Assert.True(settings.ClinicalPlaces);
        Assert.Contains("needs --clinical", noSidecar.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_publish_variants()
    {
        using var temp = new TempDirectory();

        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--variants", "repeated-names", "--publish", temp.Path]));

        Assert.Contains("can't be published", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_llm_models_prompts_and_local_endpoint()
    {
        var settings = EvaluationSettings.Parse(["--corpus", "asq-phi", "--llm", "qwen3.5:9b, gemma4:12b", "--llm-prompts", "instructions,examples", "--llm-endpoint", "http://localhost:11434/"]);

        Assert.Equal(["qwen3.5:9b", "gemma4:12b"], settings.LlmModels);
        Assert.Equal(["instructions", "examples"], settings.LlmPrompts);
        Assert.Equal(new Uri("http://localhost:11434/"), settings.LlmEndpoint);
        Assert.Equal(["instructions"], EvaluationSettings.Parse(["--corpus", "asq-phi"]).LlmPrompts);
    }

    [Theory]
    [InlineData("--llm-endpoint", "http://10.0.0.5:11434/", "must be on this machine")]
    [InlineData("--llm-endpoint", "https://api.example.com/", "must be on this machine")]
    [InlineData("--llm-prompts", "retrieval", "--llm-prompts takes a list of: instructions, examples")]
    public void Refuses_llm_settings_that_would_send_notes_away_or_that_it_does_not_know(string option, string value, string message)
    {
        var e = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--llm", "qwen3.5:9b", option, value]));

        Assert.Contains(message, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Publishes_the_recommended_stacks_with_an_llm_layer_but_not_the_self_check()
    {
        using var temp = new TempDirectory();
        temp.File(ResultsPublisher.PageFile, $"{ResultsPage.Begin}\n{ResultsPage.End}\n");
        string[] recordRun =
        [
            "--corpus", "asq-phi", "--clinical", "http://127.0.0.1:5003/", "--clinical-places", "on", "--gazetteer", "with,without",
            "--llm", "gemma4:26b-a4b", "--llm-prompts", "all-examples", "--llm-second-opinion", "on", "--publish", temp.Path,
        ];

        var settings = EvaluationSettings.Parse(recordRun);
        var selfCheck = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(
            ["--corpus", "asq-phi", "--llm", "gemma4:26b-a4b", "--llm-second-opinion", "on,self", "--publish", temp.Path]));

        Assert.Equal(temp.Path, settings.PublishDirectory);
        Assert.Equal(["gemma4:26b-a4b"], settings.LlmModels);
        Assert.Equal([true, false], settings.Gazetteer);
        Assert.Contains("self is an experiment, so it can't be published", selfCheck.Message, StringComparison.Ordinal);
        var retrieved = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(
            ["--corpus", "asq-phi", "--llm", "gemma4:26b-a4b", "--llm-prompts", "all-examples,retrieved", "--publish", temp.Path]));
        Assert.Contains("retrieved is an experiment, so it can't be published", retrieved.Message, StringComparison.Ordinal);
        var ablation = Assert.Throws<UsageException>(() => EvaluationSettings.Parse(
            ["--corpus", "asq-phi", "--llm", "gemma4:26b-a4b", "--llm-prompts", "focused-rules", "--publish", temp.Path]));
        Assert.Contains("focused-rules is an experiment, so it can't be published", ablation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_whether_stacks_have_the_gazetteer()
    {
        Assert.Equal([true], EvaluationSettings.Parse(["--corpus", "asq-phi"]).Gazetteer);
        Assert.Equal([true, false], EvaluationSettings.Parse(["--corpus", "asq-phi", "--gazetteer", "with,without"]).Gazetteer);
        Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--gazetteer", "maybe"]));
    }

    [Fact]
    public void Keeps_the_note()
    {
        Assert.Equal("clinical sidecar on CUDA", EvaluationSettings.Parse(["--corpus", "asq-phi", "--note", "clinical sidecar on CUDA"]).Note);
    }

    [Fact]
    public void Accepts_a_misses_file_outside_any_repository()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "misses.txt");

        Assert.Equal(path, EvaluationSettings.Parse(["--corpus", "asq-phi", "--misses", path]).MissesPath);
    }

    [Fact]
    public void Keeps_the_label_to_profile_and_refuses_a_blank_one()
    {
        Assert.Equal("UntypedName", EvaluationSettings.Parse(["--corpus", "asq-phi", "--miss-profile", "UntypedName"]).MissProfileLabel);
        Assert.Null(EvaluationSettings.Parse(["--corpus", "asq-phi"]).MissProfileLabel);
        Assert.Throws<UsageException>(() => EvaluationSettings.Parse(["--corpus", "asq-phi", "--miss-profile", " "]));
    }
}
