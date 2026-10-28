using System.Text;

namespace DeidSharp.Evaluation.Cli.Corpora;

/// <summary>
/// Reads comma-separated records as RFC 4180 writes them: a field in double quotes may hold
/// commas, line breaks and doubled quotes. Outside quotes a record ends at a line break, with
/// or without a carriage return.
/// </summary>
internal sealed class CsvRecordReader(TextReader reader)
{
    private const int BufferLength = 1 << 16;

    public IEnumerable<IReadOnlyList<string>> Records()
    {
        var buffer = new char[BufferLength];
        var fields = new List<string>();
        var field = new StringBuilder();
        var state = State.FieldStart;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var c = buffer[i];
                if (state == State.Quoted)
                {
                    if (c == '"')
                    {
                        state = State.QuoteInQuoted;
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                // A doubled quote inside quotes, or the quote that opens a field.
                if (c == '"' && state is State.QuoteInQuoted or State.FieldStart)
                {
                    if (state == State.QuoteInQuoted)
                    {
                        field.Append('"');
                    }

                    state = State.Quoted;
                    continue;
                }

                // Unquoted text, or the character after a closing quote.
                switch (c)
                {
                    case ',':
                        fields.Add(field.ToString());
                        field.Clear();
                        state = State.FieldStart;
                        break;
                    case '\n':
                        fields.Add(field.ToString());
                        field.Clear();
                        yield return fields;
                        fields = [];
                        state = State.FieldStart;
                        break;
                    case '\r':
                        break;
                    default:
                        field.Append(c);
                        state = State.Unquoted;
                        break;
                }
            }
        }

        if (state == State.Quoted)
        {
            throw new CorpusFormatException("The file ends inside a quoted field.");
        }

        if (fields.Count == 0 && field.Length == 0 && state != State.QuoteInQuoted)
        {
            yield break;
        }

        fields.Add(field.ToString());
        yield return fields;
    }

    private enum State
    {
        FieldStart,
        Unquoted,
        Quoted,

        // A quote inside quotes: the field's end, or the first of a doubled quote.
        QuoteInQuoted,
    }
}
