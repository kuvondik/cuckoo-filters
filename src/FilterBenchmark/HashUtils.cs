namespace FilterBenchmark;

/// <summary>
/// Two independent 64-bit hash mixers based on MurmurHash3 finalizers.
/// Both have excellent avalanche properties — flipping one input bit
/// changes ~32 output bits on average.
/// </summary>
public static class HashUtils
{
    public static ulong Mix64(ulong h)
    {
        h ^= h >> 33;
        h *= 0xff51afd7ed558ccdUL;
        h ^= h >> 33;
        h *= 0xc4ceb9fe1a85ec53UL;
        h ^= h >> 33;
        return h;
    }

    // Second independent mixer — different constants so Mix64(x) and Mix64B(x)
    // are uncorrelated for the same input x.
    public static ulong Mix64B(ulong h)
    {
        h ^= h >> 33;
        h *= 0x9e3779b97f4a7c15UL;
        h ^= h >> 33;
        h *= 0x6c62272e07bb0142UL;
        h ^= h >> 33;
        return h;
    }
}
