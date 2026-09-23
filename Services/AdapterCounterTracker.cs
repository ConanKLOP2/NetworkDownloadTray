namespace NetworkDownloadTray.Services;

// Double-buffered: after warm-up Sample allocates nothing (dictionaries keep their capacity).
public sealed class AdapterCounterTracker
{
    private Dictionary<string, long> _previous = new(), _scratch = new();
    private double? _timestamp;

    public void Reset() { _previous.Clear(); _scratch.Clear(); _timestamp = null; }

    public (double Speed, bool Measuring) Sample(IReadOnlyDictionary<string, long> counters, double timestamp)
    {
        double elapsed = timestamp - (_timestamp ?? timestamp);
        bool inWindow = elapsed > 0 && elapsed <= 10;
        double bytes = 0;
        bool comparable = false;
        // Concrete Dictionary enumerator avoids boxing an IEnumerator on the hot path.
        if (counters is Dictionary<string, long> concrete)
            foreach (var pair in concrete) Accumulate(pair.Key, pair.Value);
        else
            foreach (var pair in counters) Accumulate(pair.Key, pair.Value);
        (_previous, _scratch) = (_scratch, _previous);
        _scratch.Clear();
        _timestamp = timestamp;
        return (comparable ? bytes / elapsed * 8 / 1_000_000 : 0, counters.Count > 0 && !comparable);

        void Accumulate(string key, long value)
        {
            if (inWindow && _previous.TryGetValue(key, out long before) && value >= before)
            {
                bytes += value - before;
                comparable = true;
            }
            _scratch[key] = value;
        }
    }
}
