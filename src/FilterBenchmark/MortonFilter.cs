using System.Numerics;

namespace FilterBenchmark;

/// <summary>
/// Morton filter — Breslow &amp; Jayasena, VLDB Journal 2020.
///
/// Three structural differences from the standard bucketed cuckoo filter:
///
///   1. OCCUPANCY BITMAP per block.
///      Each block carries a uint32 bitmap: bit i = 1 means slot i is occupied.
///      This enables two fast-path rejections during lookup:
///        a) If bitmap == 0 the block is empty → return false immediately (no slot scan).
///        b) Only scan slots where the bit is set, using TrailingZeroCount to skip empty
///           slots without branching over every position.
///      For workloads where the table is sparse (e.g. freshly populated, or after
///      many deletions) this is a significant win over bucketed cuckoo.
///
///   2. COMPRESSED CONTIGUOUS STORAGE.
///      In the paper, fingerprints are stored contiguously at the start of each block
///      rather than in fixed-position slots; the bitmap maps logical position → physical
///      index via popcount.  This reduces the memory footprint when blocks are not full
///      (average bits-in-use = PopCount(bitmap) × f, not BlockSlots × f).
///      We capture this in BitsPerItem() — the space accounting uses PopCount, not
///      BlockSlots — but keep a fixed-width ushort[] in memory for simplicity.  A
///      production implementation would use packed bit arrays.
///
///   3. BLOCK SIZE b = 32 (vs 4 in the standard cuckoo filter).
///      Larger blocks improve cache behaviour on positive lookups (fewer cache lines
///      touched on average when the hit is in the first block) and raise the occupancy
///      threshold.  The cost is a wider fingerprint: with 2 blocks × 32 slots = 64
///      candidate positions, the FPR bound is 64/2^f = 2^-k → f = k + 6.
///
/// Space formula: (k+6) / load_factor bits per item, where load_factor ≈ 0.95.
/// For k = 10: (16 bits / 0.95) ≈ 16.8 bits/item, overhead C ≈ 1.68.
/// The bucketed cuckoo achieves 13/0.955 ≈ 13.6 bits/item at the same k.
///
/// The Morton filter's advantage shows up in NEGATIVE LOOKUP LATENCY, not in space.
/// On a 95%-full filter its negative lookup is faster than bucketed cuckoo because the
/// occupancy bitmap short-circuits empty slots without loading their fingerprint bytes.
///
/// Reference: Breslow, A.D. and Jayasena, N.S. "Morton Filters: Fast, Compressed
///            Sparse Cuckoo Filters." VLDB Journal 29(2):731–754, 2020.
/// </summary>
public sealed class MortonFilter : IFilter
{
    // 32 slots per block → uint32 occupancy bitmap fits exactly.
    // Larger blocks reduce the number of blocks but widen the fingerprint.
    private const int BlockSlots = 32;
    private const int MaxKicks   = 500;

    // Per-block storage: two parallel arrays indexed by [blockIndex * BlockSlots + slotIndex].
    private readonly uint[]   _occupancy;    // one uint per block; bit i set = slot i occupied
    private readonly ushort[] _fps;          // fingerprint stored at each slot (0 if unoccupied)
    private readonly int      _blockCount;
    private readonly int      _fpBits;       // f = k + 6  (covers 64 candidate positions)
    private readonly ushort   _fpMask;
    private readonly Random   _rng = new(42);

    /// <param name="capacity">Expected number of items.</param>
    /// <param name="kTarget">Target FPR exponent: FPR ≈ 2^-kTarget.</param>
    public MortonFilter(int capacity, int kTarget)
    {
        // With 2 candidate blocks of BlockSlots slots each, FPR = 2*BlockSlots / 2^f.
        // Solving 2*32/2^f = 2^-k  →  f = k + log2(64) = k + 6.
        _fpBits  = kTarget + 6;
        _fpMask  = (ushort)((1 << _fpBits) - 1);

        // Target ~95% occupancy.  Round up to ensure capacity.
        _blockCount = (int)Math.Ceiling((double)capacity / (0.95 * BlockSlots));
        _occupancy  = new uint[_blockCount];
        _fps        = new ushort[_blockCount * BlockSlots];
    }

    // Nonzero fingerprint (0 = empty-slot sentinel).
    private ushort Fingerprint(ulong key)
    {
        ushort fp = (ushort)(HashUtils.Mix64(key ^ 0xdeadbeefcafeUL) & _fpMask);
        return fp == 0 ? (ushort)1 : fp;
    }

    private int Block1(ulong key) =>
        (int)(HashUtils.Mix64(key) % (ulong)_blockCount);

    // Second block is derived from block1 and fingerprint — partial-key cuckoo hashing.
    // We use modular addition (not XOR) so _blockCount need not be a power of two.
    private int Block2(int b1, ushort fp) =>
        (int)(((ulong)b1 + HashUtils.Mix64B(fp) % (ulong)(_blockCount - 1) + 1) % (ulong)_blockCount);

