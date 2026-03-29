```

BenchmarkDotNet v0.14.0, macOS 26.4 (25E246) [Darwin 25.4.0]
Apple M4 Max, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.101
  [Host]     : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD
  Job-GZWWHV : .NET 10.0.1 (10.0.125.57005), Arm64 RyuJIT AdvSIMD

Server=True  

```
| Method                           | Mean      | Ratio | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|--------------------------------- |----------:|------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Bloom insert&#39;                   | 12.773 ns |  1.00 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bucketed (2,4) insert&#39;          | 27.684 ns |  2.17 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Windowed (2,2) insert&#39;          | 29.405 ns |  2.30 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Morton insert&#39;                  |  5.239 ns |  0.41 | 0.0000 | 0.0000 | 0.0000 |       2 B |        1.00 |
| &#39;Bloom positive lookup&#39;          | 10.614 ns |  0.83 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) positive lookup&#39; | 12.092 ns |  0.95 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) positive lookup&#39; | 16.236 ns |  1.27 |      - |      - |      - |         - |        0.00 |
| &#39;Morton positive lookup&#39;         | 21.535 ns |  1.69 |      - |      - |      - |         - |        0.00 |
| &#39;Bloom negative lookup&#39;          | 18.363 ns |  1.44 |      - |      - |      - |         - |        0.00 |
| &#39;Bucketed (2,4) negative lookup&#39; |  4.128 ns |  0.32 |      - |      - |      - |         - |        0.00 |
| &#39;Windowed (2,2) negative lookup&#39; | 13.141 ns |  1.03 |      - |      - |      - |         - |        0.00 |
| &#39;Morton negative lookup&#39;         | 46.123 ns |  3.61 |      - |      - |      - |         - |        0.00 |
