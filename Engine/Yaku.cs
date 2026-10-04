using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// Facts about how a hand was won that the tiles alone don't show.
// Seat and round winds are 0-3 for East-North. There are no rounds yet, so the round wind stays East.
public sealed record WinContext(
    bool IsTsumo,
    int SeatWind,
    int RoundWind = 0,
    bool IsHaitei = false,
    bool IsHoutei = false,
    bool IsRinshan = false,
    bool IsChankan = false);

// One scoring pattern. Han is 13 for a yakuman.
public sealed record YakuEntry(string Name, int Han);

public sealed record YakuResult(IReadOnlyList<YakuEntry> Yaku)
{
    public static readonly YakuResult None = new(Array.Empty<YakuEntry>());

    // A hand needs at least one yaku to win. Dora are not yaku.
    public bool HasYaku => Yaku.Count > 0;
    public bool IsYakuman => Yaku.Any(y => y.Han >= 13);
    public int Han => Yaku.Sum(y => y.Han);

    public override string ToString()
    {
        return string.Join(", ", Yaku.Select(y => y.Han >= 13 ? $"{y.Name} (yakuman)" : $"{y.Name} ({y.Han})"));
    }
}

// Finds the yaku of a complete hand. Riichi, ippatsu, double riichi, tenhou and chiihou are not here:
// riichi isn't built yet, and the first-draw yakuman need the engine to track the first go-round.
public static class YakuEvaluator
{
    private enum Kind
    {
        Run,
        Triplet,
        Quad,
        Pair
    }

    private readonly record struct Group(Kind Kind, int Index, bool Open);

    // concealed must include the winning tile (for a ron, add it first). Returns None when the tiles are not a
    // complete hand or when no yaku applies.
    public static YakuResult Evaluate(
        IReadOnlyList<Tile> concealed,
        IReadOnlyList<Meld> melds,
        Tile winningTile,
        WinContext context)
    {
        if (concealed.Count + 3 * melds.Count != 14)
        {
            return YakuResult.None;
        }

        var counts = new int[Tile.TileKindCount];
        foreach (var tile in concealed)
        {
            counts[tile.ToTileIndex()]++;
        }

        var all = concealed.Concat(melds.SelectMany(m => m.Tiles)).Select(t => t.ToTileIndex()).ToList();
        var closed = melds.All(m => !m.IsOpen);
        var winIndex = winningTile.ToTileIndex();

        var meldGroups = melds
            .Select(m => new Group(
                m.Type == MeldType.Chi ? Kind.Run : m.IsKan ? Kind.Quad : Kind.Triplet,
                m.Type == MeldType.Chi ? m.Tiles.Min(t => t.ToTileIndex()) : m.Tiles[0].ToTileIndex(),
                m.IsOpen))
            .ToList();

        var decompositions = new List<List<Group>>();
        Decompose(counts, 0, new List<Group>(), false, 4 - melds.Count, decompositions);

        var kokushi = melds.Count == 0 && IsKokushi(counts);
        var sevenPairs = melds.Count == 0 && IsSevenPairs(counts);
        if (decompositions.Count == 0 && !kokushi && !sevenPairs)
        {
            return YakuResult.None;
        }

        // Yakuman first; they replace every ordinary yaku.
        var yakuman = new List<YakuEntry>();
        void Man(string name) => yakuman.Add(new YakuEntry(name, 13));

        if (kokushi)
        {
            Man("Thirteen orphans");
        }

        if (all.All(i => i >= 27))
        {
            Man("All honours");
        }

        if (all.All(i => i < 27 && (i % 9 == 0 || i % 9 == 8)))
        {
            Man("All terminals");
        }

        if (all.All(IsGreen))
        {
            Man("All green");
        }

        if (melds.Count == 0 && IsNineGates(counts))
        {
            Man("Nine gates");
        }

        foreach (var groups in decompositions)
        {
            var full = groups.Concat(meldGroups).ToList();
            var triplets = full.Where(g => g.Kind is Kind.Triplet or Kind.Quad).ToList();
            var pair = full.First(g => g.Kind == Kind.Pair).Index;

            if (triplets.Count(g => g.Index is >= 31 and <= 33) == 3)
            {
                Man("Big three dragons");
            }

            var windSets = triplets.Count(g => g.Index is >= 27 and <= 30);
            if (windSets == 4)
            {
                Man("Big four winds");
            }
            else if (windSets == 3 && pair is >= 27 and <= 30)
            {
                Man("Little four winds");
            }

            if (full.Count(g => g.Kind == Kind.Quad) == 4)
            {
                Man("Four kans");
            }

            foreach (var winGroup in WinGroupChoices(groups, winIndex))
            {
                var concealedTriplets = full.Count(g =>
                    g.Kind is Kind.Triplet or Kind.Quad && !g.Open && !(g == winGroup && !context.IsTsumo && IsTripletKind(g)));
                if (concealedTriplets == 4)
                {
                    Man("Four concealed triplets");
                }
            }
        }

        if (yakuman.Count > 0)
        {
            return new YakuResult(yakuman.DistinctBy(y => y.Name).ToList());
        }

        // Ordinary yaku: take the reading of the hand that is worth the most han.
        var best = YakuResult.None;
        void Consider(List<YakuEntry> entries)
        {
            if (entries.Sum(e => e.Han) > best.Han)
            {
                best = new YakuResult(entries);
            }
        }

        if (sevenPairs)
        {
            var entries = new List<YakuEntry> { new("Seven pairs", 2) };
            AddTileYaku(entries, all, closed);
            AddContextYaku(entries, closed, context);
            Consider(entries);
        }

        foreach (var groups in decompositions)
        {
            var full = groups.Concat(meldGroups).ToList();
            foreach (var winGroup in WinGroupChoices(groups, winIndex))
            {
                var entries = new List<YakuEntry>();
                AddTileYaku(entries, all, closed);
                AddContextYaku(entries, closed, context);
                AddGroupYaku(entries, full, winGroup, winIndex, closed, context);
                Consider(entries);
            }
        }

        return best;
    }

