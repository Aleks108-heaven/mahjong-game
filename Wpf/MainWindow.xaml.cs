using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TestMahjongGame.Engine;

namespace MahjongTable;

public partial class MainWindow : Window
{
    private const double HandTileWidth = 34;
    private const double RiverTileWidth = 30;
    private const double MeldTileWidth = 28;

    private static readonly string[] SeatNames = { "East", "South", "West", "North" };
    private static readonly string[] SeatNamesShort = { "Схід", "Півд.", "Зах.", "Півн." };
    private static readonly string[] WindKanji = { "東", "南", "西", "北" };

    private static readonly Brush PanelBrush = Frozen("#1B4D3C");
    private static readonly Brush PanelEdge = Frozen("#2F7059");
    private static readonly Brush NextEdge = Frozen("#FFFFFF");
    private static readonly Brush WinnerEdge = Frozen("#FFD84D");
    private static readonly Brush YouEdge = Frozen("#3D9BFF");
    private static readonly Brush TextColour = Frozen("#F2EFE6");
    private static readonly Brush MutedColour = Frozen("#B9CFC4");
    private static readonly Brush GoldColour = Frozen("#E8B84A");
    private static readonly Brush ErrorColour = Frozen("#FFB4A8");

    private readonly DispatcherTimer _timer = new();
    private readonly List<SeatPanel> _panels = new();
    private GameEngine _engine = null!;
    private int _seed;
    private bool _ready;
    private bool _showHint;

    private int? HumanSeat => _engine?.HumanSeat;

    public MainWindow()
    {
        InitializeComponent();

        // Fit the window to the usable screen area on smaller displays.
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, work.Width * 0.96);
        Height = Math.Min(Height, work.Height * 0.96);

        for (var seat = 0; seat < 4; seat++)
        {
            var panel = new SeatPanel(seat);
            _panels.Add(panel);
            PlayerGrid.Children.Add(panel.Root);
        }

