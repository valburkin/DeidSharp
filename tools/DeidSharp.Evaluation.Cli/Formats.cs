using System.Globalization;

namespace DeidSharp.Evaluation.Cli;

/// <summary>How the tool writes numbers: the same on every machine, whatever its locale.</summary>
internal static class Formats
{
    /// <summary>A rate as a percentage with two decimals ("97.12%"), or "–" when there is none.</summary>
    public static string Percent(double? value) => value is { } v ? (v * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%" : "–";

    /// <summary>A confidence interval in percent ("87.10–92.04"), or "–" when there is none.</summary>
    public static string Range(double? low, double? high) =>
        low is { } l && high is { } h ? (l * 100).ToString("0.00", CultureInfo.InvariantCulture) + "–" + (h * 100).ToString("0.00", CultureInfo.InvariantCulture) : "–";

    public static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