    // Every concealed group the winning tile could have completed (a run, a triplet or the pair).
    private static IEnumerable<Group> WinGroupChoices(List<Group> groups, int winIndex)
    {
        var found = false;
        foreach (var group in groups)
        {
            var holds = group.Kind == Kind.Run
                ? winIndex >= group.Index && winIndex <= group.Index + 2
                : winIndex == group.Index;
            if (holds)
            {
                found = true;
                yield return group;
            }
        }

        if (!found)
        {
            // Defensive: the winning tile should always be in the concealed tiles.
            yield return groups[0];
        }
    }

    private static bool IsTripletKind(Group group) => group.Kind is Kind.Triplet or Kind.Quad;

    private static void AddContextYaku(List<YakuEntry> entries, bool closed, WinContext context)
    {
        if (closed && context.IsTsumo)
        {
            entries.Add(new YakuEntry("Self-draw (menzen tsumo)", 1));
        }

        if (context.IsHaitei)
        {
            entries.Add(new YakuEntry("Last tile draw (haitei)", 1));
        }

        if (context.IsHoutei)
        {
            entries.Add(new YakuEntry("Last discard (houtei)", 1));
        }

        if (context.IsRinshan)
        {
            entries.Add(new YakuEntry("After a kan (rinshan)", 1));
        }

        if (context.IsChankan)
        {
            entries.Add(new YakuEntry("Robbing a kan (chankan)", 1));
        }
    }

    // Yaku that depend only on which tiles are in the hand, so they also fit seven pairs.
    private static void AddTileYaku(List<YakuEntry> entries, List<int> all, bool closed)
    {
        if (all.All(i => i < 27 && i % 9 != 0 && i % 9 != 8))
        {
            entries.Add(new YakuEntry("All simples (tanyao)", 1));
        }

        if (all.All(i => i >= 27 || i % 9 == 0 || i % 9 == 8) && all.Any(i => i >= 27) && all.Any(i => i < 27))
        {
            entries.Add(new YakuEntry("All terminals and honours", 2));
        }

        var suits = all.Where(i => i < 27).Select(i => i / 9).Distinct().ToList();
        if (suits.Count == 1)
        {
            var hasHonours = all.Any(i => i >= 27);
            entries.Add(hasHonours
                ? new YakuEntry("Half flush (honitsu)", closed ? 3 : 2)
                : new YakuEntry("Full flush (chinitsu)", closed ? 6 : 5));
        }
    }

