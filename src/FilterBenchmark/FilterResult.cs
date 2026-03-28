namespace FilterBenchmark;

public record FilterResult(
    string Name,
    string ShortName,
    int    Inserted,
    int    Capacity,
    double MeasuredFpr,
    double TargetFpr,
    double BitsPerItem,
    double OverheadC,
    double InsertNsPerOp,
    double PositiveLookupNsPerOp,
    double NegativeLookupNsPerOp
);
