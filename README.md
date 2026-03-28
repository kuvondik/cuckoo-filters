# Probabilistic Filter Benchmark

Comparative benchmark of four approximate set membership filters in C#.
Measures space efficiency (bits per item, overhead factor C), throughput (ns per operation),
and false positive rate across Bloom, bucketed cuckoo, windowed cuckoo, and Morton filters.

## Filters implemented

| Filter | Reference | Fingerprint | Overhead C |
|---|---|---|---|
| Bloom | Bloom 1970 | — | 1.443 (fixed) |
| Bucketed cuckoo (2,4) | Fan et al. CoNEXT 2014 | k+3 bits | 1.05(1+3/k) |
| Windowed cuckoo (2,2) | Schmitz et al. arXiv 2025 | k+2 bits | 1.06(1+2/k) |
| Morton | Breslow & Jayasena VLDB 2020 | k+6 bits | (1+6/k)/0.95 |

**Overhead C** = actual bits per item / k, where k bits per item is the
information-theoretic minimum for FPR = 2^-k. Lower is better; 1.0 is perfect.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- No other dependencies — BenchmarkDotNet is restored automatically via NuGet

## Quick start

```bash
git clone <repo>
cd FilterBenchmark
dotnet restore
```

**Step 1 — correctness and space check** (debug mode, ~4 minutes at N=20M)

```bash
dotnet run
```

Prints a stats table to the console and writes `benchmark_report.html` to the
current directory. Open the HTML file in any browser to view the charts.

**Step 2 — rigorous throughput benchmarks** (release mode, ~40 minutes at N=20M)

```bash
dotnet run -c Release
```

Runs BenchmarkDotNet with proper warmup, GC stabilisation, and statistical
analysis. Results are saved to `BenchmarkDotNet.Artifacts/` as HTML, markdown,
and CSV. Also regenerates `benchmark_report.html` with more accurate timings.

## Parameters

Edit the top of `Program.cs` and `FilterBenchmarks.cs` to change:

```csharp
const int N = 20_000_000;  // number of keys
const int K = 10;          // FPR target = 2^-K ≈ 0.098%
```

At N=20M all filters are deep in DRAM — far beyond typical L3 cache size (~20MB),
so memory latency dominates every measurement. This is the regime where structural
differences between filters produce the most meaningful timing differences.
Approximate memory use at N=20M, K=10:

| Component | Memory |
|---|---|
| Bloom filter | ~72 MB |
| Bucketed cuckoo | ~68 MB |
| Windowed cuckoo | ~68 MB |
| Morton filter | ~84 MB |
| Key arrays (insert + query) | ~320 MB |
| Total | ~612 MB |

## Output

**Console** — stats table with columns: filter name, keys inserted, measured FPR,
target FPR, bits per item, overhead C, insert ns/op, positive lookup ns/op,
negative lookup ns/op.

**benchmark_report.html** — self-contained HTML file with seven Chart.js charts:
1. Bits per item (measured)
2. Overhead factor C
3. FPR measured vs target
4. Insert throughput
5. Negative lookup latency — Morton's occupancy bitmap advantage
6. Positive vs negative lookup side-by-side
7. Theoretical C across k values (all four formulas as lines)

No server required — open directly in any browser.

## Project structure

```
FilterBenchmark/
├── Program.cs                  entry point — runs stats then optionally BDN
├── FilterBenchmarks.cs         BenchmarkDotNet benchmark class
├── FilterStats.cs              correctness + space + simple timing
├── FilterResult.cs             data model passed from stats to HTML reporter
├── HtmlReporter.cs             generates benchmark_report.html
├── IFilter.cs                  common interface (Insert, Contains, BitsPerItem)
├── HashUtils.cs                two independent MurmurHash3 finalizers
├── BloomFilter.cs              standard Bloom filter
├── BucketedCuckooFilter.cs     Fan et al. 2014 — (2,4) bucketed cuckoo
├── WindowedCuckooFilter.cs     Schmitz et al. 2025 — (2,2) windowed cuckoo
├── MortonFilter.cs             Breslow & Jayasena 2020 — occupancy bitmap
└── FilterBenchmark.csproj      .NET 8 project file
```

## Key design decisions

**Partial-key cuckoo hashing** — fingerprints can be relocated without storing
the original key. The alternate bucket/window is computed from the current
location and the fingerprint itself. See Fan et al. 2014 Section 3.1.

**Windowed choice and offset bits** — each slot in the windowed filter stores
k fingerprint bits + 1 choice bit (which of the two candidate windows) + 1 offset
bit (which slot within the window). These bits serve double duty: they enable
displacement without the original key AND act as extra fingerprint bits keeping
FPR at 2^-k. See Schmitz et al. 2025 Section 3.

**Morton occupancy bitmap** — each 32-slot block carries a uint32 bitmap.
A lookup on an empty block short-circuits with a single comparison (`bitmap == 0`)
before touching any fingerprint data. Uses `BitOperations.TrailingZeroCount`
to scan only occupied slots. See Breslow & Jayasena 2020.

**FPR measurement** — insert keys and query keys are generated with different
RNG seeds, guaranteeing they are non-overlapping. False positive rate =
wrong answers / total queries on absent keys.

## References

- Bloom, B.H. (1970). Space/time trade-offs in hash coding with allowable errors. *CACM*.
- Fan, B., Andersen, D.G., Kaminsky, M., Mitzenmacher, M. (2014). Cuckoo filter: Practically better than Bloom. *CoNEXT*.
- Eppstein, D. (2016). Cuckoo filter: Simplification and analysis. *SWAT*. arXiv:1604.06067.
- Breslow, A.D., Jayasena, N.S. (2020). Morton filters: Fast, compressed sparse cuckoo filters. *VLDB Journal*.
- Schmitz, J.E., Zentgraf, J., Rahmann, S. (2025). Smaller and more flexible cuckoo filters. arXiv:2505.05847.
