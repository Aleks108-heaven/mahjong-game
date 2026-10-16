using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// What anybody at the table can see of one seat: its river, everything it ever threw (called tiles included)
// and its melds. Nothing about the concealed hand.
public sealed record SeatInfo(int Seat, IReadOnlyList<Tile> River, IReadOnlyList<Tile> Thrown, IReadOnlyList<Meld> Melds)
{
    // Chi, pon and open or added kans. A concealed kan keeps the hand closed.
    public int OpenMeldCount => Melds.Count(m => m.IsOpen);
}

// Everything one bot is allowed to know when it decides: its own hand plus the public table.
// Bots get this instead of the engine, so a bot can't peek at another player's concealed tiles or at the wall.
public sealed class TableView
{
    private readonly int[] _visible;

    // seats is indexed by seat number and includes the viewing seat itself.
    public TableView(
        int seat,
        PlayerHand hand,
        IReadOnlyList<SeatInfo> seats,
        IReadOnlyList<Tile> doraIndicators,
        int roundWind,
        int wallRemaining,
        bool canKan,
        IReadOnlyList<Tile> forbidden)
    {
        if (seats.Count != 4)
        {
            throw new ArgumentException("A table has four seats.", nameof(seats));
        }

        Seat = seat;
        Hand = hand;
        Seats = seats;
        DoraIndicators = doraIndicators;
        RoundWind = roundWind;
        WallRemaining = wallRemaining;
        CanKan = canKan;
        Forbidden = forbidden;
        _visible = CountVisible();
    }

    public int Seat { get; }

    // The viewing seat's own hand. A bot reads it; only the engine changes it.
    public PlayerHand Hand { get; }

    public IReadOnlyList<SeatInfo> Seats { get; }
    public IReadOnlyList<Tile> DoraIndicators { get; }
    public int RoundWind { get; }
    public int WallRemaining { get; }

    // Whether the table rules allow another kan right now (fewer than four so far, tiles left in the wall).
    public bool CanKan { get; }

    // Tiles barred by the swap-calling rule this turn.
    public IReadOnlyList<Tile> Forbidden { get; }

    public IEnumerable<SeatInfo> Opponents => Seats.Where(s => s.Seat != Seat);

    // For every tile kind, how many copies this seat can see: its own concealed tiles, every meld and river,
    // and the dora indicators. 4 minus this is how many could still be in other hands or the wall.
    public int[] VisibleCounts()
    {
        return (int[])_visible.Clone();
    }

    public int Visible(Tile tile)
    {
        return _visible[tile.ToTileIndex()];
    }

    private int[] CountVisible()
    {
        var counts = Hand.TileCounts();
        foreach (var seat in Seats)
        {
            foreach (var tile in seat.River)
            {
                counts[tile.ToTileIndex()]++;
            }

            foreach (var meld in seat.Melds)
            {
                foreach (var tile in meld.Tiles)
                {
                    counts[tile.ToTileIndex()]++;
                }
            }
        }

        foreach (var indicator in DoraIndicators)
        {
            counts[indicator.ToTileIndex()]++;
        }

        return counts;
    }
}
