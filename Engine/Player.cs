namespace TestMahjongGame.Engine;

public sealed class Player
{
    public int Seat { get; }
    public PlayerHand Hand { get; } = new();

    public Player(int seat)
    {
        Seat = seat;
    }
}