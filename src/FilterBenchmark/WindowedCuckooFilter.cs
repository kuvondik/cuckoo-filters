namespace FilterBenchmark;

/// <summary>
/// Improved (2, 2) windowed cuckoo filter.
///
/// Key ideas vs. the standard (2,4) bucketed filter:
///
///   1. OVERLAPPING WINDOWS instead of disjoint buckets.
///      The hash table has s slots indexed 0..s-1.
///      A window of size l=2 starting at position w covers slots {w, w+1}.
///      Total windows W = s - l + 1. Each slot belongs to up to l windows,
///      which dramatically increases the load threshold.
///
///      Load threshold: 0.965 for (2,2) windows vs. 0.897 for (2,2) buckets.
///      This higher threshold is why we can use smaller window size (l=2 vs. b=4)
///      and still achieve comparable fill rates.
///
///   2. FEWER OVERHEAD BITS per slot: k+2 instead of k+3.
///      Because only 2*l=4 candidate slots exist per key (same as (2,4) bucketed),
///      FPR = 4/2^(k+2) = 2^-k. One fewer overhead bit than the bucketed filter.
///
///   3. NO POWER-OF-2 RESTRICTION on slot count.
///      The alternate window is found via a signed offset (modular addition),
///      not XOR, so any slot count is valid. This avoids up to 2× wasted space.
///
/// Slot layout (k+2 bits packed into a ushort):
///   bits [k-1 : 0]   — k-bit fingerprint
///   bit  [k]          — offset bit: which slot within the window (0 or 1)
///   bit  [k+1]        — choice bit: 0 = first candidate window, 1 = second
///
/// The choice and offset bits serve double duty: they enable displacement without
/// knowing the original key, AND they act as extra "fingerprint bits" to reduce FPR.
///
/// Space:   1.06*(1 + 2/k) per k bits — smaller than bucketed for k ≥ 4.
/// Lookup:  exactly 2 memory accesses (one per candidate window).
/// Delete:  supported.
///
/// Reference: Schmitz, Zentgraf, Rahmann. arXiv:2505.05847, 2025.
/// </summary>
public sealed class WindowedCuckooFilter : IFilter
{
    private const int WindowSize = 2; // l = 2 slots per window
    private const int MaxKicks = 500_000;

    private readonly ushort[] _slots;   // s slots total
    private readonly int _slotCount;    // s
    private readonly int _windowCount;  // W = s - l + 1
    private readonly int _k;
    private readonly ushort _fpMask;
    private readonly Random _rng = new(42);

    /// <param name="capacity">Expected number of items.</param>
    /// <param name="kTarget">Target FPR exponent: FPR ≈ 2^-kTarget.</param>
    public WindowedCuckooFilter(int capacity, int kTarget)
    {
        _k = kTarget;
        _fpMask = (ushort)((1 << kTarget) - 1);

        // Target 91% of the theoretical 0.965 threshold ≈ 0.88 practical fill rate.
        // Extra (WindowSize - 1) slots ensure the last window is always within bounds.
        int s = (int)Math.Ceiling(capacity / 0.88) + WindowSize - 1;
        _slotCount = s;
        _windowCount = s - WindowSize + 1; // W
        _slots = new ushort[s];
    }

    // Nonzero fingerprint (0 = empty slot sentinel).
    private ushort Fingerprint(ulong key)
    {
        ushort fp = (ushort)(HashUtils.Mix64(key ^ 0xc4ceb9fe1a85ec53UL) & _fpMask);
        return fp == 0 ? (ushort)1 : fp;
    }

    // First candidate window index for a key.
    private int Window1(ulong key) =>
        (int)(HashUtils.Mix64(key) % (ulong)_windowCount);

    // Nonzero offset in [1, W-1] derived from the fingerprint.
    // This is the signed difference between the two candidate windows.
    private int Delta(ushort fp) =>
        (int)(HashUtils.Mix64B(fp) % (ulong)(_windowCount - 1)) + 1;

    // Second candidate window: w2 = (w1 + delta) mod W.
    // Reverse: w1 = (w2 - delta + W) mod W  — used during displacement.
    private int Window2(int w1, ushort fp) => (w1 + Delta(fp)) % _windowCount;

    // Pack (choice, offset, fingerprint) into a single ushort slot value.
    private ushort Pack(ushort fp, int choice, int offset) =>
        (ushort)(fp | (offset << _k) | (choice << (_k + 1)));

