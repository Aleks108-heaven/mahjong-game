using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// A strong club player. It uses only what is on the table (its own hand, rivers, melds, dora indicators):
//  - Attack: among the discards that keep the lowest shanten it keeps the hand that has the most unseen tiles
//    to improve with. Once tenpai, it counts only the waits that would really win (a yaku is needed, and a
//    wait it has already thrown is furiten, so only tsumo counts).
//  - Defence: when an opponent has opened three sets (or two, late), a hand that is still far from winning
//    stops attacking and throws the safest tile: one that opponent already discarded can never be ron'd
//    (furiten), then honours and terminals, then suji tiles.
//  - Calls: the same sound rules as the Middle bot, but never while folding, never an open kan, and a
//    chi picks the run that costs the least shape.
public sealed class SeniorBrain : IBotBrain
{
    private readonly ShantenCalculator _calculator = new();
    private readonly MiddleBrain _middle = new();

    public BotLevel Level => BotLevel.Senior;

    public Tile ChooseDiscard(TableView view, Random rng)
    {
        var hand = view.Hand;
        var counts = hand.TileCounts();
        var meldCount = hand.MeldCount;
        var visible = view.VisibleCounts();

        var kinds = DiscardableKinds(view, counts);
        var shantenAfter = new Dictionary<int, int>();
        foreach (var kind in kinds)
        {
            counts[kind]--;
            shantenAfter[kind] = _calculator.CalculateShanten(counts, meldCount);
            counts[kind]++;
        }

        var best = shantenAfter.Values.Min();
        var threats = Threats(view);
        var fold = ShouldFold(threats, best);
        var scored = Score(view, counts, meldCount, visible, threats, shantenAfter, kinds.Where(k => fold || shantenAfter[k] == best), fold);

        // A tenpai hand that can never win (open, no yaku in any wait) is worth less than stepping back.
        if (!fold && best == 0 && scored.All(s => s.Efficiency == 0))
        {
            var stepBack = kinds.Where(k => shantenAfter[k] == 1).ToList();
            if (stepBack.Count > 0)
            {
                scored = Score(view, counts, meldCount, visible, threats, shantenAfter, stepBack, fold: false);
            }
        }

        var top = scored.Min(s => s.Key);
        var ties = scored.Where(s => s.Key == top).Select(s => s.Kind).ToList();
        var chosen = ties[rng.Next(ties.Count)];
        return hand.Tiles.First(t => t.ToTileIndex() == chosen);
    }

    public KanOption? ChooseSelfKan(TableView view)
    {
        if (!view.CanKan)
        {
            return null;
        }

        var hand = view.Hand;
        var current = hand.BestDiscardShanten();
        var threatened = Threats(view).Count > 0;
        foreach (var option in hand.KanOptions())
        {
            // An added kan can be robbed by whoever waits on that tile, so only make one when nobody looks dangerous.
            var allowed = option.Type == MeldType.Ankan || !threatened;
            if (allowed && hand.ShantenAfterKan(option) <= current)
            {
                return option;
            }
        }

        return null;
    }

    // An open kan shows the hand and gives away a dora indicator for little gain.
    public bool WantsOpenKan(TableView view, Tile discard)
    {
        return false;
    }

    public bool WantsPon(TableView view, Tile discard)
    {
        return !IsFolding(view) && _middle.WantsPon(view, discard);
    }

    public ChiOption? ChooseChi(TableView view, Tile discard, IReadOnlyList<ChiOption> options)
    {
        if (IsFolding(view))
        {
            return null;
        }

        var hand = view.Hand;
        var best = options
            .OrderBy(option => hand.ShantenAfterChi(discard, option))
            .ThenBy(option => EdgeTiles(discard, option))
            .ThenBy(option => DistanceFromCentre(discard, option))
            .First();

        var run = new[] { discard, best.A, best.B }.OrderBy(t => t).ToList();
        var keepsYaku = hand.OpenCallKeepsAYaku(run, new[] { best.A, best.B }, false, view.Seat, view.RoundWind);
        return hand.ShantenAfterChi(discard, best) < hand.GetShanten() && keepsYaku ? best : null;
    }

