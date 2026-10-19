using DeidSharp.Text;

namespace DeidSharp.Fhir;

// Design pattern: Command.
// The pattern: a request is wrapped in an object (what to do, and with what), so it can
// be queued and carried out later, apart from the code that asked for it.
// Here: each queued text carries its context and the write-back that puts its result in
// place. Rules queue them while the walk runs; the queue carries them all out at the end,
// a few at a time, then writes the results back in the order they were queued.

/// <summary>
/// The free text of one run: queued while the resource is walked, then de-identified
/// together, so the notes of a bundle wait on a detector service a few at a time instead
/// of one after another.
/// </summary>
internal sealed class FreeTextQueue(TextDeidentifier deidentifier, int maxConcurrency)
{
    private readonly List<Entry> _entries = [];
    private bool _started;

    public int Count => _entries.Count;

    /// <exception cref="InvalidOperationException">The queue is already being de-identified.</exception>
    public void Add(string text, TextContext context, Action<string> apply)
    {
        // A text queued from a write-back would never run, and its raw value would stay.
        if (_started)
        {
            throw new InvalidOperationException("Free text cannot be queued once the queued texts are being de-identified.");
        }

        _entries.Add(new Entry(text, context, apply));
    }

    /// <summary>
    /// De-identifies every queued text, up to the concurrency limit at a time, then hands
    /// each result to its write-back in queue order. The first failure stops the texts not
    /// yet started and is thrown; nothing is written back then.
    /// </summary>
    /// <returns>How many spans were replaced, over all texts.</returns>
    public async Task<int> DeidentifyAsync(CancellationToken cancellationToken)
    {
        _started = true;
        var results = new TextDeidentificationResult[_entries.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = cancellationToken };
        await Parallel.ForEachAsync(Enumerable.Range(0, _entries.Count), options, async (i, token) =>
            results[i] = await deidentifier.DeidentifyAsync(_entries[i].Text, _entries[i].Context, token).ConfigureAwait(false))
            .ConfigureAwait(false);

        var findings = 0;
        for (var i = 0; i < results.Length; i++)
        {
            findings += results[i].Findings.Count;
            _entries[i].Apply(results[i].Text);
        }

        return findings;
    }

    // One queued text, who it is about, and where its result goes.
    private sealed record Entry(string Text, TextContext Context, Action<string> Apply);
}