    // Unpack a slot value into its three fields.
    private void Unpack(ushort slot, out ushort fp, out int choice, out int offset)
    {
        fp     = (ushort)(slot & _fpMask);
        offset = (slot >> _k) & 1;
        choice = (slot >> (_k + 1)) & 1;
    }

    // Try to place fp in any empty slot within the window starting at index w.
    // choice = 0 means w is the key's FIRST candidate window; 1 means it is the SECOND.
    // The stored offset records which physical slot within the window was used.
    private bool TryPlace(int w, ushort fp, int choice)
    {
        for (int off = 0; off < WindowSize; off++)
        {
            if (_slots[w + off] == 0)
            {
                _slots[w + off] = Pack(fp, choice, off);
                return true;
            }
        }
        return false;
    }

    public bool Insert(ulong key)
    {
        ushort fp = Fingerprint(key);
        int w1 = Window1(key);
        int w2 = Window2(w1, fp);

        if (TryPlace(w1, fp, 0) || TryPlace(w2, fp, 1))
            return true;

        // Both windows full — begin random walk.
        int curW = _rng.Next(2) == 0 ? w1 : w2;
        int curChoice = curW == w1 ? 0 : 1;
        ushort curFp = fp;

        for (int kick = 0; kick < MaxKicks; kick++)
        {
            // Evict a random slot from curW and write curFp there.
            int kickOff = _rng.Next(WindowSize);
            int slotIdx = curW + kickOff;
            ushort evicted = _slots[slotIdx];
            _slots[slotIdx] = Pack(curFp, curChoice, kickOff);

            // Recover the evicted fingerprint's window and find its alternate.
            Unpack(evicted, out ushort evFp, out int evChoice, out int evOff);

            // evOff is the offset the evicted fp stored for ITSELF (not kickOff).
            // Subtracting evOff recovers the window start of the evicted fingerprint.
            int evW = slotIdx - evOff;
            int delta = Delta(evFp);

            // Move to the alternate window: reverse the offset direction based on choice bit.
            int altW = evChoice == 0
                ? (evW + delta) % _windowCount           // was in first window → go to second
                : (evW - delta + _windowCount) % _windowCount; // was in second → go back to first
            int newChoice = 1 - evChoice;

            if (TryPlace(altW, evFp, newChoice))
                return true;

            curW = altW;
            curChoice = newChoice;
            curFp = evFp;
        }

        return false; // filter full
    }

    public bool Contains(ulong key)
    {
        ushort fp = Fingerprint(key);
        int w1 = Window1(key);
        int w2 = Window2(w1, fp);

        // Check all 4 candidate slots (2 per window).
        // For each slot, ALL of (fingerprint, choice bit, offset bit) must match —
        // this extra specificity is what keeps FPR at 2^-k despite only 4 checks.
        for (int off = 0; off < WindowSize; off++)
        {
            ushort s1 = _slots[w1 + off];
            if (s1 != 0)
            {
                Unpack(s1, out ushort f1, out int c1, out int o1);
                if (f1 == fp && c1 == 0 && o1 == off) return true;
            }

            ushort s2 = _slots[w2 + off];
            if (s2 != 0)
            {
                Unpack(s2, out ushort f2, out int c2, out int o2);
                if (f2 == fp && c2 == 1 && o2 == off) return true;
            }
        }
        return false;
    }

    public bool Delete(ulong key)
    {
        ushort fp = Fingerprint(key);
        int w1 = Window1(key);
        int w2 = Window2(w1, fp);

        for (int off = 0; off < WindowSize; off++)
        {
            ushort s1 = _slots[w1 + off];
            if (s1 != 0)
            {
                Unpack(s1, out ushort f1, out int c1, out int o1);
                if (f1 == fp && c1 == 0 && o1 == off) { _slots[w1 + off] = 0; return true; }
            }

            ushort s2 = _slots[w2 + off];
            if (s2 != 0)
            {
                Unpack(s2, out ushort f2, out int c2, out int o2);
                if (f2 == fp && c2 == 1 && o2 == off) { _slots[w2 + off] = 0; return true; }
            }
        }
        return false;
    }

    public double BitsPerItem(int n) => (double)_slotCount * (_k + 2) / n;
    public long AllocatedBits => (long)_slotCount * 16;
}
