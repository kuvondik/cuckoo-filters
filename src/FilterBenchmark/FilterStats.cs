using System.Diagnostics;

namespace FilterBenchmark;

public static class FilterStats
{
    private const int WarmupOps = 50_000;
    private const int TimingOps = 500_000;

    public static List<FilterResult> Run(int n = 500_000, int k = 10)
    {
        double targetFpr = Math.Pow(2, -k);

        Console.WriteLine($"─────────────────────────────────────────────────────────────────────");
        Console.WriteLine($"  Filter stats  —  n={n:N0}, k={k}, target FPR = 2^-{k} = {targetFpr*100:F4}%");
        Console.WriteLine($"─────────────────────────────────────────────────────────────────────");
        Console.WriteLine();

        var rng1   = new Random(1);
        var rng2   = new Random(2);
        var insert = GenKeys(rng1, n);
        var query  = GenKeys(rng2, n);

        var configs = new (string Name, string Short, Func<IFilter> Factory)[]
        {
            ("Bloom filter",          "Bloom",    () => new BloomFilter(n, k)),
            ("Bucketed cuckoo (2,4)", "Bucketed", () => new BucketedCuckooFilter(n, k)),
            ("Windowed cuckoo (2,2)", "Windowed", () => new WindowedCuckooFilter(n, k)),
            ("Morton filter",         "Morton",   () => new MortonFilter(n, k)),
        };

        var results = new List<FilterResult>();

        Console.WriteLine(
            $"{"Filter",-28} {"Inserted",9} {"FPR",9} {"Target",9} {"bits/item",10} {"C",7}  " +
            $"{"ins ns/op",10} {"+lookup",10} {"-lookup",10}");
        Console.WriteLine(new string('─', 110));

        foreach (var (name, shortName, factory) in configs)
        {
            var filter   = factory();
            int inserted = 0;
            foreach (var key in insert)
                if (filter.Insert(key)) inserted++;

            int fp = 0;
            foreach (var key in query)
                if (filter.Contains(key)) fp++;

            double measuredFpr  = (double)fp / n;
            double bpi          = filter.BitsPerItem(inserted);
            double c            = bpi / k;

            double insertNs     = MeasureInsert(factory, insert, WarmupOps, TimingOps);
            double posLookupNs  = MeasureLookup(filter, insert[..Math.Min(n, TimingOps)], WarmupOps);
            double negLookupNs  = MeasureLookup(filter, query[..Math.Min(n, TimingOps)], WarmupOps);

            Console.WriteLine(
                $"{name,-28} {inserted,9:N0} {measuredFpr*100,7:F4}%  {targetFpr*100,7:F4}%  " +
                $"{bpi,9:F2}  {c,6:F3}x  " +
                $"{insertNs,9:F1}  {posLookupNs,9:F1}  {negLookupNs,9:F1}");

            results.Add(new FilterResult(
                name, shortName, inserted, n,
                measuredFpr, targetFpr, bpi, c,
                insertNs, posLookupNs, negLookupNs));
        }

        Console.WriteLine();
        Console.WriteLine("ns/op = nanoseconds per operation (lower is better)");
        Console.WriteLine("+lookup = positive (key present)  |  -lookup = negative (key absent)");
        Console.WriteLine();
        return results;
    }

    private static double MeasureInsert(Func<IFilter> factory, ulong[] keys,
                                        int warmup, int ops)
    {
        var w = factory();
        for (int i = 0; i < Math.Min(warmup, keys.Length); i++) w.Insert(keys[i]);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var f  = factory();
        var sw = Stopwatch.StartNew();
        int n  = Math.Min(ops, keys.Length);
        for (int i = 0; i < n; i++) f.Insert(keys[i]);
        sw.Stop();
        return sw.Elapsed.TotalNanoseconds / n;
    }

    private static double MeasureLookup(IFilter filter, ulong[] keys, int warmup)
    {
        bool sink = false;
        for (int i = 0; i < Math.Min(warmup, keys.Length); i++)
            sink |= filter.Contains(keys[i]);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < keys.Length; i++) sink |= filter.Contains(keys[i]);
        sw.Stop();
        _ = sink;
        return sw.Elapsed.TotalNanoseconds / keys.Length;
    }

    private static ulong[] GenKeys(Random rng, int n)
    {
        var buf  = new byte[8];
        var keys = new ulong[n];
        for (int i = 0; i < n; i++)
        { rng.NextBytes(buf); keys[i] = BitConverter.ToUInt64(buf); }
        return keys;
    }
}
