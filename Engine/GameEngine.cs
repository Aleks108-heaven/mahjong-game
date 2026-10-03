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

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Tile> Discards => _discards;

    // Pass a seed to make a round reproducible; pass a writer to capture or redirect output.
    public GameEngine(int? seed = null, TextWriter? output = null)
    {
        _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        _output = output ?? Console.Out;

        for (var i = 0; i < 4; i++)
        {
            _players.Add(new Player(i));
        }
    }

    public RoundResult RunSimulation()
    {
        SetupRound();
        _currentPlayerIndex = 0;
        _state = TurnState.Draw;

        var turns = 0;
        Tile? lastDiscard = null;

        // The wall is only checked when a new turn starts, so every turn that begins finishes.
        while (true)
        {
            var player = _players[_currentPlayerIndex];

            switch (_state)
            {
                case TurnState.Draw:
                    var tile = _wall.Draw();
                    if (tile is null)
                    {
                        _output.WriteLine($"Wall exhausted after {turns} turns. Exhaustive draw.");
                        return new RoundResult(RoundOutcome.ExhaustiveDraw, null, null, turns);
                    }

                    turns++;
                    player.Hand.Add(tile);
                    player.Hand.Sort();
                    _output.WriteLine(
                        $"T{turns,-3} {Label(player)} draws {tile,-3} | {player.Hand} | shanten {player.Hand.GetShanten()} | wall {_wall.LiveCount}");
                    _state = TurnState.ActionPhase;
                    break;

                case TurnState.ActionPhase:
                    if (player.Hand.IsComplete())
                    {
                        _output.WriteLine($"{Label(player)} wins by tsumo with {player.Hand}");
                        return new RoundResult(RoundOutcome.Tsumo, player.Seat, null, turns);
                    }

                    _state = TurnState.Discard;
                    break;

                case TurnState.Discard:
                    lastDiscard = player.Hand.DiscardBest(_rng);
                    _discards.Add(lastDiscard);
                    _output.WriteLine($"     {Label(player)} discards {lastDiscard} | discards so far {_discards.Count}");
                    _state = TurnState.WaitPhase;
                    break;

                case TurnState.WaitPhase:
                    // Other players may declare ron, checked in turn order from the discarder's left.
                    for (var offset = 1; offset < 4; offset++)
                    {
                        var other = _players[(_currentPlayerIndex + offset) % 4];
                        if (other.Hand.CanWinOn(lastDiscard!))
                        {
                            other.Hand.Add(lastDiscard!);
                            other.Hand.Sort();
                            _output.WriteLine(
                                $"{Label(other)} wins by ron on {lastDiscard} from {Label(player)} with {other.Hand}");
                            return new RoundResult(RoundOutcome.Ron, other.Seat, player.Seat, turns);
                        }
                    }

                    _state = TurnState.NextPlayer;
                    break;

                case TurnState.NextPlayer:
                    _currentPlayerIndex = (_currentPlayerIndex + 1) % 4;
                    _state = TurnState.Draw;
                    break;
            }
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
            _output.WriteLine($"{Label(player)} starting hand: {player.Hand}");
        }

        _output.WriteLine();
    }

    private static string Label(Player player)
    {
        return $"P{player.Seat}({SeatWinds[player.Seat]})";
    }
}