        ApplyLanguage();
        _timer.Interval = TimeSpan.FromMilliseconds(SpeedSlider.Value);
        _timer.Tick += (_, _) => OnTimerTick();
        Loaded += (_, _) => StartFromCommandLine();
    }

    // Optional developer arguments: MahjongTable.exe <seed> [turns-to-play]
    // With a turns argument the app starts as a spectator and plays that many turns first.
    private void StartFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && int.TryParse(args[1], out _))
        {
            SeedBox.Text = args[1];
        }

        var hasTurns = args.Length > 2 && int.TryParse(args[2], out _);
        if (hasTurns)
        {
            SeatWatch.IsChecked = true;
        }

        _ready = true;
        NewRound();

        if (hasTurns && int.TryParse(args[2], out var turns))
        {
            for (var i = 0; i < turns && !_engine.IsFinished; i++)
            {
                _engine.PlayTurn();
            }

            Refresh();
        }
    }

    private int? SelectedSeat()
    {
        foreach (RadioButton radio in SeatPicker.Children)
        {
            if (radio.IsChecked == true)
            {
                var seat = int.Parse((string)radio.Tag);
                return seat < 0 ? null : seat;
            }
        }

        return null;
    }

    private void NewRound()
    {
        _timer.Stop();
        AutoButton.IsChecked = false;

        var text = SeedBox.Text.Trim();
        if (text.Length == 0)
        {
            _seed = Random.Shared.Next(1, 1_000_000);
        }
        else if (!int.TryParse(text, out _seed))
        {
            SeedHint.Text = Loc.T("Seed must be a whole number, for example 7.");
            SeedHint.Foreground = ErrorColour;
            SeedBox.Focus();
            return;
        }

        SeedHint.Text = Loc.T("Empty = random round. A number replays the same round.");
        SeedHint.Foreground = MutedColour;

        LogBox.Clear();
        _showHint = false;
        _engine = new GameEngine(_seed, TextWriter.Null);
        _engine.Logged += line =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        };

        var human = SelectedSeat();
        _engine.StartRound(human);

        // When you sit down, the other hands start face-down; spectators see everything.
        ShowHands.IsChecked = human is null;
        Refresh();

        if (human.HasValue)
        {
            AdvanceBots();
        }
        else
        {
            NextButton.Focus();
        }
    }

    // Plays turns until it is the human's move: the first one immediately, the rest on the timer.
    private void AdvanceBots()
    {
        PlayOneTurn();
        if (!WaitingForHuman())
        {
            _timer.Start();
        }
    }

    private void OnTimerTick()
    {
        PlayOneTurn();

        if (WaitingForHuman())
        {
            _timer.Stop();
        }
    }

    // True when the round is over or paused for the human (to discard or to answer a call).
    private bool WaitingForHuman()
    {
        return _engine.IsFinished || _engine.AwaitingHumanDiscard || _engine.AwaitingHumanCall;
    }

    private void PlayOneTurn()
    {
        if (WaitingForHuman())
        {
            return;
        }

        _engine.PlayTurn();
        Refresh();

        if (_engine.IsFinished)
        {
            _timer.Stop();
            AutoButton.IsChecked = false;
        }
    }

    private void OnTileChosen(Tile tile)
    {
        if (!_engine.AwaitingHumanDiscard)
        {
            return;
        }

        _engine.DiscardHuman(tile);
        _showHint = false;
        ResumeAfterHumanAction();
    }

    private void OnPon()
    {
        if (!_engine.AwaitingHumanCall || !_engine.PendingCall!.CanPon)
        {
            return;
        }

        _engine.HumanPon();
        ResumeAfterHumanAction();
    }

    private void OnChi(ChiOption option)
    {
        if (!_engine.AwaitingHumanCall)
        {
            return;
        }

        _engine.HumanChi(option);
        ResumeAfterHumanAction();
    }

    private void OnKan()
    {
        if (!_engine.AwaitingHumanCall || !_engine.PendingCall!.CanKan)
        {
            return;
        }

        _engine.HumanKan();
        ResumeAfterHumanAction();
    }

    private void OnDeclareKan(KanOption option)
    {
        if (!_engine.AwaitingHumanDiscard)
        {
            return;
        }

        _engine.HumanDeclareKan(option);
        ResumeAfterHumanAction();
    }

    private void OnPass()
    {
        if (!_engine.AwaitingHumanCall)
        {
            return;
        }

        _engine.HumanPass();
        ResumeAfterHumanAction();
    }

    // After the human discards, calls or passes: show the result and let the bots carry on if it is their move.
    private void ResumeAfterHumanAction()
    {
        _showHint = false;
        Refresh();

        if (WaitingForHuman())
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
        }
    }

    private void Refresh()
    {
        var finished = _engine.IsFinished;
        var result = _engine.Result;
        var human = _engine.HumanSeat;
        var awaiting = _engine.AwaitingHumanDiscard;
        var awaitingCall = _engine.AwaitingHumanCall;

        TurnText.Text = Loc.T("Turn {0}", _engine.Turns);
        ShowDora();
        WallText.Text = Loc.T("Wall {0} left", _engine.WallRemaining);
        SeedText.Text = Loc.T("Seed {0}", _seed);

        foreach (var panel in _panels)
        {
            var player = _engine.Players[panel.Seat];
            var isYou = human == panel.Seat;
            var canClick = isYou && awaiting;
            var reveal = finished || isYou || ShowHands.IsChecked == true;
            var isWinner = finished && result!.WinnerSeat == panel.Seat;
            var isNext = !finished && !awaitingCall && _engine.CurrentSeat == panel.Seat;
            var lastRiverIndex = _engine.LastDiscardSeat == panel.Seat ? player.Discards.Count - 1 : -1;
            var winTileIndex = isWinner && result!.Outcome == RoundOutcome.Ron
                ? FindLastIndex(player.Hand.Tiles, _engine.WinningTile!)
                : -1;
            var drawnIndex = canClick ? FindLastIndex(player.Hand.Tiles, _engine.LastDrawnTile!) : -1;
            var hints = canClick && _showHint ? _engine.SuggestHumanDiscards() : new List<Tile>();

            panel.Title.Text = Loc.T("Player {0} · {1}", panel.Seat + 1, SeatName(panel.Seat)) + (isYou ? Loc.T(" · You") : string.Empty);

            panel.Hand.Children.Clear();
            for (var i = 0; i < player.Hand.Tiles.Count; i++)
            {
                var tile = player.Hand.Tiles[i];
                var mark = TileMark.None;
                if (i == winTileIndex)
                {
                    mark = TileMark.WinningTile;
                }
                else if (hints.Contains(tile))
                {
                    mark = TileMark.Hint;
                }
                else if (i == drawnIndex)
                {
                    mark = TileMark.Drawn;
                }

                panel.Hand.Children.Add(canClick
                    ? ClickableTile(player.Hand, tile, mark)
                    : TileView.Create(tile, HandTileWidth, !reveal, mark));
            }

            // Open melds (chi/pon) are public, so they are always face-up.
            foreach (var meld in player.Hand.Melds)
            {
                panel.Hand.Children.Add(MeldView(meld));
            }

            panel.River.Children.Clear();
            for (var i = 0; i < player.Discards.Count; i++)
            {
                var mark = i == lastRiverIndex && !finished ? TileMark.LastDiscard : TileMark.None;
                panel.River.Children.Add(TileView.Create(player.Discards[i], RiverTileWidth, false, mark));
            }

            panel.RiverCount.Text = Loc.T("Discards ({0})", player.Discards.Count);

            if (isWinner)
            {
                panel.Status.Text = Loc.T(result!.Outcome == RoundOutcome.Tsumo ? "WINNER · Tsumo" : "WINNER · Ron");
                panel.Status.Foreground = GoldColour;
            }
            else if (reveal)
            {
                var shanten = player.Hand.GetShanten();
                var furiten = shanten == 0 && player.Hand.TotalTiles == 13 && _engine.IsFuriten(panel.Seat);
                panel.Status.Text = furiten ? Loc.T("Tenpai · Furiten") : shanten == 0 ? Loc.T("Tenpai") : Loc.T("Shanten {0}", shanten);
                panel.Status.Foreground = shanten == 0 ? GoldColour : MutedColour;
            }
            else
            {
                panel.Status.Text = Loc.T("Hand hidden");
                panel.Status.Foreground = MutedColour;
            }

            panel.Turn.Text = canClick
                ? Loc.T("▶ Your move")
                : isNext
                    ? Loc.T(_engine.NextActionIsDraw ? "▶ Draws next" : "▶ Discards next")
                    : string.Empty;
            panel.Root.BorderBrush = isWinner ? WinnerEdge : isNext ? NextEdge : isYou ? YouEdge : PanelEdge;
            panel.Root.BorderThickness = new Thickness(isWinner || isNext ? 3 : isYou ? 2 : 1);
        }

        var spectating = human is null;
        NextButton.IsEnabled = !finished && spectating;
        AutoButton.IsEnabled = !finished && spectating;
        SpeedLabel.Text = Loc.T(spectating ? "Speed: {0} ms per turn" : "Speed: {0} ms per bot turn", (int)SpeedSlider.Value);

        ShowTurnBanner(awaiting);
        ShowCallBanner(awaitingCall);
        ShowResult();

        if (finished && !SeedBox.IsKeyboardFocusWithin && !SeatPicker.IsKeyboardFocusWithin)
        {
            // The round is over, so the natural next step is a new one.
            NewButton.Focus();
        }

        if (awaiting)
        {
            // Put keyboard focus on the first tile so Enter and the arrow/Tab keys work straight away.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_engine.AwaitingHumanDiscard && !HintButton.IsKeyboardFocusWithin && PlayerHandHasNoFocus())
                {
                    FirstHandButton()?.Focus();
                }
            });
        }
        else if (awaitingCall)
        {
            // Focus Pass, the safe default, so a stray Enter never makes a call by accident.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_engine.AwaitingHumanCall)
                {
                    CallButtons.Children.OfType<Button>().LastOrDefault()?.Focus();
                }
            });
        }
    }

    // A set of three or four face-up tiles in a dark tray; the called tile has an orange outline.
    private static FrameworkElement MeldView(Meld meld)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var calledShown = false;
        for (var i = 0; i < meld.Tiles.Count; i++)
        {
            var tile = meld.Tiles[i];
            var closedKan = meld.Type == MeldType.Ankan;
            var isCalled = !closedKan && !calledShown && tile.Equals(meld.CalledTile);
            calledShown |= isCalled;

            // A closed kan shows its two outer tiles face-down, as at a real table.
            var faceDown = closedKan && (i == 0 || i == 3);
            row.Children.Add(TileView.Create(tile, MeldTileWidth, faceDown, isCalled ? TileMark.LastDiscard : TileMark.None));
        }

        var kind = Loc.T(meld.Type switch
        {
            MeldType.Pon => "Pon",
            MeldType.Chi => "Chi",
            MeldType.Ankan => "Closed kan",
            MeldType.Kakan => "Added kan",
            _ => "Open kan"
        });
        var description = meld.Type == MeldType.Ankan
            ? Loc.T("{0} of {1}", kind, meld.CalledTile)
            : Loc.T("{0} of {1}, called {2} from Player {3}", kind, string.Join(" ", meld.Tiles), meld.CalledTile, meld.FromSeat + 1);
        var tray = new Border
        {
            Background = Frozen("#12372B"),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(3),
            Margin = new Thickness(8, 0, 0, 0),
            Child = row,
            ToolTip = description
        };
        AutomationProperties.SetName(tray, description);
        return tray;
    }
    private void ShowCallBanner(bool awaitingCall)
    {
        CallBanner.Visibility = awaitingCall ? Visibility.Visible : Visibility.Collapsed;
        CallButtons.Children.Clear();
        if (!awaitingCall)
        {
            return;
        }

        var call = _engine.PendingCall!;
        CallTitle.Text = Loc.T("{0} discarded {1}. Call it?", SeatLabel(call.FromSeat), TileView.Describe(call.Discard));

        var options = new List<string>();
        if (call.CanPon)
        {
            options.Add(Loc.T("pon (three of a kind)"));
            CallButtons.Children.Add(CallButton(Loc.T("Pon (P)"), () => OnPon(), primary: true));
        }

        if (call.CanKan)
        {
            options.Add(Loc.T("kan (four of a kind; you then draw a replacement tile)"));
            CallButtons.Children.Add(CallButton(Loc.T("Kan (K)"), () => OnKan(), primary: false));
        }

        for (var i = 0; i < call.ChiOptions.Count; i++)
        {
            var option = call.ChiOptions[i];
            var tiles = new[] { call.Discard, option.A, option.B }.OrderBy(t => t).ToList();
            var label = Loc.T("Chi {0}", string.Join(" ", tiles)) + (i == 0 ? " (C)" : string.Empty);
            if (i == 0)
            {
                options.Add(Loc.T("chi (a run)"));
            }

            CallButtons.Children.Add(CallButton(label, () => OnChi(option), primary: !call.CanPon && i == 0));
        }

        CallButtons.Children.Add(CallButton(Loc.T("Pass (S)"), () => OnPass(), primary: false));
        CallHint.Text = Loc.T(
            "You can {0}. Calling takes the tile and you discard next; the called tile stays locked. Pass is the focused default.",
            string.Join(Loc.T(" or "), options));
    }
    private Button CallButton(string text, Action onClick, bool primary)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)FindResource(primary ? "PrimaryButton" : "ActionButton"),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(8, 0, 0, 0)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Button ClickableTile(PlayerHand hand, Tile tile, TileMark mark)
    {
        var description = TileView.Describe(tile);
        var after = hand.ShantenAfterDiscard(tile);
        var outcome = after switch
        {
            < 0 => Loc.T("completes your hand"),
            0 => Loc.T("tenpai"),
            _ => Loc.T("shanten {0}", after)
        };

        var button = new Button
        {
            Content = TileView.Create(tile, HandTileWidth, false, mark),
            Style = (Style)FindResource("TileButton"),
            ToolTip = Loc.T("Discard {0}: {1}", description, outcome)
        };
        AutomationProperties.SetName(button, Loc.T("Discard {0}", description));

        if (!_engine.IsDiscardAllowed(tile))
        {
            // Swap-calling rule: the tile you just called (or its mirror in a chi) is locked this turn.
            button.IsEnabled = false;
            button.Opacity = 0.4;
            button.ToolTip = Loc.T("{0} is locked: you can't discard it right after calling", description);
            AutomationProperties.SetName(button, Loc.T("{0}, locked this turn", description));
        }

        button.Click += (_, _) => OnTileChosen(tile);
        return button;
    }
    private Button? FirstHandButton()
    {
        return _panels[_engine.HumanSeat!.Value].Hand.Children.OfType<Button>().FirstOrDefault(b => b.IsEnabled);
    }

    private bool PlayerHandHasNoFocus()
    {
        var hand = _panels[_engine.HumanSeat!.Value].Hand;
        return !hand.IsKeyboardFocusWithin && !SeedBox.IsKeyboardFocusWithin;
    }

    private void ShowTurnBanner(bool awaiting)
    {
        TurnBanner.Visibility = awaiting ? Visibility.Visible : Visibility.Collapsed;
        if (!awaiting)
        {
            return;
        }

        KanButtons.Children.Clear();
        foreach (var option in _engine.HumanKanOptions)
        {
            var label = option.Type == MeldType.Ankan ? Loc.T("Closed kan {0}", option.Tile) : Loc.T("Add {0} to pon", option.Tile);
            KanButtons.Children.Add(CallButton(label + (KanButtons.Children.Count == 0 ? " (K)" : string.Empty), () => OnDeclareKan(option), primary: false));
        }

        var hand = _engine.Players[_engine.HumanSeat!.Value].Hand;
        var called = _engine.ForbiddenDiscards.Count > 0;
        YourMoveText.Text = called
            ? Loc.T("You called: now discard a tile.")
            : Loc.T("Your move: click a tile to discard it.");

        if (_showHint)
        {
            var suggestions = _engine.SuggestHumanDiscards();
            var best = hand.ShantenAfterDiscard(suggestions[0]);
            var goal = best == 0 ? Loc.T("tenpai") : Loc.T("shanten {0}", best);
            HintMessage.Text = Loc.T("Suggested: {0} (keeps you at {1}). Green outline marks them.", string.Join(", ", suggestions), goal);
        }
        else if (called)
        {
            var lastMeld = hand.Melds[^1];
            HintMessage.Text = lastMeld.Type == MeldType.Pon
                ? Loc.T("You can't discard the {0} you just called, and the dimmed tiles are locked this turn.", lastMeld.CalledTile)
                : Loc.T("Dimmed tiles are locked this turn: you can't discard the tile you just called, or the other end of the same run.");
        }
        else
        {
            HintMessage.Text = Loc.T("Blue outline = the tile you just drew. Hover a tile to see where it leaves you. Tab and Enter also work.");
        }
    }
    private void ShowDora()
    {
        DoraTiles.Children.Clear();
        foreach (var indicator in _engine.DoraIndicators)
        {
            DoraTiles.Children.Add(TileView.Create(indicator, 22));
        }
    }

    private void ShowResult()
    {
        if (!_engine.IsFinished)
        {
            ResultBanner.Visibility = Visibility.Collapsed;
            return;
        }

        var result = _engine.Result!;
        ResultText.Text = result.Outcome switch
        {
            RoundOutcome.Tsumo =>
                Loc.T("{0} wins by tsumo (self-drawn) on turn {1}.", SeatLabel(result.WinnerSeat!.Value), result.Turns),
            RoundOutcome.Ron =>
                Loc.T("{0} wins by ron on {1}, discarded by {2}, on turn {3}.",
                    SeatLabel(result.WinnerSeat!.Value), TileView.Describe(_engine.WinningTile!),
                    SeatLabel(result.LoserSeat!.Value), result.Turns),
            _ => Loc.T("Exhaustive draw: the wall ran out after {0} turns and nobody won.", result.Turns)
        };

        if (_engine.WinningYaku is { } yaku)
        {
            var dora = _engine.WinningDora > 0 ? Loc.T(" · dora {0}", _engine.WinningDora) : string.Empty;
            ResultYaku.Text = Loc.T("{0} = {1} han", Loc.YakuText(yaku), yaku.Han) + dora;
            ResultYaku.Visibility = Visibility.Visible;
        }
        else
        {
            ResultYaku.Visibility = Visibility.Collapsed;
        }

        ResultBanner.Visibility = Visibility.Visible;
    }
    private string SeatLabel(int seat)
    {
        var you = HumanSeat == seat ? Loc.T(" - you") : string.Empty;
        return Loc.T("Player {0} ({1}{2})", seat + 1, SeatName(seat), you);
    }

    private static string SeatName(int seat)
    {
        return Loc.T(SeatNames[seat]);
    }

    // Applies the chosen language to every fixed label in the window, then redraws the table.
    private void ApplyLanguage()
    {
        Title = Loc.T("Mahjong Table");
        TitleText.Text = Loc.T("Mahjong Table");
        SubtitleText.Text = Loc.T("Riichi engine · four bots play one round");
        DoraLabel.Text = Loc.T("Dora");
        HintButton.Content = Loc.T("Hint (H)");
        HintButton.ToolTip = Loc.T("Show the discard that keeps you closest to winning");
        ControlsLabel.Text = Loc.T("CONTROLS");
        NextButton.Content = Loc.T("Next turn  ▶");
        NextButton.ToolTip = Loc.T("Play one full turn (Right arrow)");
        AutoButton.Content = Loc.T("Auto-play");
        AutoButton.ToolTip = Loc.T("Play turns automatically (A)");
        NewButton.Content = Loc.T("New round");
        NewButton.ToolTip = Loc.T("Deal a new round (N)");
        YourSeatLabel.Text = Loc.T("YOUR SEAT");
        foreach (RadioButton radio in SeatPicker.Children)
        {
            var seat = int.Parse((string)radio.Tag);
            radio.Content = seat < 0
                ? Loc.T("Watch")
                : Loc.IsUkrainian ? SeatNamesShort[seat] : SeatNames[seat];
        }

        SeatWatch.ToolTip = Loc.T("Spectate: four bots play");
        SeatHint.Text = Loc.T("Picking a seat starts a new round. Bots' hands stay hidden.");
        LangLabel.Text = Loc.T("LANGUAGE");
        SeedLabel.Text = Loc.T("SEED (OPTIONAL)");
        SeedBox.ToolTip = Loc.T("A whole number replays the same round");
        AutomationProperties.SetName(SeedBox, Loc.T("Seed"));
        SeedHint.Text = Loc.T("Empty = random round. A number replays the same round.");
        SeedHint.Foreground = MutedColour;
        ShowHands.Content = Loc.T("Show all hands");
        LogLabel.Text = Loc.T("GAME LOG");
        AutomationProperties.SetName(LogBox, Loc.T("Game log"));
        AutomationProperties.SetName(SpeedSlider, Loc.T("Auto-play speed"));
        KeysText.Text = Loc.T("Keys: → next turn · A auto-play · N new round");
        ResultFooter.Text = Loc.T("Press New round (N) to deal again, or enter a seed to replay a round.");
        SpeedSlider_ValueChanged(SpeedSlider, null!);

        if (_engine is not null)
        {
            Refresh();
        }
    }

    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        var wanted = LangUk.IsChecked == true ? Lang.Uk : Lang.En;
        if (Loc.Current == wanted)
        {
            return;
        }

        Loc.Current = wanted;
        ApplyLanguage();
    }
    private static int FindLastIndex(IReadOnlyList<Tile> tiles, Tile tile)
    {
        for (var i = tiles.Count - 1; i >= 0; i--)
        {
            if (tiles[i].Equals(tile))
            {
                return i;
            }
        }

        return -1;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        PlayOneTurn();
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        NewRound();
    }

    private void HintButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHint();
    }

    private void ShowHint()
    {
        if (_engine is null || !_engine.AwaitingHumanDiscard)
        {
            return;
        }

        _showHint = true;
        Refresh();
    }

    private void Seat_Checked(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            NewRound();
        }
    }

    private void AutoButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine is null)
        {
            return;
        }

        if (AutoButton.IsChecked == true && !_engine.IsFinished && HumanSeat is null)
        {
            _timer.Start();
        }
        else if (HumanSeat is null)
        {
            _timer.Stop();
        }
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var ms = (int)SpeedSlider.Value;
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
        if (SpeedLabel is not null)
        {
            var spectating = _engine is null || _engine.HumanSeat is null;
            SpeedLabel.Text = Loc.T(spectating ? "Speed: {0} ms per turn" : "Speed: {0} ms per bot turn", ms);
        }
    }

    private void ShowHands_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine is not null)
        {
            Refresh();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Letter shortcuts must not fire while typing a seed.
        if (Keyboard.FocusedElement is TextBox { IsReadOnly: false })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right when NextButton.IsEnabled:
                PlayOneTurn();
                e.Handled = true;
                break;
            case Key.A when AutoButton.IsEnabled:
                AutoButton.IsChecked = AutoButton.IsChecked != true;
                e.Handled = true;
                break;
            case Key.H:
                ShowHint();
                e.Handled = true;
                break;
            case Key.P when _engine.AwaitingHumanCall:
                OnPon();
                e.Handled = true;
                break;
            case Key.C when _engine.AwaitingHumanCall:
                if (_engine.PendingCall!.ChiOptions.Count > 0)
                {
                    OnChi(_engine.PendingCall.ChiOptions[0]);
                }

                e.Handled = true;
                break;
            case Key.K when _engine.AwaitingHumanCall && _engine.PendingCall!.CanKan:
                OnKan();
                e.Handled = true;
                break;
            case Key.K when _engine.AwaitingHumanDiscard && _engine.HumanKanOptions.Count > 0:
                OnDeclareKan(_engine.HumanKanOptions[0]);
                e.Handled = true;
                break;
            case Key.S when _engine.AwaitingHumanCall:
                OnPass();
                e.Handled = true;
                break;
            case Key.N:
                NewRound();
                e.Handled = true;
                break;
        }
    }

    private static Brush Frozen(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    // One player's area: header, hand and discard river.
    private sealed class SeatPanel
    {
        public int Seat { get; }
        public Border Root { get; }
        public TextBlock Title { get; }
        public TextBlock Status { get; }
        public TextBlock Turn { get; }
        public TextBlock RiverCount { get; }
        public WrapPanel Hand { get; }
        public WrapPanel River { get; }

        public SeatPanel(int seat)
        {
            Seat = seat;

            var badge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = GoldColour,
                Child = new TextBlock
                {
                    Text = WindKanji[seat],
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = Frozen("#16201B"),
                    FontFamily = new FontFamily("Microsoft YaHei UI, Yu Gothic UI, Segoe UI"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            Title = new TextBlock
            {
                Text = $"Player {seat + 1} · {SeatNames[seat]}",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextColour
            };

            Turn = new TextBlock { FontSize = 13, Foreground = TextColour, FontWeight = FontWeights.SemiBold };
            var nameStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(Title);
            nameStack.Children.Add(Turn);

            Status = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var header = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(badge, Dock.Left);
            DockPanel.SetDock(Status, Dock.Right);
            header.Children.Add(badge);
            header.Children.Add(Status);
            header.Children.Add(nameStack);

            // Top margin leaves room for a hovered tile to lift without being clipped.
            Hand = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            RiverCount = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = MutedColour,
                Margin = new Thickness(0, 12, 0, 4)
            };
            River = new WrapPanel();

            var content = new StackPanel();
            content.Children.Add(header);
            content.Children.Add(Hand);
            content.Children.Add(RiverCount);
            content.Children.Add(River);

            Root = new Border
            {
                Background = PanelBrush,
                BorderBrush = PanelEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 12, 12),
                Child = content
            };
        }
    }
}
