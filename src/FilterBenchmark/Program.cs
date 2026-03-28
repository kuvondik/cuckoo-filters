using BenchmarkDotNet.Running;
using FilterBenchmark;

// ── Step 1: correctness + space + simple throughput timing ───────────────────
// Runs in both Debug and Release. Produces benchmark_report.html in the
// current directory with 6 Chart.js charts.
// ─────────────────────────────────────────────────────────────────────────────
const int N = 20_000_000;
const int K = 10;

var results = FilterStats.Run(n: N, k: K);
HtmlReporter.Write(results, k: K, path: "benchmark_report.html");

// ── Step 2: BenchmarkDotNet rigorous timing (Release only) ───────────────────
// BDN requires Release mode — Debug JIT skips optimizations, inflating times 5-10×.
// After BDN finishes, its own HTML/markdown report lands in BenchmarkDotNet.Artifacts/.
//
//   dotnet run -c Release
// ─────────────────────────────────────────────────────────────────────────────
#if RELEASE
Console.WriteLine("Running BenchmarkDotNet suite — this takes a few minutes...");
BenchmarkRunner.Run<FilterBenchmarks>();
#else
Console.WriteLine("─────────────────────────────────────────────────────────────────────────");
Console.WriteLine("Tip: run  dotnet run -c Release  for rigorous BenchmarkDotNet timings.");
Console.WriteLine("─────────────────────────────────────────────────────────────────────────");
#endif
