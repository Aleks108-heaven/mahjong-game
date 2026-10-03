using System;

namespace TestMahjongGame.Engine;

public enum Suit
{
    Manzu,
    Pinzu,
    Souzu,
    Honors
}

public enum Honor
{
    East,
    South,
    West,
    North,
    White,
    Green,
    Red
}

public sealed class Tile : IComparable<Tile>, IEquatable<Tile>
{
    public const int TileKindCount = 34;

    public Suit Suit { get; }
    public int Rank { get; }
    public Honor? Honor { get; }

    public Tile(Suit suit, int rank)
    {
        if (suit == Suit.Honors)
        {
            throw new ArgumentException("Use the Tile(Honor) constructor for honor tiles.", nameof(suit));
        }

        if (!Enum.IsDefined(suit))
        {
            throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit.");
        }

        if (rank < 1 || rank > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be between 1 and 9.");
        }

        Suit = suit;
        Rank = rank;
        Honor = null;
    }

    public Tile(Honor honor)
    {
        if (!Enum.IsDefined(honor))
        {
            throw new ArgumentOutOfRangeException(nameof(honor), honor, "Unknown honor.");
        }

        Suit = Suit.Honors;
        Rank = 0;
        Honor = honor;
    }

    public bool IsHonor => Suit == Suit.Honors;

    public bool IsTerminal => !IsHonor && (Rank == 1 || Rank == 9);

    public bool IsTerminalOrHonor => IsHonor || IsTerminal;

    public int ToTileIndex()
    {
        return Suit switch
        {
            Suit.Manzu => Rank - 1,
            Suit.Pinzu => 9 + (Rank - 1),
            Suit.Souzu => 18 + (Rank - 1),
            _ => 27 + (int)Honor!.Value
        };
    }

    public static Tile FromTileIndex(int index)
    {
        if (index < 0 || index >= TileKindCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Tile index must be between 0 and 33.");
        }

        return index switch
        {
            < 9 => new Tile(Suit.Manzu, index + 1),
            < 18 => new Tile(Suit.Pinzu, index - 9 + 1),
            < 27 => new Tile(Suit.Souzu, index - 18 + 1),
            _ => new Tile((Honor)(index - 27))
        };
    }

    public int CompareTo(Tile? other)
    {
        if (other is null)
        {
            return 1;
        }

        return ToTileIndex().CompareTo(other.ToTileIndex());
    }

    public bool Equals(Tile? other)
    {
        return other is not null && ToTileIndex() == other.ToTileIndex();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Tile);
    }

    public override int GetHashCode()
    {
        return ToTileIndex();
    }

    // Standard riichi notation: 1m-9m, 1p-9p, 1s-9s, honors 1z-7z (E S W N white green red).
    public override string ToString()
    {
        return Suit switch
        {
            Suit.Manzu => $"{Rank}m",
            Suit.Pinzu => $"{Rank}p",
            Suit.Souzu => $"{Rank}s",
            _ => $"{(int)Honor!.Value + 1}z"
        };
    }
}
