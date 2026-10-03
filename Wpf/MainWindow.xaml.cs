using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TestMahjongGame.Engine;

namespace MahjongTable;

public partial class MainWindow : Window
{
    private const int MaxTurns = 70;
    private const double HandTileWidth = 34;
    private const double RiverTileWidth = 30;

    private static readonly string[] SeatNames = { "East", "South", "West", "North" };
    private static readonly string[] WindKanji = { "東", "南", "西", "北" };

    private static readonly Brush PanelBrush = Frozen("#1B4D3C");
    private static readonly Brush PanelEdge = Frozen("#2F7059");
    private static readonly Brush NextEdge = Frozen("#FFFFFF");
    private static readonly Brush WinnerEdge = Frozen("#FFD84D");
    private static readonly Brush TextColour = Frozen("#F2EFE6");
    private static readonly Brush MutedColour = Frozen("#B9CFC4");
    private static readonly Brush GoldColour = Frozen("#E8B84A");
    private static readonly Brush ErrorColour = Frozen("#FFB4A8");

    private readonly DispatcherTimer _timer = new();
    private readonly List<SeatPanel> _panels = new();
    private GameEngine _engine = null!;
    private int _seed;

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

        _timer.Interval = TimeSpan.FromMilliseconds(SpeedSlider.Value);
        _timer.Tick += (_, _) => PlayOneTurn();
        Loaded += (_, _) => StartFromCommandLine();
    }

    // Optional developer arguments: MahjongTable.exe <seed> [turns-to-play]
    private void StartFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && int.TryParse(args[1], out _))
        {
            SeedBox.Text = args[1];
        }

        NewRound();

        if (args.Length > 2 && int.TryParse(args[2], out var turns))
        {
            for (var i = 0; i < turns && !_engine.IsFinished; i++)
            {
                _engine.PlayTurn();
            }

            Refresh();
        }
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
            SeedHint.Text = "Seed must be a whole number, for example 7.";
            SeedHint.Foreground = ErrorColour;
            SeedBox.Focus();
            return;
        }

        SeedHint.Text = "Empty = random round. A number replays the same round.";
        SeedHint.Foreground = MutedColour;

        LogBox.Clear();
        _engine = new GameEngine(_seed, TextWriter.Null);
        _engine.Logged += line =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        };
        _engine.StartRound();
        Refresh();
        NextButton.Focus();
    }

    private void PlayOneTurn()
    {
        if (_engine.IsFinished)
        {
            _timer.Stop();
            AutoButton.IsChecked = false;
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

    private void Refresh()
    {
        var finished = _engine.IsFinished;
        var result = _engine.Result;
        var reveal = ShowHands.IsChecked == true;

        TurnText.Text = $"Turn {_engine.Turns} / {MaxTurns}";
        WallText.Text = $"Wall {_engine.WallRemaining} left";
        SeedText.Text = $"Seed {_seed}";

        foreach (var panel in _panels)
        {
            var player = _engine.Players[panel.Seat];
            var isWinner = finished && result!.WinnerSeat == panel.Seat;
            var isNext = !finished && _engine.CurrentSeat == panel.Seat;
            var lastRiverIndex = _engine.LastDiscardSeat == panel.Seat ? player.Discards.Count - 1 : -1;
            var winTileIndex = isWinner && result!.Outcome == RoundOutcome.Ron
                ? FindLastIndex(player.Hand.Tiles, _engine.WinningTile!)
                : -1;

            panel.Hand.Children.Clear();
            for (var i = 0; i < player.Hand.Tiles.Count; i++)
            {
                var mark = i == winTileIndex ? TileMark.WinningTile : TileMark.None;
                panel.Hand.Children.Add(TileView.Create(player.Hand.Tiles[i], HandTileWidth, !reveal, mark));
            }

            panel.River.Children.Clear();
            for (var i = 0; i < player.Discards.Count; i++)
            {
                var mark = i == lastRiverIndex && !finished ? TileMark.LastDiscard : TileMark.None;
                panel.River.Children.Add(TileView.Create(player.Discards[i], RiverTileWidth, false, mark));
            }

            panel.RiverCount.Text = $"Discards ({player.Discards.Count})";

            if (isWinner)
            {
                panel.Status.Text = result!.Outcome == RoundOutcome.Tsumo ? "WINNER · Tsumo" : "WINNER · Ron";
                panel.Status.Foreground = GoldColour;
            }
            else if (reveal)
            {
                var shanten = player.Hand.GetShanten();
                panel.Status.Text = shanten == 0 ? "Tenpai" : $"Shanten {shanten}";
                panel.Status.Foreground = shanten == 0 ? GoldColour : MutedColour;
            }
            else
            {
                panel.Status.Text = "Hand hidden";
                panel.Status.Foreground = MutedColour;
            }

            panel.Turn.Text = isNext ? "▶ Draws next" : string.Empty;
            panel.Root.BorderBrush = isWinner ? WinnerEdge : isNext ? NextEdge : PanelEdge;
            panel.Root.BorderThickness = new Thickness(isWinner || isNext ? 3 : 1);
        }

        NextButton.IsEnabled = !finished;
        AutoButton.IsEnabled = !finished;
        ShowResult();
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
                $"{SeatLabel(result.WinnerSeat!.Value)} wins by tsumo (self-drawn) on turn {result.Turns}.",
            RoundOutcome.Ron =>
                $"{SeatLabel(result.WinnerSeat!.Value)} wins by ron on {TileView.Describe(_engine.WinningTile!)}, " +
                $"discarded by {SeatLabel(result.LoserSeat!.Value)}, on turn {result.Turns}.",
            _ => $"Exhaustive draw: the wall ran out after {result.Turns} turns and nobody won."
        };
        ResultBanner.Visibility = Visibility.Visible;
    }

    private static string SeatLabel(int seat)
    {
        return $"Player {seat + 1} ({SeatNames[seat]})";
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

    private void AutoButton_Changed(object sender, RoutedEventArgs e)
    {
        if (_engine is null)
        {
            return;
        }

        if (AutoButton.IsChecked == true && !_engine.IsFinished)
        {
            _timer.Start();
        }
        else
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
            SpeedLabel.Text = $"Speed: {ms} ms per turn";
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
        if (Keyboard.FocusedElement is TextBox && !(Keyboard.FocusedElement as TextBox)!.IsReadOnly)
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

            var name = new TextBlock
            {
                Text = $"Player {seat + 1} · {SeatNames[seat]}",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextColour
            };

            Turn = new TextBlock { FontSize = 13, Foreground = TextColour, FontWeight = FontWeights.SemiBold };
            var nameStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(name);
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

            Hand = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
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
