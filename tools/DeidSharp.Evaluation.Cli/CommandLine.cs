namespace DeidSharp.Evaluation.Cli;

/// <summary>The <c>--name value</c> pairs of a command line. Every option takes a value; none may repeat.</summary>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _read = new(StringComparer.Ordinal);

    public CommandLine(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Count; i += 2)
        {
            var name = args[i];
            if (!name.StartsWith("--", StringComparison.Ordinal) || name.Length == 2)
            {
                throw new UsageException($"Argument {i + 1} should be an option such as --corpus.");
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException($"{name} needs a value.");
            }

            if (!_values.TryAdd(name[2..], args[i + 1]))
            {
                throw new UsageException($"{name} is given twice.");
            }
        }
    }

    public string? Optional(string name)
    {
        _read.Add(name);
        return _values.GetValueOrDefault(name);
    }

    /// <summary>Every value given, but for the named option's: for checking that two options don't name one file.</summary>
    public IEnumerable<string> Values(string except) => _values.Where(kv => kv.Key != except).Select(kv => kv.Value);

    public string Required(string name, string why) => Optional(name) ?? throw new UsageException($"--{name} is required {why}.");

    /// <summary>Refuses options nobody asked for, so a misspelled one is never silently ignored.</summary>
    public void EnsureAllRead()
    {
        var unknown = _values.Keys.FirstOrDefault(k => !_read.Contains(k));
        if (unknown is not null)
        {
            throw new UsageException($"--{unknown} is not an option here.");
        }
    }
}
