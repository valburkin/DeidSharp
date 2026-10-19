namespace DeidSharp.Evaluation.Cli.Corpora;

/// <summary>
/// A corpus file is not in its expected format, or its gold spans don't line up with its
/// text. Messages name positions (a line number, a query's number), never content: a
/// restricted corpus's text must not reach a console or a log.
/// </summary>
internal sealed class CorpusFormatException(string message) : Exception(message);