    private static void AddGroupYaku(
        List<YakuEntry> entries,
        List<Group> full,
        Group winGroup,
        int winIndex,
        bool closed,
        WinContext context)
    {
        var runs = full.Where(g => g.Kind == Kind.Run).ToList();
        var triplets = full.Where(g => g.Kind is Kind.Triplet or Kind.Quad).ToList();
        var pair = full.First(g => g.Kind == Kind.Pair);

        // Value tiles: dragons, your own wind and the round wind.
        foreach (var set in triplets)
        {
            if (set.Index >= 31)
            {
                entries.Add(new YakuEntry($"Dragon triplet ({Tile.FromTileIndex(set.Index)})", 1));
            }

            if (set.Index == 27 + context.SeatWind)
            {
                entries.Add(new YakuEntry("Seat wind", 1));
            }

            if (set.Index == 27 + context.RoundWind)
            {
                entries.Add(new YakuEntry("Round wind", 1));
            }
        }

        if (closed && runs.Count == 4 && !IsValuePair(pair.Index, context) && IsTwoSidedWait(winGroup, winIndex))
        {
            entries.Add(new YakuEntry("No-points hand (pinfu)", 1));
        }

        if (closed)
        {
            var pairsOfRuns = runs.GroupBy(r => r.Index).Sum(g => g.Count() / 2);
            if (pairsOfRuns >= 2)
            {
                entries.Add(new YakuEntry("Twice the same run (ryanpeikou)", 3));
            }
            else if (pairsOfRuns == 1)
            {
                entries.Add(new YakuEntry("Same run twice (iipeikou)", 1));
            }
        }

        var bonus = closed ? 0 : 1; // several yaku lose one han in an open hand

        for (var start = 0; start < 7; start++)
        {
            if (runs.Any(r => r.Index == start) && runs.Any(r => r.Index == start + 9) && runs.Any(r => r.Index == start + 18))
            {
                entries.Add(new YakuEntry("Same run in three suits (sanshoku)", 2 - bonus));
                break;
            }
        }

        for (var suit = 0; suit < 3; suit++)
        {
            if (new[] { 0, 3, 6 }.All(offset => runs.Any(r => r.Index == suit * 9 + offset)))
            {
                entries.Add(new YakuEntry("Straight (ittsu)", 2 - bonus));
                break;
            }
        }

        if (runs.Count > 0)
        {
            var everyGroupHasEdge = full.All(g => g.Kind == Kind.Run
                ? g.Index % 9 == 0 || g.Index % 9 == 6
                : g.Index >= 27 || g.Index % 9 == 0 || g.Index % 9 == 8);
            if (everyGroupHasEdge)
            {
                var hasHonour = full.Any(g => g.Index >= 27);
                entries.Add(hasHonour
                    ? new YakuEntry("Terminal or honour in every set (chanta)", 2 - bonus)
                    : new YakuEntry("Terminal in every set (junchan)", 3 - bonus));
            }
        }

        if (runs.Count == 0)
        {
            entries.Add(new YakuEntry("All triplets (toitoi)", 2));
        }

        var concealedTriplets = triplets.Count(g => !g.Open && !(g == winGroup && !context.IsTsumo));
        if (concealedTriplets == 3)
        {
            entries.Add(new YakuEntry("Three concealed triplets (sanankou)", 2));
        }

        for (var rank = 0; rank < 9; rank++)
        {
            if (triplets.Any(t => t.Index == rank) && triplets.Any(t => t.Index == rank + 9) && triplets.Any(t => t.Index == rank + 18))
            {
                entries.Add(new YakuEntry("Same triplet in three suits (sanshoku doukou)", 2));
                break;
            }
        }

        if (full.Count(g => g.Kind == Kind.Quad) == 3)
        {
            entries.Add(new YakuEntry("Three kans (sankantsu)", 2));
        }

        if (triplets.Count(g => g.Index >= 31) == 2 && pair.Index >= 31)
        {
            entries.Add(new YakuEntry("Little three dragons (shousangen)", 2));
        }
    }

