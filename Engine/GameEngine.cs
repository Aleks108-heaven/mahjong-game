using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    private List<Tile> _forbidden = new();
    private Wall _wall = null!;
    private int _currentPlayerIndex;
    private TurnState _state;
    private bool _started;
    private bool _stepBreak;

    public IReadOnlyList<Player> Players => _players;

    // Every tile thrown this round, in order, including ones that were later called.
    public IReadOnlyList<Tile> Discards => _discards;

    public bool IsFinished => Result is not null;
    public RoundResult? Result { get; private set; }

    // Number of draws so far (calls do not add to it); a round has at most 70.
    public int Turns { get; private set; }
    public int WallRemaining => _started ? _wall.LiveCount : 0;

    // How many pon/chi calls have been made this round.
    public int CallCount { get; private set; }

    // Seat whose turn comes next (or who just won / just discarded last when the round ended).
    public int CurrentSeat => _currentPlayerIndex;

    // The last tile thrown, until another player calls it (then both are null).
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

    // True while the round is paused for the human to call the last discard or pass.
    public bool AwaitingHumanCall => _started && !IsFinished && _state == TurnState.CallDecision;

    // True when the next step is a draw; false when the seat to act discards without drawing (after a call).
    public bool NextActionIsDraw => _state == TurnState.Draw;

    // What the human may call right now; null unless AwaitingHumanCall.
    public CallOptions? PendingCall { get; private set; }

    // Tiles the current player may not discard right after a call (swap-calling rule).
    public IReadOnlyList<Tile> ForbiddenDiscards => _forbidden;

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
    // Pass a seat (0-3) to let a person choose that seat's discards and calls; the other seats stay bots.
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
        _forbidden = new List<Tile>();
        PendingCall = null;
        CallCount = 0;
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

    // Plays one step of the round: a draw and discard, or the discard that follows a call.
    // Stops early when the human must discard or decide on a call. Does nothing once the round is over.
    public void PlayTurn()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Call StartRound() first.");
        }

        if (IsFinished || AwaitingHumanDiscard || AwaitingHumanCall)
        {
            return;
        }

        RunUntilPause(playAtLeastOnce: true);
    }

    // The human throws a tile from their hand; the turn then finishes (ron and call checks) and passes on.
    public void DiscardHuman(Tile tile)
    {
        if (!AwaitingHumanDiscard)
        {
            throw new InvalidOperationException("The engine is not waiting for a human discard.");
        }

        var player = _players[_currentPlayerIndex];
        if (!player.Hand.Tiles.Contains(tile))
        {
            throw new ArgumentException("The hand does not contain that tile.", nameof(tile));
        }

        if (!IsDiscardAllowed(tile))
        {
            throw new ArgumentException("That tile can't be discarded right after a call (swap-calling rule).", nameof(tile));
        }

        player.Hand.Remove(tile);
        CommitDiscard(player, tile);
        RunUntilPause(playAtLeastOnce: false);
    }

    // False for tiles barred by the swap-calling rule (unless nothing else is left in the hand).
    public bool IsDiscardAllowed(Tile tile)
    {
        if (_forbidden.Count == 0 || !_forbidden.Contains(tile))
        {
            return true;
        }

        var hand = _players[_currentPlayerIndex].Hand;
        return hand.Tiles.All(t => _forbidden.Contains(t));
    }

    // Discards that keep the human closest to winning, honouring the swap-calling rule.
    public IReadOnlyList<Tile> SuggestHumanDiscards()
    {
        if (HumanSeat is null)
        {
            throw new InvalidOperationException("There is no human seat.");
        }

        return _players[HumanSeat.Value].Hand.SuggestDiscards(_forbidden);
    }

    public void HumanPon()
    {
        RequireCallDecision();
        if (!PendingCall!.CanPon)
        {
            throw new InvalidOperationException("Pon is not available for this discard.");
        }

        ExecuteCall(_players[HumanSeat!.Value], MeldType.Pon, null);
    }

    public void HumanChi(ChiOption option)
    {
        RequireCallDecision();
        if (!PendingCall!.ChiOptions.Contains(option))
        {
            throw new ArgumentException("That sequence is not available for this discard.", nameof(option));
        }

        ExecuteCall(_players[HumanSeat!.Value], MeldType.Chi, option);
    }

    public void HumanPass()
    {
        RequireCallDecision();
        PendingCall = null;

        if (!ResolveCalls(humanPassed: true))
        {
            _state = TurnState.NextPlayer;
        }

        RunUntilPause(playAtLeastOnce: false);
    }

    private void RequireCallDecision()
    {
        if (!AwaitingHumanCall || PendingCall is null)
        {
            throw new InvalidOperationException("The engine is not waiting for a call decision.");
        }
    }

    // Runs phases until a draw is next, a call just happened, the round ended, or the human must act.
    private void RunUntilPause(bool playAtLeastOnce)
    {
        _stepBreak = false;

        if (playAtLeastOnce)
        {
            Advance();
        }

        while (!IsFinished && _state != TurnState.Draw && !AwaitingHumanDiscard && !AwaitingHumanCall && !_stepBreak)
        {
            Advance();
        }
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

                CommitDiscard(player, player.Hand.DiscardBest(_rng, _forbidden));
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

                if (!ResolveCalls(humanPassed: false))
                {
                    _state = TurnState.NextPlayer;
                }

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

            case TurnState.CallDecision:
                // Paused: the human answers through HumanPon, HumanChi or HumanPass.
                return;
        }
    }

    private void CommitDiscard(Player player, Tile discarded)
    {
        _discards.Add(discarded);
        player.AddDiscard(discarded);
        LastDiscard = discarded;
        LastDiscardSeat = player.Seat;
        _forbidden = new List<Tile>();
        Log($"     {Label(player)} discards {discarded} | discards so far {_discards.Count}");
        _state = TurnState.WaitPhase;
    }

    // Decides who, if anyone, calls the last discard. Returns true when the state was changed
    // (a bot called, or the human is being asked); false when nobody calls.
    // Priority follows the rules: ron (already checked) > pon > chi, and only the player to the
    // discarder's left may chi. The last discard of the round can't be called.
    private bool ResolveCalls(bool humanPassed)
    {
        if (_wall.LiveCount == 0 || LastDiscard is null || LastDiscardSeat is null)
        {
            return false;
        }

        var tile = LastDiscard;
        var from = LastDiscardSeat.Value;
        var chiSeat = (from + 1) % 4;

        int? ponSeat = null;
        for (var seat = 0; seat < 4; seat++)
        {
            if (seat != from && _players[seat].Hand.CanPon(tile))
            {
                ponSeat = seat;
                break;
            }
        }

        var chiOptions = _players[chiSeat].Hand.ChiOptions(tile);

        // 1. A bot that can pon does so when it brings the hand closer to winning.
        if (ponSeat is int ps && ps != HumanSeat)
        {
            var hand = _players[ps].Hand;
            if (hand.ShantenAfterPon(tile) < hand.GetShanten())
            {
                ExecuteCall(_players[ps], MeldType.Pon, null);
                return true;
            }
        }

        // 2. The human gets to decide on anything they could call (pon beats a bot's chi).
        if (!humanPassed && HumanSeat is int human && human != from)
        {
            var canPon = ponSeat == human;
            var humanChi = human == chiSeat ? chiOptions : Array.Empty<ChiOption>();
            if (canPon || humanChi.Count > 0)
            {
                PendingCall = new CallOptions(tile, from, canPon, humanChi);
                _state = TurnState.CallDecision;
                return true;
            }
        }

        // 3. Otherwise the next bot in turn order may chi, picking the option that helps most.
        if (chiSeat != HumanSeat && chiOptions.Count > 0)
        {
            var hand = _players[chiSeat].Hand;
            var current = hand.GetShanten();
            var best = chiOptions.OrderBy(option => hand.ShantenAfterChi(tile, option)).First();
            if (hand.ShantenAfterChi(tile, best) < current)
            {
                ExecuteCall(_players[chiSeat], MeldType.Chi, best);
                return true;
            }
        }

        return false;
    }

    // Moves the last discard from its river into the caller's meld; the caller then discards.
    private void ExecuteCall(Player caller, MeldType type, ChiOption? option)
    {
        var tile = LastDiscard!;
        var fromPlayer = _players[LastDiscardSeat!.Value];
        fromPlayer.RemoveLastDiscard();

        var meld = type == MeldType.Pon
            ? caller.Hand.Pon(tile, fromPlayer.Seat)
            : caller.Hand.Chi(tile, option!, fromPlayer.Seat);

        _forbidden = Meld.ForbiddenDiscards(type, tile, meld.Tiles).ToList();
        Log($"     {Label(caller)} {(type == MeldType.Pon ? "pons" : "chis")} {tile} from {Label(fromPlayer)} | {caller.Hand}");

        CallCount++;
        PendingCall = null;
        LastDiscard = null;
        LastDiscardSeat = null;
        LastDrawnTile = null;
        _currentPlayerIndex = caller.Seat;
        _state = TurnState.Discard;
        _stepBreak = true;
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
