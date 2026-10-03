using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using TestMahjongGame.Engine;

namespace MahjongTable;

public enum TileMark
{
    None,
    LastDiscard,
    WinningTile,
    Drawn,
    Hint
}

// Draws one mahjong tile. Suit is shown by glyph and label as well as colour, so colour is never the only cue.
public static class TileView
{
    private static readonly FontFamily Face = new("Microsoft YaHei UI, Yu Gothic UI, Segoe UI");
    private static readonly string[] ManzuNumerals = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] WindGlyphs = { "東", "南", "西", "北" };

    private static readonly Brush FaceBrush = Frozen("#F7F2E4");
    private static readonly Brush EdgeBrush = Frozen("#B8AC86");
    private static readonly Brush BackBrush = Frozen("#2F7D5E");
    private static readonly Brush BackEdgeBrush = Frozen("#1D5A43");
    private static readonly Brush ManzuBrush = Frozen("#B3261E");
    private static readonly Brush PinzuBrush = Frozen("#1D4E89");
    private static readonly Brush SouzuBrush = Frozen("#1E6B3A");
    private static readonly Brush InkBrush = Frozen("#1C1C1C");
    private static readonly Brush LastBrush = Frozen("#F2A33A");
    private static readonly Brush WinBrush = Frozen("#FFD84D");
    private static readonly Brush DrawnBrush = Frozen("#3D9BFF");
    private static readonly Brush HintBrush = Frozen("#2FD36B");

    public static FrameworkElement Create(Tile tile, double width, bool faceDown = false, TileMark mark = TileMark.None)
    {
        var height = width * 1.4;
        var border = new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(width * 0.05),
            CornerRadius = new CornerRadius(width * 0.12),
            Background = faceDown ? BackBrush : FaceBrush,
            BorderBrush = faceDown ? BackEdgeBrush : EdgeBrush,
            BorderThickness = new Thickness(1, 1, 1, 3),
            ToolTip = faceDown ? "Hidden tile" : Describe(tile)
        };

        AutomationProperties.SetName(border, faceDown ? "Hidden tile" : Describe(tile));

        if (mark != TileMark.None && !faceDown)
        {
            border.BorderBrush = mark switch
            {
                TileMark.WinningTile => WinBrush,
                TileMark.Drawn => DrawnBrush,
                TileMark.Hint => HintBrush,
                _ => LastBrush
            };
            border.BorderThickness = new Thickness(2.5, 2.5, 2.5, 4);
        }

        if (!faceDown)
        {
            border.Child = BuildFace(tile, width);
        }

        return border;
    }

    public static string Describe(Tile tile)
    {
        if (tile.IsHonor)
        {
            return $"{tile.Honor} ({tile})";
        }

        var suit = tile.Suit switch
        {
            Suit.Manzu => "characters",
            Suit.Pinzu => "circles",
            _ => "bamboo"
        };

        return $"{tile.Rank} of {suit} ({tile})";
    }

    private static UIElement BuildFace(Tile tile, double width)
    {
        if (tile.IsHonor)
        {
            return HonorFace(tile.Honor!.Value, width);
        }

        var (brush, rank, suitGlyph) = tile.Suit switch
        {
            Suit.Manzu => (ManzuBrush, ManzuNumerals[tile.Rank - 1], "萬"),
            Suit.Pinzu => (PinzuBrush, tile.Rank.ToString(), "筒"),
            _ => (SouzuBrush, tile.Rank.ToString(), "索")
        };

        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(Text(rank, width * 0.52, brush, FontWeights.Bold));
        panel.Children.Add(Text(suitGlyph, width * 0.36, brush, FontWeights.SemiBold));
        return panel;
    }

    private static UIElement HonorFace(Honor honor, double width)
    {
        switch (honor)
        {
            case Honor.White:
                // The white dragon is a blank tile with a blue frame.
                return new Border
                {
                    Width = width * 0.58,
                    Height = width * 0.86,
                    BorderBrush = PinzuBrush,
                    BorderThickness = new Thickness(Math.Max(2, width * 0.07)),
                    CornerRadius = new CornerRadius(width * 0.06),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            case Honor.Green:
                return CenteredGlyph("發", width, SouzuBrush);
            case Honor.Red:
                return CenteredGlyph("中", width, ManzuBrush);
            default:
                return CenteredGlyph(WindGlyphs[(int)honor], width, InkBrush);
        }
    }

    private static UIElement CenteredGlyph(string glyph, double width, Brush brush)
    {
        var text = Text(glyph, width * 0.66, brush, FontWeights.Bold);
        text.VerticalAlignment = VerticalAlignment.Center;
        return text;
    }

    private static TextBlock Text(string value, double size, Brush brush, FontWeight weight)
    {
        return new TextBlock
        {
            Text = value,
            FontFamily = Face,
            FontSize = size,
            FontWeight = weight,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = size * 1.05,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = TextAlignment.Center
        };
    }

    private static Brush Frozen(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
