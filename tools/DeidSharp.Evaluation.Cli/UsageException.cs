namespace DeidSharp.Evaluation.Cli;

/// <summary>The command line asks for something the tool can't do. The message says what to change.</summary>
internal sealed class UsageException(string message) : Exception(message);
