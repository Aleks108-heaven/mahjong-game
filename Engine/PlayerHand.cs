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
    public int TotalTiles => _tiles.Count + 3 * _melds.Count;

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

    public Tile DiscardRandom(Random rng)
    {
        var index = rng.Next(_tiles.Count);
        var tile = _tiles[index];
        _tiles.RemoveAt(index);
        return tile;
    }

    // Discards the tile that leaves the lowest shanten; ties are broken randomly.
    // Tiles in forbidden (swap-calling) are skipped unless nothing else is left.
    public Tile DiscardBest(Random rng, IReadOnlyCollection<Tile>? forbidden = null)
    {
        var best = BestDiscardKinds(forbidden);
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
