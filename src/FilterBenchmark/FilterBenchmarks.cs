using BenchmarkDotNet.Attributes;

namespace FilterBenchmark;

/// <summary>
/// BenchmarkDotNet suite comparing Bloom, standard bucketed cuckoo, and windowed cuckoo filters.
///
/// What each benchmark measures:
///   Insert  — time to build a fresh filter from scratch (includes random walk cost).
///   PositiveLookup — time to look up keys that ARE in the filter (true positives).
///   NegativeLookup — time to look up keys that are NOT in the filter (true negatives).
///
/// Run with:   dotnet run -c Release
/// </summary>
[MemoryDiagnoser]
[GcServer(true)]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class FilterBenchmarks
{
    // Ops count must be a compile-time constant for OperationsPerInvocation.
    private const int N = 500_000;
    private const int K = 10;

    private ulong[] _insertKeys  = null!;
    private ulong[] _positiveKeys = null!;
    private ulong[] _negativeKeys = null!;

    // Pre-built filters for lookup benchmarks (we don't want to measure build time there).
    private BloomFilter          _bloom    = null!;
    private BucketedCuckooFilter _bucketed = null!;
    private WindowedCuckooFilter _windowed = null!;
    private MortonFilter         _morton   = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        _insertKeys   = GenKeys(rng, N);
        _positiveKeys = _insertKeys[..(N / 2)];
        _negativeKeys = GenKeys(rng, N / 2);

        _bloom    = new BloomFilter(N, K);
        _bucketed = new BucketedCuckooFilter(N, K);
        _windowed = new WindowedCuckooFilter(N, K);
        _morton   = new MortonFilter(N, K);

        foreach (var key in _insertKeys)
        {
            _bloom.Insert(key);
            _bucketed.Insert(key);
            _windowed.Insert(key);
            _morton.Insert(key);
        }
    }

    // ── INSERT ────────────────────────────────────────────────────────────────
    // Each benchmark builds a brand-new filter and inserts all N keys.
    // OperationsPerInvocation tells BDN to divide reported time by N,
    // giving nanoseconds per individual insert operation.

    [Benchmark(Baseline = true, OperationsPerInvoke = N, Description = "Bloom insert")]
    public void Bloom_Insert()
    {
        var f = new BloomFilter(N, K);
        for (int i = 0; i < N; i++) f.Insert(_insertKeys[i]);
    }

    [Benchmark(OperationsPerInvoke = N, Description = "Bucketed (2,4) insert")]
    public void BucketedCuckoo_Insert()
    {
        var f = new BucketedCuckooFilter(N, K);
        for (int i = 0; i < N; i++) f.Insert(_insertKeys[i]);
    }

    [Benchmark(OperationsPerInvoke = N, Description = "Windowed (2,2) insert")]
    public void WindowedCuckoo_Insert()
    {
        var f = new WindowedCuckooFilter(N, K);
        for (int i = 0; i < N; i++) f.Insert(_insertKeys[i]);
    }

    [Benchmark(OperationsPerInvoke = N, Description = "Morton insert")]
    public void Morton_Insert()
    {
        var f = new MortonFilter(N, K);
        for (int i = 0; i < N; i++) f.Insert(_insertKeys[i]);
    }

    // ── POSITIVE LOOKUP ──────────────────────────────────────────────────────
    // Looks up keys that WERE inserted — measures the "true positive" path.

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Bloom positive lookup")]
    public bool Bloom_PositiveLookup()
    {
        bool r = false;
        for (int i = 0; i < _positiveKeys.Length; i++)
            r |= _bloom.Contains(_positiveKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Bucketed (2,4) positive lookup")]
    public bool BucketedCuckoo_PositiveLookup()
    {
        bool r = false;
        for (int i = 0; i < _positiveKeys.Length; i++)
            r |= _bucketed.Contains(_positiveKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Windowed (2,2) positive lookup")]
    public bool WindowedCuckoo_PositiveLookup()
    {
        bool r = false;
        for (int i = 0; i < _positiveKeys.Length; i++)
            r |= _windowed.Contains(_positiveKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Morton positive lookup")]
    public bool Morton_PositiveLookup()
    {
        bool r = false;
        for (int i = 0; i < _positiveKeys.Length; i++)
            r |= _morton.Contains(_positiveKeys[i]);
        return r;
    }

    // ── NEGATIVE LOOKUP ──────────────────────────────────────────────────────
    // Looks up keys that were NOT inserted — the dominant workload in most systems.
    // Bloom filters can early-exit on the first zero bit; cuckoo filters always
    // check exactly 2 buckets/windows regardless.

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Bloom negative lookup")]
    public bool Bloom_NegativeLookup()
    {
        bool r = false;
        for (int i = 0; i < _negativeKeys.Length; i++)
            r |= _bloom.Contains(_negativeKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Bucketed (2,4) negative lookup")]
    public bool BucketedCuckoo_NegativeLookup()
    {
        bool r = false;
        for (int i = 0; i < _negativeKeys.Length; i++)
            r |= _bucketed.Contains(_negativeKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Windowed (2,2) negative lookup")]
    public bool WindowedCuckoo_NegativeLookup()
    {
        bool r = false;
        for (int i = 0; i < _negativeKeys.Length; i++)
            r |= _windowed.Contains(_negativeKeys[i]);
        return r;
    }

    [Benchmark(OperationsPerInvoke = N / 2, Description = "Morton negative lookup")]
    public bool Morton_NegativeLookup()
    {
        bool r = false;
        for (int i = 0; i < _negativeKeys.Length; i++)
            r |= _morton.Contains(_negativeKeys[i]);
        return r;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static ulong[] GenKeys(Random rng, int n)
    {
        var buf  = new byte[8];
        var keys = new ulong[n];
        for (int i = 0; i < n; i++)
        {
            rng.NextBytes(buf);
            keys[i] = BitConverter.ToUInt64(buf);
        }
        return keys;
    }
}
