using System.Collections.Generic;

namespace TestMahjongGame.Engine;

public enum MeldType
{
    Chi,
    Pon,

    // Kan made from another player's discard (open).
    Daiminkan,

    // Kan made from four tiles in your own hand. The hand stays closed.
    Ankan,

    // A pon upgraded with the fourth tile (open).
    Kakan
}

// The two tiles from a hand that would complete a sequence with a discarded tile.
public sealed record ChiOption(Tile A, Tile B);

// A kan the current player can declare on their own turn: four in hand (Ankan) or a fourth for a pon (Kakan).
public sealed record KanOption(Tile Tile, MeldType Type);

// A set on the table. Tiles has three tiles, or four for a kan.
public sealed record Meld(MeldType Type, IReadOnlyList<Tile> Tiles, Tile CalledTile, int FromSeat)
{
    public bool IsKan => Type is MeldType.Daiminkan or MeldType.Ankan or MeldType.Kakan;

    // Only a concealed kan keeps the hand closed.
    public bool IsOpen => Type != MeldType.Ankan;

    // Three or four of one tile (pon or any kan).
    public bool IsTriplet => Type != MeldType.Chi;

    // A concealed kan is written with round brackets so it reads differently from an open set.
    public override string ToString()
    {
        var tiles = string.Join(" ", Tiles);
        return Type == MeldType.Ankan ? $"({tiles})" : $"[{tiles}]";
    }

    // Swap-calling rule: right after a call you may not discard the called tile, nor (for a chi)
    // the tile that would complete the same sequence from the other end.
    public static IReadOnlyList<Tile> ForbiddenDiscards(MeldType type, Tile called, IReadOnlyList<Tile> meldTiles)
    {
        var forbidden = new List<Tile> { called };
        if (type != MeldType.Chi || called.IsHonor)
        {
            return forbidden;
        }

        var low = 9;
        var high = 1;
        foreach (var tile in meldTiles)
        {
            low = System.Math.Min(low, tile.Rank);
            high = System.Math.Max(high, tile.Rank);
        }

        if (called.Rank == low && high + 1 <= 9)
        {
            forbidden.Add(new Tile(called.Suit, high + 1));
        }
        else if (called.Rank == high && low - 1 >= 1)
        {
            forbidden.Add(new Tile(called.Suit, low - 1));
        }

        return forbidden;
    }
}

// What the human may do with a discard they could call. Pass is always allowed.
public sealed record CallOptions(
    Tile Discard,
    int FromSeat,
    bool CanPon,
    IReadOnlyList<ChiOption> ChiOptions,
    bool CanKan = false);
