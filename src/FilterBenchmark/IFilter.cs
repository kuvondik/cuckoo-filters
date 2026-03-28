namespace FilterBenchmark;

public interface IFilter
{
    /// <summary>
    /// Insert a key. Returns false if the filter is full (cuckoo filters only).
    /// Bloom filters always return true.
    /// </summary>
    bool Insert(ulong key);

    /// <summary>
    /// Returns true if the key may be in the set.
    /// No false negatives. Small probability of false positives.
    /// </summary>
    bool Contains(ulong key);

    /// <summary>Theoretical bits per item based on fingerprint size and load factor.</summary>
    double BitsPerItem(int n);

    /// <summary>Total bits allocated in the backing array.</summary>
    long AllocatedBits { get; }
}
