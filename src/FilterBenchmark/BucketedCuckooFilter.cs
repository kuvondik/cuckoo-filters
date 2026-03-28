namespace FilterBenchmark;

/// <summary>
/// Standard (2, 4) bucketed cuckoo filter using partial-key cuckoo hashing.
///
/// Design:
///   - B buckets (power of 2), each holding 4 fingerprint slots.
///   - Fingerprint size: f = k+3 bits, because 2 buckets × 4 slots = 8 candidate
///     locations per key, and FPR = 8/2^f = 2^-k requires f = k+3.
///   - Partial-key hashing: alternate bucket = current XOR hash(fingerprint),
///     so fingerprints can be relocated without knowing the original key.
///   - Load factor: ~95.5% with b=4 (Fan et al. Table 2, empirical).
///   - Bucket count must be a power of 2 for the XOR trick to produce valid indices.
///
/// Space:   ~1.05*(1 + 3/k) per k bits — better than Bloom for ε &lt; 3%.
/// Lookup:  exactly 2 memory accesses (one per candidate bucket).
/// Delete:  supported (remove matching fingerprint from either bucket).
///
/// Reference: Fan, Andersen, Kaminsky, Mitzenmacher. CoNEXT 2014.
/// </summary>
public sealed class BucketedCuckooFilter : IFilter
{
    private const int SlotsPerBucket = 4;
    private const int MaxKicks = 50000;

    private readonly ushort[] _table;      // [B * 4] — one ushort per slot
    private readonly int _bucketCount;     // B, always a power of 2
    private readonly ulong _bucketMask;    // B - 1, for fast modulo via AND
    private readonly int _fpBits;          // f = k + 3
    private readonly ushort _fpMask;
    private readonly Random _rng = new(42);

    /// <param name="capacity">Expected number of items.</param>
    /// <param name="kTarget">Target FPR exponent: FPR ≈ 2^-kTarget.</param>
    public BucketedCuckooFilter(int capacity, int kTarget)
    {
        _fpBits = kTarget + 3;
        _fpMask = (ushort)((1 << _fpBits) - 1);

        // Size for ~95.5% load at 4 slots/bucket, rounded up to next power of 2.
        // Note: rounding to power-of-2 can add up to 2× extra space in the worst case —
        // this is a known limitation of the XOR-based alternate bucket scheme.
        int minBuckets = (int)Math.Ceiling(capacity / (0.955 * SlotsPerBucket));
        _bucketCount = NextPow2(minBuckets);
        _bucketMask = (ulong)(_bucketCount - 1);
        _table = new ushort[_bucketCount * SlotsPerBucket];
    }

    private static int NextPow2(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    // Nonzero fingerprint — zero is reserved as the "empty slot" sentinel.
    private ushort Fingerprint(ulong key)
    {
        ushort fp = (ushort)(HashUtils.Mix64(key ^ 0xc4ceb9fe1a85ec53UL) & _fpMask);
        return fp == 0 ? (ushort)1 : fp;
    }

    private int Bucket1(ulong key) =>
        (int)(HashUtils.Mix64(key) & _bucketMask);

    // Partial-key cuckoo hashing: alt = current XOR hash(fingerprint).
    // This formula is SYMMETRIC: applying it twice returns to the original bucket.
    // Therefore, during displacement we always compute the same formula regardless
    // of which of the two buckets the fingerprint currently occupies.
    private int AlternateBucket(int b, ushort fp) =>
        (int)((ulong)b ^ (HashUtils.Mix64(fp) & _bucketMask));

    private bool TryAdd(int bucket, ushort fp)
    {
        int start = bucket * SlotsPerBucket;
        for (int i = 0; i < SlotsPerBucket; i++)
        {
            if (_table[start + i] == 0)
            {
                _table[start + i] = fp;
                return true;
            }
        }
        return false;
    }

    private bool BucketContains(int bucket, ushort fp)
    {
        int start = bucket * SlotsPerBucket;
        for (int i = 0; i < SlotsPerBucket; i++)
            if (_table[start + i] == fp) return true;
        return false;
    }

    public bool Insert(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Bucket1(key);
        int b2 = AlternateBucket(b1, fp);

        if (TryAdd(b1, fp) || TryAdd(b2, fp))
            return true;

        // Both buckets full — begin the random walk (cuckoo displacement chain).
        int b = _rng.Next(2) == 0 ? b1 : b2;
        for (int kick = 0; kick < MaxKicks; kick++)
        {
            // Swap fp with a random existing entry in bucket b.
            int slotIdx = b * SlotsPerBucket + _rng.Next(SlotsPerBucket);
            (fp, _table[slotIdx]) = (_table[slotIdx], fp);

            // The displaced fp must now try its alternate bucket.
            b = AlternateBucket(b, fp);
            if (TryAdd(b, fp))
                return true;
        }

        return false; // filter full; caller should resize or report failure
    }

    public bool Contains(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Bucket1(key);
        int b2 = AlternateBucket(b1, fp);
        return BucketContains(b1, fp) || BucketContains(b2, fp);
    }

    public bool Delete(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Bucket1(key);
        int b2 = AlternateBucket(b1, fp);

        foreach (int bucket in new[] { b1, b2 })
        {
            int start = bucket * SlotsPerBucket;
            for (int i = 0; i < SlotsPerBucket; i++)
            {
                if (_table[start + i] == fp)
                {
                    _table[start + i] = 0;
                    return true;
                }
            }
        }
        return false;
    }

    // Theoretical bits/item = fingerprint_bits / load_factor
    public double BitsPerItem(int n) => (double)_bucketCount * SlotsPerBucket * _fpBits / n;
    public long AllocatedBits => (long)_table.Length * 16; // ushort = 16 bits stored
}
