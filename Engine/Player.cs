using System.Collections.Generic;

namespace TestMahjongGame.Engine;

public sealed class Player
{
    private readonly List<Tile> _discards = new();

    public int Seat { get; }
    public PlayerHand Hand { get; } = new();

    // This player's own discard river, in the order the tiles were thrown.
    // A tile that another player calls leaves the river and joins that player's meld.
    public IReadOnlyList<Tile> Discards => _discards;

    public Player(int seat)
    {
        Seat = seat;
    }

    internal void AddDiscard(Tile tile)
    {
        _discards.Add(tile);
    }

    internal void RemoveLastDiscard()
    {
        _discards.RemoveAt(_discards.Count - 1);
    }
}
