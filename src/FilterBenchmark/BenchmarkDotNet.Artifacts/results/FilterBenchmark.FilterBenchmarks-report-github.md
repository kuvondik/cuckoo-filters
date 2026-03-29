```

BenchmarkDotNet v0.14.0, macOS 26.4 (25E246) [Darwin 25.4.0]
Apple M4 Max, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD
  Job-WOKYJU : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD

Server=True  

```
| Method                           | Mean      | Ratio | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Bloom insert&#39;                   | 12.569 ns |  1.00 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bucketed (2,4) insert&#39;          | 26.636 ns |  2.12 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Windowed (2,2) insert&#39;          | 28.618 ns |  2.28 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Morton insert&#39;                  |  5.070 ns |  0.40 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bloom positive lookup&#39;          | 10.640 ns |  0.85 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) positive lookup&#39; | 11.494 ns |  0.91 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) positive lookup&#39; | 16.038 ns |  1.28 |      - |      - |      - |         - |        0.00 |
| &#39;Morton positive lookup&#39;         | 21.275 ns |  1.69 |      - |      - |      - |         - |        0.00 |
| &#39;Bloom negative lookup&#39;          | 17.884 ns |  1.42 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) negative lookup&#39; |  4.076 ns |  0.32 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) negative lookup&#39; | 13.161 ns |  1.05 |      - |      - |      - |         - |        0.00 |
| &#39;Morton negative lookup&#39;         | 45.734 ns |  3.64 |      - |      - |      - |         - |        0.00 |
