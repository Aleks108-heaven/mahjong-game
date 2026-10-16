using System;
using System.Collections.Generic;

namespace TestMahjongGame.Engine;

// Tile efficiency: which tiles move a hand closer to winning, and how many of them are still out there.
public static class TileEfficiency
{
    // Tiles that lower the shanten of a 13-tile hand (counting melds as three), with how many copies of each
    // this seat cannot see. counts is the concealed hand; visible is TableView.VisibleCounts().
    public static IReadOnlyList<(int Kind, int Left)> ImprovingTiles(
        ShantenCalculator calculator,
        int[] counts,
        int meldCount,
        int[] visible)
    {
        var baseShanten = calculator.CalculateShanten(counts, meldCount);
        var improving = new List<(int Kind, int Left)>();
        foreach (var kind in CandidateKinds(calculator, counts, meldCount, baseShanten))
        {
            var left = 4 - visible[kind];
            if (left <= 0)
            {
                continue;
            }

            counts[kind]++;
            var shanten = calculator.CalculateShanten(counts, meldCount);
            counts[kind]--;
            if (shanten < baseShanten)
            {
                improving.Add((kind, left));
            }
        }

        return improving;
    }

    // Total copies still unseen of the tiles that lower the hand's shanten. More is a faster hand.
    public static int Ukeire(ShantenCalculator calculator, int[] counts, int meldCount, int[] visible)
    {
        var total = 0;
        foreach (var (_, left) in ImprovingTiles(calculator, counts, meldCount, visible))
        {
            total += left;
        }

        return total;
    }

    // The tile kinds that would complete a 13-tile hand, whether or not any copy is left. Empty unless tenpai.
    public static IReadOnlyList<int> WaitKinds(ShantenCalculator calculator, int[] counts, int meldCount)
    {
        var waits = new List<int>();
        if (calculator.CalculateShanten(counts, meldCount) != 0)
        {
            return waits;
        }

        foreach (var kind in CandidateKinds(calculator, counts, meldCount, 0))
        {
            if (counts[kind] >= 4)
            {
                continue;
            }

            counts[kind]++;
            var shanten = calculator.CalculateShanten(counts, meldCount);
            counts[kind]--;
            if (shanten == -1)
            {
                waits.Add(kind);
            }
        }

        return waits;
    }

    // Reference version that tries all 34 kinds. Slower; tests use it to check the pruned search.
    public static int UkeireExhaustive(ShantenCalculator calculator, int[] counts, int meldCount, int[] visible)
    {
        var baseShanten = calculator.CalculateShanten(counts, meldCount);
        var total = 0;
        for (var kind = 0; kind < Tile.TileKindCount; kind++)
        {
            var left = 4 - visible[kind];
            if (left <= 0)
            {
                continue;
            }

            counts[kind]++;
            var shanten = calculator.CalculateShanten(counts, meldCount);
            counts[kind]--;
            if (shanten < baseShanten)
            {
                total += left;
            }
        }

        return total;
    }

    // A tile can only help a standard hand if it joins one already held: same tile, or the same suit within
    // two ranks. Seven pairs and thirteen orphans can also gain from tiles far from the hand, so when either
    // of those is as close as the best shape every kind is tried.
    private static IEnumerable<int> CandidateKinds(ShantenCalculator calculator, int[] counts, int meldCount, int baseShanten)
    {
        var tryAll = meldCount == 0
            && (calculator.CalculateSevenPairsShanten(counts) == baseShanten
                || calculator.CalculateThirteenOrphansShanten(counts) == baseShanten);

        for (var kind = 0; kind < Tile.TileKindCount; kind++)
        {
            if (tryAll || counts[kind] > 0 || HasNeighbour(counts, kind))
            {
                yield return kind;
            }
        }
    }

    private static bool HasNeighbour(int[] counts, int kind)
    {
        if (kind >= 27)
        {
            return false;
        }

        var rank = kind % 9;
        for (var offset = -2; offset <= 2; offset++)
        {
            var other = rank + offset;
            if (offset != 0 && other >= 0 && other <= 8 && counts[kind + offset] > 0)
            {
                return true;
            }
        }

        return false;
    }
}
