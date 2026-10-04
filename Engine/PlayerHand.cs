using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

public sealed class PlayerHand
{
    private readonly List<Tile> _tiles = new();
    private readonly List<Meld> _melds = new();
    private readonly ShantenCalculator _shantenCalculator = new();

    // Concealed tiles only; open melds are in Melds.
    public IReadOnlyList<Tile> Tiles => _tiles;
    public IReadOnlyList<Meld> Melds => _melds;
    public int MeldCount => _melds.Count;

    // Concealed tiles plus three per meld: 13 between turns, 14 right after a draw.
    // A kan counts as three too, because its extra tile is paid for by the replacement draw.
    public int TotalTiles => _tiles.Count + 3 * _melds.Count;

    // Tiles physically in front of the player, counting all four of a kan.
    public int PhysicalTileCount => _tiles.Count + _melds.Sum(m => m.Tiles.Count);

    // A hand with no open meld is closed (a concealed kan keeps it closed).
    public bool IsClosed => _melds.All(m => !m.IsOpen);

    public void Add(Tile tile)
    {
        _tiles.Add(tile);
    }

    public bool Remove(Tile tile)
    {
        return _tiles.Remove(tile);
    }

    public void Sort()
    {
        _tiles.Sort();
    }

    public int GetShanten()
    {
        return _shantenCalculator.CalculateShanten(ToTileCounts(), MeldCount);
    }

    // Shanten the hand would have if it also held the given tile (used for ron checks).
    public int GetShantenWith(Tile extra)
    {
        var counts = ToTileCounts();
        counts[extra.ToTileIndex()]++;
        return _shantenCalculator.CalculateShanten(counts, MeldCount);
    }

    public bool IsTenpai()
    {
        return TotalTiles == 13 && GetShanten() == 0;
    }

    // A 14-tile hand (counting melds) with shanten -1 is a winning hand.
    public bool IsComplete()
    {
        return TotalTiles == 14 && GetShanten() == -1;
    }

    public bool CanWinOn(Tile tile)
    {
        return TotalTiles == 13 && GetShantenWith(tile) == -1;
    }

    public bool CanPon(Tile discard)
    {
        return _tiles.Count(t => t.Equals(discard)) >= 2;
    }

    // Every pair of hand tiles that forms a sequence with the discard (chi). Honors can't be chi'd.
    public IReadOnlyList<ChiOption> ChiOptions(Tile discard)
    {
        var options = new List<ChiOption>();
        if (discard.IsHonor)
        {
            return options;
        }

        void TryAdd(int low, int high)
        {
            if (low < 1 || high > 9)
            {
                return;
            }

            var a = new Tile(discard.Suit, low);
            var b = new Tile(discard.Suit, high);
            if (_tiles.Contains(a) && _tiles.Contains(b))
            {
                options.Add(new ChiOption(a, b));
            }
        }

        var rank = discard.Rank;
        TryAdd(rank - 2, rank - 1);
        TryAdd(rank - 1, rank + 1);
        TryAdd(rank + 1, rank + 2);
        return options;
    }

    public Meld Pon(Tile called, int fromSeat)
    {
        if (!CanPon(called))
        {
            throw new InvalidOperationException("The hand does not hold two matching tiles.");
        }

        _tiles.Remove(called);
        _tiles.Remove(called);
        var meld = new Meld(MeldType.Pon, new[] { called, called, called }, called, fromSeat);
        _melds.Add(meld);
        return meld;
    }

    public Meld Chi(Tile called, ChiOption option, int fromSeat)
    {
        if (!ChiOptions(called).Contains(option))
        {
            throw new InvalidOperationException("The hand cannot form that sequence.");
        }

        _tiles.Remove(option.A);
        _tiles.Remove(option.B);
        var tiles = new[] { called, option.A, option.B }.OrderBy(t => t).ToList();
        var meld = new Meld(MeldType.Chi, tiles, called, fromSeat);
        _melds.Add(meld);
        return meld;
    }

    // Three matching tiles in hand let you take a fourth discard as an open kan.
    public bool CanDaiminkan(Tile discard)
    {
        return _tiles.Count(t => t.Equals(discard)) >= 3;
    }

    public Meld Daiminkan(Tile called, int fromSeat)
    {
        if (!CanDaiminkan(called))
        {
            throw new InvalidOperationException("The hand does not hold three matching tiles.");
        }

        for (var i = 0; i < 3; i++)
        {
            _tiles.Remove(called);
        }

        var meld = new Meld(MeldType.Daiminkan, new[] { called, called, called, called }, called, fromSeat);
        _melds.Add(meld);
        return meld;
    }

    // Kans this hand can declare itself: four in hand (Ankan) or the fourth tile of an existing pon (Kakan).
    public IReadOnlyList<KanOption> KanOptions()
    {
        var options = new List<KanOption>();
        foreach (var group in _tiles.GroupBy(t => t.ToTileIndex()).OrderBy(g => g.Key))
        {
            if (group.Count() == 4)
            {
                options.Add(new KanOption(group.First(), MeldType.Ankan));
            }
        }

        foreach (var meld in _melds)
        {
            if (meld.Type == MeldType.Pon && _tiles.Contains(meld.CalledTile))
            {
                options.Add(new KanOption(meld.CalledTile, MeldType.Kakan));
            }
        }

        return options;
    }

