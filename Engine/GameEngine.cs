using System;
using System.Collections.Generic;
using System.IO;

namespace TestMahjongGame.Engine;

public enum RoundOutcome
{
    Tsumo,
    Ron,
    ExhaustiveDraw
}

public sealed record RoundResult(RoundOutcome Outcome, int? WinnerSeat, int? LoserSeat, int Turns);

public sealed class GameEngine
{
    private static readonly string[] SeatWinds = { "E", "S", "W", "N" };

    private readonly Random _rng;
    private readonly TextWriter _output;
    private readonly List<Player> _players = new();
    private readonly List<Tile> _discards = new();
    private Wall _wall = null!;
    private int _currentPlayerIndex;
    private TurnState _state;
    private bool _started;

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Tile> Discards => _discards;

    public bool IsFinished => Result is not null;
    public RoundResult? Result { get; private set; }
    public int Turns { get; private set; }
    public int WallRemaining => _started ? _wall.LiveCount : 0;

    // Seat whose turn comes next (or who just won / just discarded last when the round ended).
    public int CurrentSeat => _currentPlayerIndex;
    public Tile? LastDiscard { get; private set; }
    public int? LastDiscardSeat { get; private set; }
    public Tile? WinningTile { get; private set; }

    // The tile the current player (or the last one to draw) just drew, so a UI can point at it.
    public Tile? LastDrawnTile { get; private set; }
    public int? LastDrawSeat { get; private set; }

    // Seat controlled by a person, or null when all four seats are bots.
    public int? HumanSeat { get; private set; }

    // True while the round is paused for the human to choose a discard (call DiscardHuman).
    public bool AwaitingHumanDiscard =>
        _started && !IsFinished && HumanSeat == _currentPlayerIndex && _state == TurnState.Discard;

    // Raised for every log line, so a UI can show the same text the console prints.
    public event Action<string>? Logged;

    // Pass a seed to make a round reproducible; pass a writer to capture or redirect output.
    public GameEngine(int? seed = null, TextWriter? output = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _output = output ?? Console.Out;
    }

    public static string WindName(int seat)
    {
        return SeatWinds[seat];
    }

    public RoundResult RunSimulation()
    {
        StartRound();

        while (!IsFinished)
        {
            Advance();
        }

        return Result!;
    }

    // Deals a fresh round. Safe to call again to start another round with the same random stream.
    // Pass a seat (0-3) to let a person choose that seat's discards; the other seats stay bots.
    public void StartRound(int? humanSeat = null)
    {
        if (humanSeat is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(humanSeat), humanSeat, "Seat must be between 0 and 3.");
        }

        HumanSeat = humanSeat;
        LastDrawnTile = null;
        LastDrawSeat = null;
        _players.Clear();
        _discards.Clear();
        for (var i = 0; i < 4; i++)
        {
            _players.Add(new Player(i));
        }

        Result = null;
        Turns = 0;
        LastDiscard = null;
        LastDiscardSeat = null;
        WinningTile = null;

        SetupRound();
        _currentPlayerIndex = 0;
        _state = TurnState.Draw;
        _started = true;
    }

    // Plays one complete turn: draw, tsumo check, discard, ron check. Does nothing once the round is over.
    public void PlayTurn()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Call StartRound() first.");
        }

        if (IsFinished || AwaitingHumanDiscard)
        {
            return;
        }

        do
        {
            Advance();
        }
        while (!IsFinished && _state != TurnState.Draw && !AwaitingHumanDiscard);
    }

    // The human throws a tile from their hand; the turn then finishes (ron check) and passes on.
    public void DiscardHuman(Tile tile)
    {
        if (!AwaitingHumanDiscard)
        {
            throw new InvalidOperationException("The engine is not waiting for a human discard.");
        }

        var player = _players[_currentPlayerIndex];
        if (!player.Hand.Remove(tile))
        {
            throw new ArgumentException("The hand does not contain that tile.", nameof(tile));
        }

        CommitDiscard(player, tile);

        while (!IsFinished && _state != TurnState.Draw)
        {
            Advance();
        }
    }

    private void CommitDiscard(Player player, Tile discarded)
    {
        _discards.Add(discarded);
        player.AddDiscard(discarded);
        LastDiscard = discarded;
        LastDiscardSeat = player.Seat;
        Log($"     {Label(player)} discards {discarded} | discards so far {_discards.Count}");
        _state = TurnState.WaitPhase;
    }

    // The wall is only checked when a new turn starts, so every turn that begins finishes.
    private void Advance()
    {
        var player = _players[_currentPlayerIndex];

        switch (_state)
        {
            case TurnState.Draw:
                var tile = _wall.Draw();
                if (tile is null)
                {
                    Log($"Wall exhausted after {Turns} turns. Exhaustive draw.");
                    Result = new RoundResult(RoundOutcome.ExhaustiveDraw, null, null, Turns);
                    return;
                }

                Turns++;
                LastDrawnTile = tile;
                LastDrawSeat = player.Seat;
                player.Hand.Add(tile);
                player.Hand.Sort();
                Log($"T{Turns,-3} {Label(player)} draws {tile,-3} | {player.Hand} | shanten {player.Hand.GetShanten()} | wall {_wall.LiveCount}");
                _state = TurnState.ActionPhase;
                break;

            case TurnState.ActionPhase:
                if (player.Hand.IsComplete())
                {
                    Log($"{Label(player)} wins by tsumo with {player.Hand}");
                    Result = new RoundResult(RoundOutcome.Tsumo, player.Seat, null, Turns);
                    return;
                }

                _state = TurnState.Discard;
                break;

            case TurnState.Discard:
                if (player.Seat == HumanSeat)
                {
                    // Paused: the human picks the tile through DiscardHuman.
                    return;
                }

                CommitDiscard(player, player.Hand.DiscardBest(_rng));
                break;

            case TurnState.WaitPhase:
                // Other players may declare ron, checked in turn order from the discarder's left.
                for (var offset = 1; offset < 4; offset++)
                {
                    var other = _players[(_currentPlayerIndex + offset) % 4];
                    if (other.Hand.CanWinOn(LastDiscard!))
                    {
                        other.Hand.Add(LastDiscard!);
                        other.Hand.Sort();
                        WinningTile = LastDiscard;
                        Log($"{Label(other)} wins by ron on {LastDiscard} from {Label(player)} with {other.Hand}");
                        Result = new RoundResult(RoundOutcome.Ron, other.Seat, player.Seat, Turns);
                        _currentPlayerIndex = other.Seat;
                        return;
                    }
                }

                _state = TurnState.NextPlayer;
                break;

            case TurnState.NextPlayer:
                _currentPlayerIndex = (_currentPlayerIndex + 1) % 4;
                _state = TurnState.Draw;

                // End the round as soon as the last turn is done, rather than on the next failed draw.
                if (_wall.LiveCount == 0)
                {
                    Log($"Wall exhausted after {Turns} turns. Exhaustive draw.");
                    Result = new RoundResult(RoundOutcome.ExhaustiveDraw, null, null, Turns);
                }

                break;
        }
    }

    private void SetupRound()
    {
        _wall = new Wall(_rng);

        foreach (var player in _players)
        {
            for (var i = 0; i < 13; i++)
            {
                player.Hand.Add(_wall.Draw()!);
            }

            player.Hand.Sort();
            Log($"{Label(player)} starting hand: {player.Hand}");
        }

        Log(string.Empty);
    }

    private void Log(string line)
    {
        _output.WriteLine(line);
        Logged?.Invoke(line);
    }

    private static string Label(Player player)
    {
        return $"P{player.Seat}({SeatWinds[player.Seat]})";
    }
}
