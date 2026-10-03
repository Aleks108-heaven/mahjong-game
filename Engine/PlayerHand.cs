using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

public sealed class PlayerHand
{
    private readonly List<Tile> _tiles = new();
    private readonly ShantenCalculator _shantenCalculator = new();

    public IReadOnlyList<Tile> Tiles => _tiles;

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
        return _shantenCalculator.CalculateShanten(ToTileCounts());
    }

    // Shanten the hand would have if it also held the given tile (used for ron checks).
    public int GetShantenWith(Tile extra)
    {
        var counts = ToTileCounts();
        counts[extra.ToTileIndex()]++;
        return _shantenCalculator.CalculateShanten(counts);
    }

    public bool IsTenpai()
    {
        return _tiles.Count % 3 == 1 && GetShanten() == 0;
    }

    // A 14-tile hand with shanten -1 is a winning hand.
    public bool IsComplete()
    {
        return _tiles.Count == 14 && GetShanten() == -1;
    }

    public bool CanWinOn(Tile tile)
    {
        return _tiles.Count == 13 && GetShantenWith(tile) == -1;
    }

    public Tile DiscardRandom(Random rng)
    {
        var index = rng.Next(_tiles.Count);
        var tile = _tiles[index];
        _tiles.RemoveAt(index);
        return tile;
    }

    // Discards the tile that leaves the lowest shanten; ties are broken randomly.
    public Tile DiscardBest(Random rng)
    {
        var best = BestDiscardKinds();
        var chosen = best[rng.Next(best.Count)];
        var tile = _tiles.First(t => t.ToTileIndex() == chosen);
        _tiles.Remove(tile);
        return tile;
    }

    // Every distinct tile whose discard leaves the lowest shanten (used for hints).
    public IReadOnlyList<Tile> SuggestDiscards()
    {
        return BestDiscardKinds().Select(Tile.FromTileIndex).ToList();
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
        return _shantenCalculator.CalculateShanten(counts);
    }

    private List<int> BestDiscardKinds()
    {
        var counts = ToTileCounts();
        var best = new List<int>();
        var bestShanten = int.MaxValue;

        for (var kind = 0; kind < counts.Length; kind++)
        {
            if (counts[kind] == 0)
            {
                continue;
            }

            counts[kind]--;
            var shanten = _shantenCalculator.CalculateShanten(counts);
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

    public override string ToString()
    {
        return string.Join(" ", _tiles.OrderBy(tile => tile));
    }
}