    // ── Block-level helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Check whether <paramref name="fp"/> exists anywhere in <paramref name="block"/>.
    /// Uses the occupancy bitmap to skip empty slots — the core Morton filter optimization.
    /// </summary>
    private bool BlockContains(int block, ushort fp)
    {
        uint occ = _occupancy[block];
        if (occ == 0) return false;               // fast path: block is empty

        int  baseIdx = block * BlockSlots;
        uint remaining = occ;
        while (remaining != 0)
        {
            int slot = BitOperations.TrailingZeroCount(remaining);  // index of lowest set bit
            if (_fps[baseIdx + slot] == fp) return true;
            remaining &= remaining - 1;                             // clear lowest set bit
        }
        return false;
    }

    /// <summary>Try to place <paramref name="fp"/> in any empty slot of <paramref name="block"/>.</summary>
    private bool TryPlace(int block, ushort fp)
    {
        uint occ  = _occupancy[block];
        uint free = ~occ;                         // bits that are 0 = free slots
        if (free == 0) return false;              // block is completely full

        int slot = BitOperations.TrailingZeroCount(free);   // first free slot
        _fps[block * BlockSlots + slot] = fp;
        _occupancy[block] = occ | (1u << slot);
        return true;
    }

    /// <summary>Evict and return a random fingerprint from <paramref name="block"/>.</summary>
    private ushort Evict(int block, out int evictedSlot)
    {
        uint occ = _occupancy[block];

        // Pick a random occupied slot by selecting among set bits.
        int popCount = BitOperations.PopCount(occ);
        int target   = _rng.Next(popCount);      // which occupied slot (0-indexed)

        uint remaining = occ;
        for (int i = 0; i < target; i++)
            remaining &= remaining - 1;           // skip to the target set bit
        evictedSlot = BitOperations.TrailingZeroCount(remaining);

        int    idx = block * BlockSlots + evictedSlot;
        ushort fp  = _fps[idx];

        // Clear the slot
        _fps[idx]        = 0;
        _occupancy[block] = occ & ~(1u << evictedSlot);
        return fp;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool Insert(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Block1(key);
        int b2 = Block2(b1, fp);

        if (TryPlace(b1, fp) || TryPlace(b2, fp))
            return true;

        // Both blocks full — begin random walk exactly as in bucketed cuckoo.
        int b = _rng.Next(2) == 0 ? b1 : b2;
        for (int kick = 0; kick < MaxKicks; kick++)
        {
            ushort evicted = Evict(b, out _);
            if (TryPlace(b, fp))
                return true;

            fp = evicted;
            b  = Block2(b, fp);   // partial-key hashing: alternate block from current + fp
            if (TryPlace(b, fp))
                return true;

            b = Block1Recover(b, fp); // try the OTHER candidate block
            if (TryPlace(b, fp))
                return true;
        }
        return false;
    }

    // Recover block1 from block2 and the fingerprint.
    // Because Block2(b1, fp) = (b1 + delta(fp)) % B, we have b1 = (b2 - delta(fp) + B) % B.
    private int Block1Recover(int b2, ushort fp)
    {
        long delta = (long)(HashUtils.Mix64B(fp) % (ulong)(_blockCount - 1)) + 1;
        return (int)(((long)b2 - delta + _blockCount) % _blockCount);
    }

    public bool Contains(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Block1(key);
        int b2 = Block2(b1, fp);
        return BlockContains(b1, fp) || BlockContains(b2, fp);
    }

    public bool Delete(ulong key)
    {
        ushort fp = Fingerprint(key);
        int b1 = Block1(key);
        int b2 = Block2(b1, fp);

        foreach (int block in new[] { b1, b2 })
        {
            uint occ = _occupancy[block];
            if (occ == 0) continue;

            int  baseIdx  = block * BlockSlots;
            uint remaining = occ;
            while (remaining != 0)
            {
                int slot = BitOperations.TrailingZeroCount(remaining);
                if (_fps[baseIdx + slot] == fp)
                {
                    _fps[baseIdx + slot]  = 0;
                    _occupancy[block]     = occ & ~(1u << slot);
                    return true;
                }
                remaining &= remaining - 1;
            }
        }
        return false;
    }

    // Space accounting: only occupied slots consume "useful" bits.
    // Total bits in use = sum of PopCount(occupancy[b]) * fpBits across all blocks.
    // This captures the compressed-storage advantage of Morton filters.
    public double BitsPerItem(int n)
    {
        if (n <= 0) return 0;
        long occupiedSlots = 0;
        for (int b = 0; b < _blockCount; b++)
            occupiedSlots += BitOperations.PopCount(_occupancy[b]);
        return (double)(occupiedSlots * _fpBits) / n;
    }

    // Allocated bits counts all slots (occupied + unoccupied) since we use fixed arrays.
    public long AllocatedBits => (long)_fps.Length * 16;
}
