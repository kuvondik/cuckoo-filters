namespace FilterBenchmark;

/// <summary>
/// Standard Bloom filter using the double-hashing trick to simulate k independent
/// hash functions from just two: gi(x) = h1(x) + i*h2(x).
///
/// Space:   ~1.44 * k bits per item (44% overhead over information-theoretic minimum).
/// Lookup:  k cache misses in the worst case (one per hash function).
/// Delete:  not supported — rebuilding required.
///
/// Reference: Bloom 1970; Kirsch &amp; Mitzenmacher 2008 (double-hashing).
/// </summary>
public sealed class BloomFilter : IFilter
{
    private readonly ulong[] _bits;
    private readonly int _hashCount;   // k hash functions
    private readonly long _bitCount;   // m bits in the array

    /// <param name="capacity">Expected number of items.</param>
    /// <param name="kTarget">Target FPR exponent: FPR ≈ 2^-kTarget.</param>
    public BloomFilter(int capacity, int kTarget)
    {
        // Optimal m: bits = k_target * n / ln(2) ≈ 1.44 * k_target * n
        _bitCount = (long)Math.Ceiling(kTarget * capacity / Math.Log(2));

        // Optimal k: hash functions = ln(2) * m/n ≈ 0.693 * kTarget
        _hashCount = Math.Max(1, (int)Math.Round(Math.Log(2) * _bitCount / capacity));

        _bits = new ulong[(_bitCount + 63) / 64];
    }

    // Compute two independent hashes once, then derive all k hashes cheaply.
    private (ulong h1, ulong h2) Hashes(ulong key) =>
        (HashUtils.Mix64(key), HashUtils.Mix64B(key));

    public bool Insert(ulong key)
    {
        var (h1, h2) = Hashes(key);
        for (int i = 0; i < _hashCount; i++)
        {
            long pos = (long)((h1 + (ulong)i * h2) % (ulong)_bitCount);
            _bits[pos >> 6] |= 1UL << (int)(pos & 63);
        }
        return true; // Bloom filters never fail to insert
    }

    public bool Contains(ulong key)
    {
        var (h1, h2) = Hashes(key);
        for (int i = 0; i < _hashCount; i++)
        {
            long pos = (long)((h1 + (ulong)i * h2) % (ulong)_bitCount);
            if ((_bits[pos >> 6] & (1UL << (int)(pos & 63))) == 0)
                return false; // early exit on first zero bit
        }
        return true;
    }

    public double BitsPerItem(int n) => (double)_bitCount / n;
    public long AllocatedBits => (long)_bits.Length * 64;
}