    public Meld Ankan(Tile tile)
    {
        if (_tiles.Count(t => t.Equals(tile)) < 4)
        {
            throw new InvalidOperationException("The hand does not hold four matching tiles.");
        }

        for (var i = 0; i < 4; i++)
        {
            _tiles.Remove(tile);
        }

        var meld = new Meld(MeldType.Ankan, new[] { tile, tile, tile, tile }, tile, -1);
        _melds.Add(meld);
        return meld;
    }

    // Adds the fourth tile to a pon. Returns the new meld that replaced the pon.
    public Meld Kakan(Tile tile)
    {
        var index = _melds.FindIndex(m => m.Type == MeldType.Pon && m.CalledTile.Equals(tile));
        if (index < 0 || !_tiles.Contains(tile))
        {
            throw new InvalidOperationException("The hand has no pon to add that tile to.");
        }

        _tiles.Remove(tile);
        var pon = _melds[index];
        var meld = new Meld(MeldType.Kakan, new[] { tile, tile, tile, tile }, pon.CalledTile, pon.FromSeat);
        _melds[index] = meld;
        return meld;
    }

    // Shanten right after declaring the kan, before the replacement draw (13 tiles counting each meld as 3).
    public int ShantenAfterKan(KanOption option)
    {
        var counts = ToTileCounts();
        var index = option.Tile.ToTileIndex();
        if (option.Type == MeldType.Ankan)
        {
            counts[index] -= 4;
            return _shantenCalculator.CalculateShanten(counts, MeldCount + 1);
        }

        counts[index]--;
        return _shantenCalculator.CalculateShanten(counts, MeldCount);
    }

    // Shanten after taking a discard as an open kan (13 tiles counting each meld as 3).
    public int ShantenAfterDaiminkan(Tile discard)
    {
        var counts = ToTileCounts();
        counts[discard.ToTileIndex()] -= 3;
        return _shantenCalculator.CalculateShanten(counts, MeldCount + 1);
    }

