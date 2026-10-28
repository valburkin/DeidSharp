namespace DeidSharp.Evaluation.Cli.Tests.Corpora.Surrogates;

/// <summary>Tiny surrogate lists in the shipped lists' formats, so a test knows every name that can be drawn.</summary>
internal static class SurrogateListsFixture
{
    public static readonly string[] FemaleNames = ["Ada", "Iris"];
    public static readonly string[] Names = ["Ada", "Iris", "Otto", "Hugo", "Quill", "Fenwick", "Ashdown"];

    public static string Write(TempDirectory directory)
    {
        directory.File("first-names.tsv", "# header\nAda\tF\t2.5\nIris\tF\t1.0\nOtto\tM\t2.0\nHugo\tM\t0.5\n");
        directory.File("surnames.txt", "# header\nquill\t300\nfenwick\t200\nashdown\t100\n");
        directory.File("places.txt", "# header\nMaplewood\nCedar Falls\n");
        return directory.Path;
    }
}
