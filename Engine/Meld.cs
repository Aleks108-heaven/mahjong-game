using System.Collections.Generic;

namespace TestMahjongGame.Engine;

public enum MeldType
{
    Chi,
    Pon
}

// The two tiles from a hand that would complete a sequence with a discarded tile.
public sealed record ChiOption(Tile A, Tile B);

// An open set made by calling another player's discard.
public sealed record Meld(MeldType Type, IReadOnlyList<Tile> Tiles, Tile CalledTile, int FromSeat)
{
    public override string ToString()
    {
        return $"[{string.Join(" ", Tiles)}]";
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
public sealed record CallOptions(Tile Discard, int FromSeat, bool CanPon, IReadOnlyList<ChiOption> ChiOptions);
