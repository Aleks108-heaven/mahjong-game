using System;
using System.Collections.Generic;

namespace TestMahjongGame.Engine;

public sealed class Wall
{
    private readonly Stack<Tile> _liveWall;
    private readonly List<Tile> _deadWall;

    public int LiveCount => _liveWall.Count;
    public IReadOnlyList<Tile> DeadWall => _deadWall;

    public Wall(Random rng)
    {
        var tiles = BuildTiles();
        Shuffle(tiles, rng);

        _deadWall = tiles.GetRange(tiles.Count - 14, 14);
        tiles.RemoveRange(tiles.Count - 14, 14);
        _liveWall = new Stack<Tile>(tiles);
    }

    public Tile? Draw()
    {
        return _liveWall.Count > 0 ? _liveWall.Pop() : null;
    }

    private static List<Tile> BuildTiles()
    {
        var tiles = new List<Tile>(136);

        foreach (var suit in new[] { Suit.Manzu, Suit.Pinzu, Suit.Souzu })
        {
            for (var rank = 1; rank <= 9; rank++)
            {
                for (var copy = 0; copy < 4; copy++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
        }

        foreach (Honor honor in Enum.GetValues(typeof(Honor)))
        {
            for (var copy = 0; copy < 4; copy++)
            {
                tiles.Add(new Tile(honor));
            }
        }

        return tiles;
    }

    private static void Shuffle(IList<Tile> tiles, Random rng)
    {
        for (var i = tiles.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
    }
}