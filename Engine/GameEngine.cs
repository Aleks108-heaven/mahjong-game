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
    private const int MaxKans = 4;

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
    private bool _lastDrawWasReplacement;
    private int _pendingDora;

    public IReadOnlyList<Player> Players => _players;

    // Every tile thrown this round, in order, including ones that were later called.
    public IReadOnlyList<Tile> Discards => _discards;

    public bool IsFinished => Result is not null;
    public RoundResult? Result { get; private set; }

    // Number of draws from the live wall so far (calls and kan replacement draws do not add to it).
    // A round has at most 70, fewer once kans have moved tiles into the dead wall.
    public int Turns { get; private set; }
    public int WallRemaining => _started ? _wall.LiveCount : 0;

    // How many calls of another player's discard (pon, chi, open kan) have been made this round.
    public int CallCount { get; private set; }

    // How many kans (open, closed or added) have been made this round. At most four.
    public int KanCount { get; private set; }

    // Seat whose turn comes next (or who just won / just discarded last when the round ended).
    public int CurrentSeat => _currentPlayerIndex;

    // The wind of the round. There are no rounds yet, so it is always East (0).
    public int RoundWind => 0;

    // Dora indicators showing right now, and the tiles they make dora.
    public IReadOnlyList<Tile> DoraIndicators => _started ? _wall.DoraIndicators : Array.Empty<Tile>();
    public IReadOnlyList<Tile> DoraTiles => DoraIndicators.Select(Dora.FromIndicator).ToList();

    // The last tile thrown, until another player calls it (then both are null).
    public Tile? LastDiscard { get; private set; }
    public int? LastDiscardSeat { get; private set; }
    public Tile? WinningTile { get; private set; }

    // The yaku of the winning hand and how many dora it holds; null until somebody wins.
    public YakuResult? WinningYaku { get; private set; }
    public int WinningDora { get; private set; }

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

    // Kans the human could declare instead of discarding (call HumanDeclareKan); empty unless AwaitingHumanDiscard.
    public IReadOnlyList<KanOption> HumanKanOptions =>
        AwaitingHumanDiscard && CanKanNow ? _players[HumanSeat!.Value].Hand.KanOptions() : Array.Empty<KanOption>();

    // Tiles the current player may not discard right after a call (swap-calling rule).
    public IReadOnlyList<Tile> ForbiddenDiscards => _forbidden;

    // Raised for every log line, so a UI can show the same text the console prints.
    public event Action<string>? Logged;

    // Kans need a tile left in the live wall to top the dead wall up, and no more than four per round.
    private bool CanKanNow => _started && KanCount < MaxKans && _wall.LiveCount > 0 && _wall.ReplacementsLeft > 0;

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

    // A player is furiten when a tile that would win their hand is in their own discards (even one that was
    // called). They can still win by tsumo but not by ron. Only meaningful for a tenpai hand between turns.
    public bool IsFuriten(int seat)
    {
        var player = _players[seat];
        return player.Hand.Waits().Any(wait => player.Thrown.Contains(wait));
    }

    // Deals a fresh round. Safe to call again to start another round with the same random stream.
    // Pass a seat (0-3) to let a person choose that seat's discards and calls; the other seats stay bots.
    // Pass a wall to play a prepared deal (see Wall.Stacked) instead of a shuffled one.
    public void StartRound(int? humanSeat = null, Wall? wall = null)
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
        KanCount = 0;
        _pendingDora = 0;
        _lastDrawWasReplacement = false;
        for (var i = 0; i < 4; i++)
        {
            _players.Add(new Player(i));
        }

        Result = null;
        Turns = 0;
        LastDiscard = null;
        LastDiscardSeat = null;
        WinningTile = null;
        WinningYaku = null;
        WinningDora = 0;

        SetupRound(wall);
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

    // The human declares a closed or added kan instead of discarding. They then draw a replacement tile
    // and must discard (or win) as usual.
    public void HumanDeclareKan(KanOption option)
    {
        if (!AwaitingHumanDiscard)
        {
            throw new InvalidOperationException("The engine is not waiting for a human discard.");
        }

        if (!HumanKanOptions.Contains(option))
        {
            throw new ArgumentException("That kan is not available.", nameof(option));
        }

        var roundEnded = DeclareKan(_players[_currentPlayerIndex], option);
        if (!roundEnded)
        {
            RunUntilPause(playAtLeastOnce: false);
        }
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

    // Takes the last discard as an open kan, draws a replacement tile and leaves the human to discard.
    public void HumanKan()
    {
        RequireCallDecision();
        if (!PendingCall!.CanKan)
        {
            throw new InvalidOperationException("Kan is not available for this discard.");
        }

        ExecuteCall(_players[HumanSeat!.Value], MeldType.Daiminkan, null);
        RunUntilPause(playAtLeastOnce: false);
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
                _lastDrawWasReplacement = false;
                LastDrawnTile = tile;
                LastDrawSeat = player.Seat;
                player.Hand.Add(tile);
                player.Hand.Sort();
                Log($"T{Turns,-3} {Label(player)} draws {tile,-3} | {player.Hand} | shanten {player.Hand.GetShanten()} | wall {_wall.LiveCount}");
                _state = TurnState.ActionPhase;
                break;

            case TurnState.ActionPhase:
                if (TryTsumo(player))
                {
                    return;
                }

                if (player.Seat != HumanSeat && TryBotKan(player))
                {
                    // Either the kan was robbed (round over) or a replacement tile is waiting to be looked at.
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

                CommitDiscard(player, player.Hand.DiscardBest(
                    _rng,
                    _forbidden,
                    preferEdges: player.Hand.IsPlayingForSimples(player.Seat, RoundWind)));
                break;

            case TurnState.WaitPhase:
                // Other players may declare ron, checked in turn order from the discarder's left.
                for (var offset = 1; offset < 4; offset++)
                {
                    var other = _players[(_currentPlayerIndex + offset) % 4];
                    if (other.Hand.CanWinOn(LastDiscard!) && TryRon(other, player, LastDiscard!, chankan: false))
                    {
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
                // Paused: the human answers through HumanPon, HumanChi, HumanKan or HumanPass.
                return;
        }
    }

    // The yaku of a hand that wins on winTile. For a ron the tile is not in the hand yet.
    private YakuResult EvaluateWin(Player winner, Tile winTile, bool tsumo, bool chankan)
    {
        var tiles = tsumo ? winner.Hand.Tiles.ToList() : winner.Hand.Tiles.Append(winTile).ToList();
        var context = new WinContext(
            IsTsumo: tsumo,
            SeatWind: winner.Seat,
            RoundWind: RoundWind,
            IsHaitei: tsumo && !_lastDrawWasReplacement && _wall.LiveCount == 0,
            IsHoutei: !tsumo && !chankan && _wall.LiveCount == 0,
            IsRinshan: tsumo && _lastDrawWasReplacement,
            IsChankan: chankan);
        return YakuEvaluator.Evaluate(tiles, winner.Hand.Melds, winTile, context);
    }

    // A complete hand only wins with at least one yaku; otherwise play goes on.
    private bool TryTsumo(Player player)
    {
        if (!player.Hand.IsComplete())
        {
            return false;
        }

        var yaku = EvaluateWin(player, LastDrawnTile!, tsumo: true, chankan: false);
        if (!yaku.HasYaku)
        {
            Log($"     {Label(player)} has a complete hand but no yaku, so it can't win: {player.Hand}");
            return false;
        }

        RecordWin(player, yaku);
        Log($"{Label(player)} wins by tsumo with {player.Hand} | {WinSummary()}");
        Result = new RoundResult(RoundOutcome.Tsumo, player.Seat, null, Turns);
        return true;
    }

    // Ron needs a yaku and a player who is not furiten. Returns true when the round ends with this win.
    private bool TryRon(Player winner, Player loser, Tile tile, bool chankan)
    {
        var yaku = EvaluateWin(winner, tile, tsumo: false, chankan);
        if (!yaku.HasYaku)
        {
            Log($"     {Label(winner)} is waiting on {tile} but has no yaku, so it can't ron.");
            return false;
        }

        if (IsFuriten(winner.Seat))
        {
            Log($"     {Label(winner)} could ron on {tile} but is furiten: a winning tile is in its own discards.");
            return false;
        }

        winner.Hand.Add(tile);
        winner.Hand.Sort();
        WinningTile = tile;
        RecordWin(winner, yaku);
        var how = chankan ? "ron (robbing the kan)" : "ron";
        Log($"{Label(winner)} wins by {how} on {tile} from {Label(loser)} with {winner.Hand} | {WinSummary()}");
        Result = new RoundResult(RoundOutcome.Ron, winner.Seat, loser.Seat, Turns);
        _currentPlayerIndex = winner.Seat;
        return true;
    }

    private void RecordWin(Player winner, YakuResult yaku)
    {
        WinningYaku = yaku;
        WinningDora = Dora.Count(winner.Hand.Tiles, winner.Hand.Melds, DoraIndicators);
    }

    private string WinSummary()
    {
        var dora = WinningDora > 0 ? $", dora {WinningDora}" : string.Empty;
        return $"{WinningYaku} = {WinningYaku!.Han} han{dora}";
    }

    private void CommitDiscard(Player player, Tile discarded)
    {
        // A kan made from a discard or an added tile turns its dora indicator over once the kan player discards.
        while (_pendingDora > 0)
        {
            _pendingDora--;
            RevealDoraIndicator();
        }

        _discards.Add(discarded);
        player.AddDiscard(discarded);
        LastDiscard = discarded;
        LastDiscardSeat = player.Seat;
        _forbidden = new List<Tile>();
        Log($"     {Label(player)} discards {discarded} | discards so far {_discards.Count}");
        _state = TurnState.WaitPhase;
    }

    private void RevealDoraIndicator()
    {
        if (_wall.RevealDoraIndicator() is { } indicator)
        {
            Log($"     New dora indicator {indicator}: {Dora.FromIndicator(indicator)} is dora");
        }
    }

    // Decides who, if anyone, calls the last discard. Returns true when the state was changed
    // (a bot called, or the human is being asked); false when nobody calls.
    // Priority follows the rules: ron (already checked) > pon or kan > chi, and only the player to the
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

        // At most one other player can hold two or more copies of the discard.
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

        // 1. A bot that can kan or pon does so when it helps and a yaku stays possible.
        if (ponSeat is int ps && ps != HumanSeat)
        {
            var hand = _players[ps].Hand;
            var current = hand.GetShanten();
            var sameFour = new[] { tile, tile, tile, tile };

            if (CanKanNow
                && hand.CanDaiminkan(tile)
                && hand.ShantenAfterDaiminkan(tile) <= current
                && hand.OpenCallKeepsAYaku(sameFour, new[] { tile, tile, tile }, true, ps, RoundWind))
            {
                ExecuteCall(_players[ps], MeldType.Daiminkan, null);
                return true;
            }

            if (hand.ShantenAfterPon(tile) < current
                && hand.OpenCallKeepsAYaku(new[] { tile, tile, tile }, new[] { tile, tile }, true, ps, RoundWind))
            {
                ExecuteCall(_players[ps], MeldType.Pon, null);
                return true;
            }
        }

        // 2. The human gets to decide on anything they could call (pon beats a bot's chi).
        if (!humanPassed && HumanSeat is int human && human != from)
        {
            var canPon = ponSeat == human;
            var canKan = canPon && CanKanNow && _players[human].Hand.CanDaiminkan(tile);
            var humanChi = human == chiSeat ? chiOptions : Array.Empty<ChiOption>();
            if (canPon || humanChi.Count > 0)
            {
                PendingCall = new CallOptions(tile, from, canPon, humanChi, canKan);
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
            var run = new[] { tile, best.A, best.B }.OrderBy(t => t).ToList();
            if (hand.ShantenAfterChi(tile, best) < current
                && hand.OpenCallKeepsAYaku(run, new[] { best.A, best.B }, false, chiSeat, RoundWind))
            {
                ExecuteCall(_players[chiSeat], MeldType.Chi, best);
                return true;
            }
        }

        return false;
    }

    // Moves the last discard from its river into the caller's meld. After a pon or chi the caller discards;
    // after a kan the caller first draws a replacement tile.
    private void ExecuteCall(Player caller, MeldType type, ChiOption? option)
    {
        var tile = LastDiscard!;
        var fromPlayer = _players[LastDiscardSeat!.Value];
        fromPlayer.RemoveLastDiscard();

        var meld = type switch
        {
            MeldType.Pon => caller.Hand.Pon(tile, fromPlayer.Seat),
            MeldType.Daiminkan => caller.Hand.Daiminkan(tile, fromPlayer.Seat),
            _ => caller.Hand.Chi(tile, option!, fromPlayer.Seat)
        };

        var verb = type switch
        {
            MeldType.Pon => "pons",
            MeldType.Daiminkan => "kans",
            _ => "chis"
        };
        Log($"     {Label(caller)} {verb} {tile} from {Label(fromPlayer)} | {caller.Hand}");

        PendingCall = null;
        LastDiscard = null;
        LastDiscardSeat = null;
        LastDrawnTile = null;
        _currentPlayerIndex = caller.Seat;
        _stepBreak = true;
        CallCount++;

        if (type == MeldType.Daiminkan)
        {
            _forbidden = new List<Tile>();
            FinishKan(caller, immediateDora: false);
            return;
        }

        _forbidden = Meld.ForbiddenDiscards(type, tile, meld.Tiles).ToList();
        _state = TurnState.Discard;
    }

    // A bot declares a closed or added kan when it doesn't make the hand worse. Returns true if one was declared.
    private bool TryBotKan(Player player)
    {
        if (!CanKanNow)
        {
            return false;
        }

        var hand = player.Hand;
        var options = hand.KanOptions();
        if (options.Count == 0)
        {
            return false;
        }

        var current = hand.BestDiscardShanten();
        foreach (var option in options)
        {
            if (hand.ShantenAfterKan(option) <= current)
            {
                DeclareKan(player, option);
                return true;
            }
        }

        return false;
    }

    // Declares a closed or added kan for the player whose turn it is. Returns true if the round ended because
    // another player robbed an added kan.
    private bool DeclareKan(Player player, KanOption option)
    {
        if (option.Type == MeldType.Kakan)
        {
            // Another player waiting on this very tile may rob the kan.
            for (var offset = 1; offset < 4; offset++)
            {
                var other = _players[(player.Seat + offset) % 4];
                if (other.Hand.CanWinOn(option.Tile) && TryRon(other, player, option.Tile, chankan: true))
                {
                    return true;
                }
            }

            player.Hand.Kakan(option.Tile);
            Log($"     {Label(player)} adds {option.Tile} to its pon (kan) | {player.Hand}");
        }
        else
        {
            player.Hand.Ankan(option.Tile);
            Log($"     {Label(player)} declares a closed kan of {option.Tile} | {player.Hand}");
        }

        _forbidden = new List<Tile>();
        FinishKan(player, immediateDora: option.Type == MeldType.Ankan);
        return false;
    }

    // After any kan: count it, handle the dora indicator and give the player the replacement tile.
    private void FinishKan(Player player, bool immediateDora)
    {
        KanCount++;
        if (immediateDora)
        {
            RevealDoraIndicator();
        }
        else
        {
            _pendingDora++;
        }

        var tile = _wall.DrawReplacement();
        _lastDrawWasReplacement = true;
        LastDrawnTile = tile;
        LastDrawSeat = player.Seat;
        player.Hand.Add(tile);
        player.Hand.Sort();
        Log($"     {Label(player)} draws replacement tile {tile,-3} | {player.Hand} | shanten {player.Hand.GetShanten()} | wall {_wall.LiveCount}");
        _state = TurnState.ActionPhase;
    }

    private void SetupRound(Wall? wall)
    {
        _wall = wall ?? new Wall(_rng);

        foreach (var player in _players)
        {
            for (var i = 0; i < 13; i++)
            {
                player.Hand.Add(_wall.Draw()!);
            }

            player.Hand.Sort();
            Log($"{Label(player)} starting hand: {player.Hand}");
        }

        var indicator = _wall.DoraIndicators[0];
        Log($"Dora indicator {indicator}: {Dora.FromIndicator(indicator)} is dora");
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