    private static bool IsValuePair(int index, WinContext context)
    {
        return index >= 31 || index == 27 + context.SeatWind || index == 27 + context.RoundWind;
    }

    // Pinfu needs the winning tile to complete a run from either end of a two-sided wait (not 1-2 or 8-9 edge waits,
    // not the middle).
    private static bool IsTwoSidedWait(Group winGroup, int winIndex)
    {
        if (winGroup.Kind != Kind.Run)
        {
            return false;
        }

        var start = winGroup.Index % 9; // 0-based rank of the lowest tile
        if (winIndex == winGroup.Index)
        {
            return start != 6; // 7-8-9 completed by the 7 was waiting on 8-9
        }

        if (winIndex == winGroup.Index + 2)
        {
            return start != 0; // 1-2-3 completed by the 3 was waiting on 1-2
        }

        return false;
    }

    private static bool IsGreen(int index)
    {
        // 2s 3s 4s 6s 8s and the green dragon.
        return index is 19 or 20 or 21 or 23 or 25 or 32;
    }

    private static bool IsSevenPairs(int[] counts)
    {
        return counts.Count(c => c == 2) == 7;
    }

    private static bool IsKokushi(int[] counts)
    {
        var kinds = 0;
        var hasPair = false;
        for (var i = 0; i < Tile.TileKindCount; i++)
        {
            var isOrphan = i >= 27 || i % 9 == 0 || i % 9 == 8;
            if (!isOrphan)
            {
                if (counts[i] > 0)
                {
                    return false;
                }

                continue;
            }

            if (counts[i] == 0)
            {
                return false;
            }

            kinds++;
            hasPair |= counts[i] == 2;
        }

        return kinds == 13 && hasPair;
    }

    private static bool IsNineGates(int[] counts)
    {
        for (var suit = 0; suit < 3; suit++)
        {
            var total = 0;
            var ok = true;
            for (var rank = 0; rank < 9; rank++)
            {
                var n = counts[suit * 9 + rank];
                total += n;
                var need = rank is 0 or 8 ? 3 : 1;
                ok &= n >= need;
            }

            if (ok && total == 14)
            {
                return true;
            }
        }

        return false;
    }

    // Splits the concealed tiles into the given number of sets plus one pair, in every possible way.
    private static void Decompose(
        int[] counts,
        int from,
        List<Group> current,
        bool hasPair,
        int setsLeft,
        List<List<Group>> output)
    {
        var i = from;
        while (i < Tile.TileKindCount && counts[i] == 0)
        {
            i++;
        }

        if (i == Tile.TileKindCount)
        {
            if (hasPair && setsLeft == 0)
            {
                output.Add(new List<Group>(current));
            }

            return;
        }

        if (!hasPair && counts[i] >= 2)
        {
            counts[i] -= 2;
            current.Add(new Group(Kind.Pair, i, false));
            Decompose(counts, i, current, true, setsLeft, output);
            current.RemoveAt(current.Count - 1);
            counts[i] += 2;
        }

        if (setsLeft > 0 && counts[i] >= 3)
        {
            counts[i] -= 3;
            current.Add(new Group(Kind.Triplet, i, false));
            Decompose(counts, i, current, hasPair, setsLeft - 1, output);
            current.RemoveAt(current.Count - 1);
            counts[i] += 3;
        }

        if (setsLeft > 0 && i < 27 && i % 9 <= 6 && counts[i + 1] > 0 && counts[i + 2] > 0)
        {
            counts[i]--;
            counts[i + 1]--;
            counts[i + 2]--;
            current.Add(new Group(Kind.Run, i, false));
            Decompose(counts, i, current, hasPair, setsLeft - 1, output);
            current.RemoveAt(current.Count - 1);
            counts[i]++;
            counts[i + 1]++;
            counts[i + 2]++;
        }
    }
}
