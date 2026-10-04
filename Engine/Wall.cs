using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// 136 tiles: a live wall that players draw from and a 14-tile dead wall.
// The dead wall is laid out as 4 replacement tiles for kans, 5 dora indicators and 5 more
// (the tiles under the indicators, unused without riichi). Every kan takes one replacement tile
// and moves the last live tile into the dead wall, so the dead wall always stays at 14.
public sealed class Wall
{
    public const int DeadWallSize = 14;

    private readonly List<Tile> _live;          // draws come from the end
    private readonly List<Tile> _replacements;  // kan draws come from the front
    private readonly List<Tile> _indicators;
    private readonly List<Tile> _under;
    private int _revealed;

    public int LiveCount => _live.Count;
    public IReadOnlyList<Tile> DeadWall => _replacements.Concat(_indicators).Concat(_under).ToList();

    // Dora indicators turned face-up so far (one at the start, one more per kan).
    public IReadOnlyList<Tile> DoraIndicators => _indicators.Take(_revealed).ToList();

    // Kans still possible before the dead wall has no replacement tile left.
    public int ReplacementsLeft => _replacements.Count;

    public Wall(Random rng)
    {
        var tiles = BuildTiles();
        Shuffle(tiles, rng);

        var dead = tiles.GetRange(tiles.Count - DeadWallSize, DeadWallSize);
        tiles.RemoveRange(tiles.Count - DeadWallSize, DeadWallSize);

        _live = tiles;
        _replacements = dead.GetRange(0, 4);
        _indicators = dead.GetRange(4, 5);
        _under = dead.GetRange(9, 5);
        _revealed = 1;
    }

    private Wall(List<Tile> live, List<Tile> replacements, List<Tile> indicators, List<Tile> under)
    {
        _live = live;
        _replacements = replacements;
        _indicators = indicators;
        _under = under;
        _revealed = 1;
    }

    // A wall with a chosen order, for tests and puzzles. drawOrder is what gets dealt and drawn first
    // (the deal takes 13 tiles for each seat in turn, then draws continue from the same list);
    // replacements are the kan draws and indicators the dora indicators, first one revealed first.
    // Whatever is left of the 136 tiles fills the rest of the wall.
    public static Wall Stacked(
        IEnumerable<Tile> drawOrder,
        IEnumerable<Tile>? replacements = null,
        IEnumerable<Tile>? indicators = null)
    {
        var pool = BuildTiles();
        List<Tile> Take(IEnumerable<Tile>? wanted)
        {
            var taken = new List<Tile>();
            foreach (var tile in wanted ?? Enumerable.Empty<Tile>())
            {
                if (!pool.Remove(tile))
                {
                    throw new ArgumentException($"There is no {tile} left to place in the wall.");
                }

                taken.Add(tile);
            }

            return taken;
        }

        var draws = Take(drawOrder);
        var repl = Take(replacements);
        var indic = Take(indicators);
        if (repl.Count > 4 || indic.Count > 5)
        {
            throw new ArgumentException("A wall has 4 replacement tiles and 5 dora indicators.");
        }

        // Fill the unspecified dead wall slots first, then everything else goes to the live wall.
        while (repl.Count < 4)
        {
            repl.Add(PopLast(pool));
        }

        while (indic.Count < 5)
        {
            indic.Add(PopLast(pool));
        }

        var under = new List<Tile>();
        while (under.Count < 5)
        {
            under.Add(PopLast(pool));
        }

        draws.Reverse();
        pool.AddRange(draws);
        return new Wall(pool, repl, indic, under);
    }

    private static Tile PopLast(List<Tile> pool)
    {
        var tile = pool[^1];
        pool.RemoveAt(pool.Count - 1);
        return tile;
    }

    public Tile? Draw()
    {
        if (_live.Count == 0)
        {
            return null;
        }

        return PopLast(_live);
    }

    // The tile a player draws after declaring a kan. The dead wall is topped up from the end of the live wall.
    public Tile DrawReplacement()
    {
        if (_replacements.Count == 0 || _live.Count == 0)
        {
            throw new InvalidOperationException("No kan is possible: the dead wall has no replacement tile to give.");
        }

        var tile = _replacements[0];
        _replacements.RemoveAt(0);
        _under.Add(_live[0]);
        _live.RemoveAt(0);
        return tile;
    }

    // Turns the next dora indicator face-up (after a kan). Returns null when all five are showing.
    public Tile? RevealDoraIndicator()
    {
        if (_revealed >= _indicators.Count)
        {
            return null;
        }

        return _indicators[_revealed++];
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