    private bool IsFolding(TableView view)
    {
        return ShouldFold(Threats(view), view.Hand.GetShanten());
    }

    private static List<int> DiscardableKinds(TableView view, int[] counts)
    {
        var barred = view.Forbidden.Select(t => t.ToTileIndex()).ToHashSet();
        var kinds = Enumerable.Range(0, Tile.TileKindCount).Where(k => counts[k] > 0 && !barred.Contains(k)).ToList();

        // The swap-calling rule never leaves a hand with nothing to throw.
        return kinds.Count > 0 ? kinds : Enumerable.Range(0, Tile.TileKindCount).Where(k => counts[k] > 0).ToList();
    }

    private sealed record Candidate(int Kind, int Efficiency, (int, int, int, int) Key);

    private List<Candidate> Score(
        TableView view,
        int[] counts,
        int meldCount,
        int[] visible,
        IReadOnlyList<Threat> threats,
        Dictionary<int, int> shantenAfter,
        IEnumerable<int> pool,
        bool fold)
    {
        var scored = new List<Candidate>();
        foreach (var kind in pool)
        {
            counts[kind]--;
            var efficiency = shantenAfter[kind] > 0
                ? TileEfficiency.Ukeire(_calculator, counts, meldCount, visible)
                : WinValue(view, counts, meldCount, visible, kind);
            counts[kind]++;

            var danger = (int)Math.Round(2 * Danger(threats, visible, view.RoundWind, kind));
            var keep = KeepValue(view, counts, kind);

            // Smaller sorts first. Folding puts safety above speed; attacking only uses danger to break ties.
            var key = fold
                ? (danger, shantenAfter[kind], -efficiency, keep)
                : (0, -efficiency, danger, keep);
            scored.Add(new Candidate(kind, efficiency, key));
        }

        return scored;
    }

    // For a tenpai hand after discarding discardedKind: unseen winning tiles, weighted by how they can win.
    // A ron needs a yaku and no furiten; a tsumo needs a yaku too (a closed hand always has one).
    private int WinValue(TableView view, int[] counts, int meldCount, int[] visible, int discardedKind)
    {
        var waits = TileEfficiency.WaitKinds(_calculator, counts, meldCount);
        if (waits.Count == 0)
        {
            return 0;
        }

        var thrown = view.Seats[view.Seat].Thrown;
        var furiten = waits.Any(w => w == discardedKind || thrown.Any(t => t.ToTileIndex() == w));
        var concealed = new List<Tile>();
        for (var kind = 0; kind < counts.Length; kind++)
        {
            for (var i = 0; i < counts[kind]; i++)
            {
                concealed.Add(Tile.FromTileIndex(kind));
            }
        }

        var melds = view.Hand.Melds;
        var total = 0;
        foreach (var kind in waits)
        {
            var left = 4 - visible[kind];
            if (left <= 0)
            {
                continue;
            }

            var tile = Tile.FromTileIndex(kind);
            var all = new List<Tile>(concealed) { tile };
            var tsumo = YakuEvaluator.Evaluate(all, melds, tile, new WinContext(true, view.Seat, view.RoundWind)).HasYaku;
            var ron = !furiten && YakuEvaluator.Evaluate(all, melds, tile, new WinContext(false, view.Seat, view.RoundWind)).HasYaku;
            total += left * ((ron ? 3 : 0) + (tsumo ? 1 : 0));
        }

        return total;
    }

    // Lower is thrown first: lone honours, then terminals, then 2s and 8s, with value pairs kept longest.
    private static int KeepValue(TableView view, int[] counts, int kind)
    {
        var tile = Tile.FromTileIndex(kind);
        if (tile.IsHonor)
        {
            if (counts[kind] >= 2)
            {
                return PlayerHand.IsValueTile(tile, view.Seat, view.RoundWind) ? 4 : 1;
            }

            return 0;
        }

        if (tile.IsTerminal)
        {
            return 1;
        }

        return tile.Rank is 2 or 8 ? 2 : 3;
    }

