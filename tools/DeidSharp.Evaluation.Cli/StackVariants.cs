using DeidSharp.Detection;

namespace DeidSharp.Evaluation.Cli;

// Design pattern: Table-driven method.
// The pattern: behavior that varies case by case is written as rows of data that one
// piece of code reads, instead of as one branch per case.
// Here: each variant a run can add is one row, its name and how it wraps a stack, so a
// new experiment (another way to search the note again) is a row, never a branch in DetectorStacks.

/// <summary>
/// Changes to try on every stack: with <c>--variants</c>, each stack is also measured
/// wrapped in each variant, side by side with the stack as it is.
/// </summary>
internal static class StackVariants
{
    private static readonly Dictionary<string, (string Label, Func<IPhiDetector, IPhiDetector> Wrap)> Rows = new(StringComparer.Ordinal)
    {
        ["repeated-names"] = ("repeated names", stack => new RepeatedNameDetector(stack)),
    };

    public static IEnumerable<string> Ids => Rows.Keys;

    public static bool Knows(string id) => Rows.ContainsKey(id);

    /// <summary>The stack wrapped in the variant, named after both ("deterministic + clinical + repeated names").</summary>
    public static NamedStack Apply(string id, NamedStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        var (label, wrap) = Rows[id];
        return new NamedStack($"{stack.Name} + {label}", wrap(stack.Detector), stack.Name);
    }
}
