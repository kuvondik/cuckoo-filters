```

BenchmarkDotNet v0.14.0, macOS 26.4 (25E246) [Darwin 25.4.0]
Apple M4 Max, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD
  Job-XUHTBM : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD

Server=True  

```
| Method                           | Mean      | Ratio | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Bloom insert&#39;                   | 12.734 ns |  1.00 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bucketed (2,4) insert&#39;          | 27.121 ns |  2.13 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Windowed (2,2) insert&#39;          | 28.974 ns |  2.28 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Morton insert&#39;                  |  5.057 ns |  0.40 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bloom positive lookup&#39;          | 10.675 ns |  0.84 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) positive lookup&#39; | 11.681 ns |  0.92 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) positive lookup&#39; | 16.146 ns |  1.27 |      - |      - |      - |         - |        0.00 |
| &#39;Morton positive lookup&#39;         | 21.352 ns |  1.68 |      - |      - |      - |         - |        0.00 |
| &#39;Bloom negative lookup&#39;          | 17.848 ns |  1.40 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) negative lookup&#39; |  4.046 ns |  0.32 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) negative lookup&#39; | 13.002 ns |  1.02 |      - |      - |      - |         - |        0.00 |
| &#39;Morton negative lookup&#39;         | 45.238 ns |  3.55 |      - |      - |      - |         - |        0.00 |