    private static int EdgeTiles(Tile discard, ChiOption option)
    {
        return new[] { discard, option.A, option.B }.Count(t => t.IsTerminalOrHonor);
    }

    private static int DistanceFromCentre(Tile discard, ChiOption option)
    {
        return new[] { discard, option.A, option.B }.Sum(t => Math.Abs(t.Rank - 5));
    }

    // Defence ----------------------------------------------------------------------------------------------

    private sealed record Threat(SeatInfo Seat, double Weight);

    // With no riichi in the game, an open hand is the only warning sign: three open sets is nearly tenpai,
    // two open sets late in the round is worth respecting.
    private static List<Threat> Threats(TableView view)
    {
        var threats = new List<Threat>();
        foreach (var opponent in view.Opponents)
        {
            var open = opponent.OpenMeldCount;
            if (open >= 3)
            {
                threats.Add(new Threat(opponent, 1.0));
            }
            else if (open == 2 && opponent.Thrown.Count >= 6)
            {
                threats.Add(new Threat(opponent, 0.6));
            }
        }

        return threats;
    }

    // Fold when a hand that is still far from winning faces a real threat.
    private static bool ShouldFold(IReadOnlyList<Threat> threats, int shanten)
    {
        if (threats.Count == 0)
        {
            return false;
        }

        return threats.Any(t => t.Weight >= 1.0) ? shanten >= 1 : shanten >= 2;
    }

    // 0 (safe) to about 6 (very risky), taking the worst of the threatening opponents.
    private static double Danger(IReadOnlyList<Threat> threats, int[] visible, int roundWind, int kind)
    {
        var tile = Tile.FromTileIndex(kind);
        var worst = 0.0;
        foreach (var threat in threats)
        {
            worst = Math.Max(worst, DangerFrom(threat.Seat, tile, visible, roundWind) * threat.Weight);
        }

        return worst;
    }

    private static double DangerFrom(SeatInfo opponent, Tile tile, int[] visible, int roundWind)
    {
        // A player can't ron on a tile they threw themselves (furiten), so it is the one certainly safe tile.
        if (opponent.Thrown.Contains(tile))
        {
            return 0;
        }

        var kind = tile.ToTileIndex();
        if (tile.IsHonor)
        {
            // An honour only wins as a pair wait (they hold one) or a double-pair wait (they hold two).
            var unseen = 4 - visible[kind];
            var danger = unseen <= 0 ? 0.5 : unseen == 1 ? 2.0 : 3.0;
            return PlayerHand.IsValueTile(tile, opponent.Seat, roundWind) ? danger + 1 : danger;
        }

        var risk = tile.Rank switch
        {
            1 or 9 => 3.0,
            2 or 8 => 4.0,
            3 or 7 => 5.0,
            _ => 6.0
        };

        // Suji: a player who threw the tile three ranks away can't be waiting on it with a two-sided wait.
        var low = ThrewSameSuit(opponent, tile, tile.Rank - 3);
        var high = ThrewSameSuit(opponent, tile, tile.Rank + 3);
        var safeSides = tile.Rank switch
        {
            <= 3 => high ? 2 : 0,
            >= 7 => low ? 2 : 0,
            _ => (low ? 1 : 0) + (high ? 1 : 0)
        };
        risk *= safeSides == 2 ? 0.4 : safeSides == 1 ? 0.7 : 1.0;

        // An opponent whose open sets are all one suit is probably collecting that suit.
        var open = opponent.Melds.Where(m => m.IsOpen).ToList();
        if (open.Count >= 2 && open.All(m => m.Tiles[0].Suit == tile.Suit))
        {
            risk += 1;
        }

        return risk;
    }

    private static bool ThrewSameSuit(SeatInfo opponent, Tile tile, int rank)
    {
        return rank is >= 1 and <= 9 && opponent.Thrown.Contains(new Tile(tile.Suit, rank));
    }
}