    // The shanten the hand reaches after its best discard (what a 14-tile hand is really worth).
    public int BestDiscardShanten()
    {
        var counts = ToTileCounts();
        var best = int.MaxValue;
        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] == 0)
            {
                continue;
            }

            counts[kind]--;
            best = Math.Min(best, _shantenCalculator.CalculateShanten(counts, MeldCount));
            counts[kind]++;
        }

        return best;
    }

    // The tiles that would complete a 13-tile hand. Empty unless the hand is tenpai.
    public IReadOnlyList<Tile> Waits()
    {
        var waits = new List<Tile>();
        if (TotalTiles != 13)
        {
            return waits;
        }

        var counts = ToTileCounts();
        for (var kind = 0; kind < counts.Length; kind++)
        {
            // Four already in hand can't be drawn or discarded by anyone else.
            if (counts[kind] >= 4)
            {
                continue;
            }

            counts[kind]++;
            if (_shantenCalculator.CalculateShanten(counts, MeldCount) == -1)
            {
                waits.Add(Tile.FromTileIndex(kind));
            }

            counts[kind]--;
        }

        return waits;
    }

    // Bot rule of thumb for opening the hand: a call is only worth making if a yaku stays possible,
    // either a value-tile set (dragon, seat wind, round wind) or an all-simples plan with few
    // terminals and honours left to throw away. meldTiles is the new set; fromHand the tiles it uses from the hand.
    public bool OpenCallKeepsAYaku(
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> fromHand,
        bool isTriplet,
        int seatWind,
        int roundWind)
    {
        if (isTriplet && IsValueTile(meldTiles[0], seatWind, roundWind))
        {
            return true;
        }

        if (HasValueSet(seatWind, roundWind))
        {
            return true;
        }

        var allMelds = _melds.SelectMany(m => m.Tiles).Concat(meldTiles);
        if (allMelds.Any(t => t.IsTerminalOrHonor))
        {
            return false;
        }

        var remaining = ToTileCounts();
        foreach (var tile in fromHand)
        {
            remaining[tile.ToTileIndex()]--;
        }

        var edges = 0;
        for (var kind = 0; kind < remaining.Length; kind++)
        {
            if (Tile.FromTileIndex(kind).IsTerminalOrHonor)
            {
                edges += remaining[kind];
            }
        }

        return edges <= 3;
    }

    // True when the hand already holds an open or concealed-kan set of dragons or a relevant wind.
    public bool HasValueSet(int seatWind, int roundWind)
    {
        return _melds.Any(m => m.IsTriplet && IsValueTile(m.CalledTile, seatWind, roundWind));
    }

    // Dragons, your own wind and the round wind score a han as a set.
    private static bool IsValueTile(Tile tile, int seatWind, int roundWind)
    {
        return tile.Honor is { } honor && (honor >= Honor.White || (int)honor == seatWind || (int)honor == roundWind);
    }

    // True for an open hand that is playing for all simples, so it should shed terminals and honours first.
    public bool IsPlayingForSimples(int seatWind, int roundWind)
    {
        return !IsClosed && !HasValueSet(seatWind, roundWind) && _melds.All(m => m.Tiles.All(t => !t.IsTerminalOrHonor));
    }

    public Tile DiscardRandom(Random rng)
    {
        var index = rng.Next(_tiles.Count);
        var tile = _tiles[index];
        _tiles.RemoveAt(index);
        return tile;
    }

    // Discards the tile that leaves the lowest shanten; ties are broken randomly.
    // Tiles in forbidden (swap-calling) are skipped unless nothing else is left.
    // With preferEdges, terminals and honours go first among equally good discards (for an all-simples plan).
    public Tile DiscardBest(Random rng, IReadOnlyCollection<Tile>? forbidden = null, bool preferEdges = false)
    {
        var best = BestDiscardKinds(forbidden);
        if (preferEdges)
        {
            var edges = best.Where(kind => Tile.FromTileIndex(kind).IsTerminalOrHonor).ToList();
            if (edges.Count > 0)
            {
                best = edges;
            }
        }

        var chosen = best[rng.Next(best.Count)];
        var tile = _tiles.First(t => t.ToTileIndex() == chosen);
        _tiles.Remove(tile);
        return tile;
    }

    // Every distinct tile whose discard leaves the lowest shanten (used for hints).
    public IReadOnlyList<Tile> SuggestDiscards(IReadOnlyCollection<Tile>? forbidden = null)
    {
        return BestDiscardKinds(forbidden).Select(Tile.FromTileIndex).ToList();
    }

    // Shanten of the hand after throwing away one copy of the given tile.
    public int ShantenAfterDiscard(Tile tile)
    {
        var counts = ToTileCounts();
        if (counts[tile.ToTileIndex()] == 0)
        {
            throw new ArgumentException("The hand does not contain that tile.", nameof(tile));
        }

        counts[tile.ToTileIndex()]--;
        return _shantenCalculator.CalculateShanten(counts, MeldCount);
    }

    // Best shanten reachable by calling pon on the discard and then discarding the best allowed tile.
    public int ShantenAfterPon(Tile discard)
    {
        return BestShantenAfterCall(MeldType.Pon, discard, discard, discard);
    }

    public int ShantenAfterChi(Tile discard, ChiOption option)
    {
        return BestShantenAfterCall(MeldType.Chi, discard, option.A, option.B);
    }

    private int BestShantenAfterCall(MeldType type, Tile called, Tile a, Tile b)
    {
        var counts = ToTileCounts();
        counts[a.ToTileIndex()]--;
        counts[b.ToTileIndex()]--;

        var forbidden = Meld.ForbiddenDiscards(type, called, new[] { called, a, b })
            .Select(t => t.ToTileIndex())
            .ToHashSet();

        var bestAllowed = int.MaxValue;
        var bestAny = int.MaxValue;
        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] == 0)
            {
                continue;
            }

            counts[kind]--;
            var shanten = _shantenCalculator.CalculateShanten(counts, MeldCount + 1);
            counts[kind]++;

            bestAny = Math.Min(bestAny, shanten);
            if (!forbidden.Contains(kind))
            {
                bestAllowed = Math.Min(bestAllowed, shanten);
            }
        }

        return bestAllowed == int.MaxValue ? bestAny : bestAllowed;
    }

    private List<int> BestDiscardKinds(IReadOnlyCollection<Tile>? forbidden)
    {
        var counts = ToTileCounts();
        var forbiddenKinds = forbidden?.Select(t => t.ToTileIndex()).ToHashSet() ?? new HashSet<int>();

        var anyAllowed = false;
        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] > 0 && !forbiddenKinds.Contains(kind))
            {
                anyAllowed = true;
            }
        }

        if (!anyAllowed)
        {
            forbiddenKinds.Clear();
        }

        var best = new List<int>();
        var bestShanten = int.MaxValue;

        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] == 0 || forbiddenKinds.Contains(kind))
            {
                continue;
            }

            counts[kind]--;
            var shanten = _shantenCalculator.CalculateShanten(counts, MeldCount);
            counts[kind]++;

            if (shanten < bestShanten)
            {
                bestShanten = shanten;
                best.Clear();
            }

            if (shanten == bestShanten)
            {
                best.Add(kind);
            }
        }

        return best;
    }

    private int[] ToTileCounts()
    {
        var counts = new int[Tile.TileKindCount];
        foreach (var tile in _tiles)
        {
            counts[tile.ToTileIndex()]++;
        }

        return counts;
    }

    // Concealed tiles in order, then open melds in brackets: "1m 2m 5p [3s 4s 5s]".
    public override string ToString()
    {
        var concealed = string.Join(" ", _tiles.OrderBy(tile => tile));
        return _melds.Count == 0 ? concealed : $"{concealed} {string.Join(" ", _melds)}";
    }
}
