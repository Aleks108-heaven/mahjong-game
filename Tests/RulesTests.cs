using TestMahjongGame.Engine;
using Xunit;

namespace TestMahjongGame.Tests;

internal static class T
{
    public static Tile Parse(string token)
    {
        var rank = token[0] - '0';
        return token[1] switch
        {
            'm' => new Tile(Suit.Manzu, rank),
            'p' => new Tile(Suit.Pinzu, rank),
            's' => new Tile(Suit.Souzu, rank),
            _ => new Tile((Honor)(rank - 1))
        };
    }

    public static List<Tile> Tiles(string tiles) =>
        tiles.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToList();

    public static Meld Meld(MeldType type, string tile, int from = 1)
    {
        var t = Parse(tile);
        return new Meld(type, Enumerable.Repeat(t, type == MeldType.Pon ? 3 : 4).ToList(), t, from);
    }

    public static Meld Chi(string tiles)
    {
        var list = Tiles(tiles);
        return new Meld(MeldType.Chi, list, list[0], 3);
    }
}

public class DoraTests
{
    [Theory]
    [InlineData("9m", "1m")]
    [InlineData("3p", "4p")]
    [InlineData("9s", "1s")]
    [InlineData("1z", "2z")]
    [InlineData("4z", "1z")]
    [InlineData("5z", "6z")]
    [InlineData("7z", "5z")]
    public void IndicatorPointsToTheNextTile(string indicator, string dora)
    {
        Assert.Equal(T.Parse(dora), Dora.FromIndicator(T.Parse(indicator)));
    }

    [Fact]
    public void Count_IncludesMeldsAndEveryTileOfAKan()
    {
        var kan = new Meld(MeldType.Ankan, T.Tiles("5p 5p 5p 5p"), T.Parse("5p"), -1);
        var count = Dora.Count(T.Tiles("5p 1m"), new[] { kan }, new[] { T.Parse("4p") });
        Assert.Equal(5, count);
    }
}

public class YakuTests
{
    private static YakuResult Eval(string concealed, string win, bool tsumo = false, params Meld[] melds)
    {
        var tiles = T.Tiles(concealed);
        return YakuEvaluator.Evaluate(tiles, melds, T.Parse(win), new WinContext(tsumo, SeatWind: 1));
    }

    private static bool Has(YakuResult result, string part) => result.Yaku.Any(y => y.Name.Contains(part));

    [Fact]
    public void ClosedTanyaoRon()
    {
        var result = Eval("2m 3m 4m 3p 4p 5p 6s 7s 8s 4s 5s 6s 5m 5m", "5m");
        Assert.True(Has(result, "tanyao"));
        Assert.False(Has(result, "pinfu"));
    }

    [Fact]
    public void ClosedRonWithoutYaku_IsRejected_ButTsumoWinsByMenzenTsumo()
    {
        const string hand = "1m 2m 3m 4p 5p 6p 7s 8s 9s 1p 2p 3p 9m 9m";
        Assert.False(Eval(hand, "3p").HasYaku);
        Assert.True(Has(Eval(hand, "3p", tsumo: true), "menzen tsumo"));
    }

    [Fact]
    public void Pinfu_NeedsATwoSidedWait()
    {
        const string hand = "1m 2m 3m 4m 5m 6m 7p 8p 9p 2s 3s 4s 5p 5p";
        Assert.True(Has(Eval(hand, "2s"), "pinfu"));
        Assert.False(Has(Eval(hand, "3s"), "pinfu")); // middle wait
        Assert.False(Has(Eval(hand, "5p"), "pinfu")); // pair wait
    }

    [Fact]
    public void OpenHand_ScoresValueTilesButNotAPlainHand()
    {
        var dragons = Eval("2m 3m 4m 4p 5p 6p 6s 7s 8s 5m 5m", "5m", false, T.Meld(MeldType.Pon, "5z"));
        Assert.True(Has(dragons, "Dragon triplet"));

        var plain = Eval("1m 2m 3m 4p 5p 6p 7s 8s 9s 9m 9m", "3m", false, T.Chi("4s 5s 6s"));
        Assert.False(plain.HasYaku);
    }

    [Fact]
    public void SevenPairs_KokushiAndFlush()
    {
        Assert.True(Has(Eval("1m 1m 3m 3m 5p 5p 7p 7p 2s 2s 4s 4s 9s 9s", "9s"), "Seven pairs"));

        var kokushi = Eval("1m 9m 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z 1m", "1m");
        Assert.True(kokushi.IsYakuman);

        var flush = Eval("1m 2m 3m 4m 5m 6m 7m 8m 9m 1m 2m 3m 5m 5m", "3m");
        Assert.True(Has(flush, "Full flush"));
        Assert.True(Has(flush, "Straight"));
    }

    [Fact]
    public void ClosedKanKeepsTheHandClosed()
    {
        var ankan = new Meld(MeldType.Ankan, T.Tiles("9s 9s 9s 9s"), T.Parse("9s"), -1);
        var result = Eval("2m 3m 4m 4p 5p 6p 6s 7s 8s 5m 5m", "5m", true, ankan);
        Assert.True(Has(result, "menzen tsumo"));
    }

    [Fact]
    public void ValueWindPairIsNotAYaku_ButSeatWindTripletIs()
    {
        // Seat wind is South (1): a South triplet is a yaku, an East one is the round wind and also counts.
        Assert.True(Has(Eval("2m 3m 4m 4p 5p 6p 5m 5m", "5m", false, T.Meld(MeldType.Pon, "2z"), T.Chi("6s 7s 8s")), "Seat wind"));
        Assert.True(Has(Eval("2m 3m 4m 4p 5p 6p 5m 5m", "5m", false, T.Meld(MeldType.Pon, "1z"), T.Chi("6s 7s 8s")), "Round wind"));
    }
}

public class KanAndFuritenTests
{
    private static GameEngine KanTable()
    {
        // Seat 0 holds four 1m; the first draw is a 6z. The kan's replacement tile is 7p, the first indicator 3m.
        var deal = new List<Tile>();
        deal.AddRange(T.Tiles("1m 1m 1m 1m 2s 2s 2s 3s 3s 3s 6z 6z 7z"));
        deal.AddRange(T.Tiles("2m 2m 2m 3m 3m 3m 4m 4m 4m 5m 5m 5m 6m"));
        deal.AddRange(T.Tiles("7m 8m 9m 1p 2p 3p 4p 5p 6p 7p 8p 9p 1s"));
        deal.AddRange(T.Tiles("2s 3s 4s 5s 6s 7s 8s 9s 1z 2z 3z 4z 5z"));
        deal.AddRange(T.Tiles("6z"));

        var wall = Wall.Stacked(deal, T.Tiles("7p"), T.Tiles("3m 9s"));
        var engine = new GameEngine(1, TextWriter.Null);
        engine.StartRound(0, wall);
        engine.PlayTurn();
        return engine;
    }

    [Fact]
    public void StackedWall_DealsTheChosenTiles()
    {
        var engine = KanTable();
        Assert.True(engine.AwaitingHumanDiscard);
        Assert.Equal(4, engine.Players[0].Hand.Tiles.Count(t => t.Equals(T.Parse("1m"))));
        Assert.Equal(T.Parse("4m"), engine.DoraTiles[0]);
    }

    [Fact]
    public void ClosedKan_TakesAReplacementTile_AndFlipsADoraAtOnce()
    {
        var engine = KanTable();
        var option = Assert.Single(engine.HumanKanOptions);
        Assert.Equal(MeldType.Ankan, option.Type);
        var wallBefore = engine.WallRemaining;

        engine.HumanDeclareKan(option);

        var hand = engine.Players[0].Hand;
        Assert.Equal(1, engine.KanCount);
        Assert.Equal(MeldType.Ankan, Assert.Single(hand.Melds).Type);
        Assert.Equal(14, hand.TotalTiles);
        Assert.Contains(T.Parse("7p"), hand.Tiles);
        Assert.Equal(2, engine.DoraIndicators.Count);
        Assert.Equal(wallBefore - 1, engine.WallRemaining);
        Assert.True(engine.AwaitingHumanDiscard);

        // Dead wall stays at 14 and no tile is lost.
        Assert.Equal(136, engine.Players.Sum(p => p.Hand.PhysicalTileCount) + engine.Discards.Count + engine.WallRemaining + 14);
    }

    [Fact]
    public void Kan_ThatIsNotAvailable_Throws()
    {
        var engine = KanTable();
        Assert.Throws<ArgumentException>(() => engine.HumanDeclareKan(new KanOption(T.Parse("2s"), MeldType.Ankan)));
    }

    [Fact]
    public void Waits_ListEveryCompletingTile()
    {
        var hand = new PlayerHand();
        foreach (var tile in T.Tiles("1m 2m 3m 4m 5m 6m 7p 8p 9p 2s 3s 5z 5z"))
        {
            hand.Add(tile);
        }

        Assert.Equal("1s,4s", string.Join(",", hand.Waits()));
    }

    [Fact]
    public void Kans_Wins_AndFuriten_HoldAcrossManyRounds()
    {
        var kans = 0;
        for (var seed = 0; seed < 400; seed++)
        {
            var engine = new GameEngine(seed, TextWriter.Null);
            var result = engine.RunSimulation();
            kans += engine.KanCount;

            Assert.InRange(engine.KanCount, 0, 4);
            Assert.InRange(engine.DoraIndicators.Count, 1, 1 + engine.KanCount);
            Assert.Equal(136, engine.Players.Sum(p => p.Hand.PhysicalTileCount)
                + engine.Players.Sum(p => p.Discards.Count) + engine.WallRemaining + 14
                - (result.Outcome == RoundOutcome.Ron ? 1 : 0));

            if (result.Outcome == RoundOutcome.ExhaustiveDraw)
            {
                continue;
            }

            // Whoever won has a yaku, and a ron winner was not furiten.
            Assert.True(engine.WinningYaku!.HasYaku);
            if (result.Outcome != RoundOutcome.Ron)
            {
                continue;
            }

            var winner = engine.Players[result.WinnerSeat!.Value];
            if (winner.Hand.MeldCount > 0)
            {
                continue;
            }

            var before = new PlayerHand();
            var removed = false;
            foreach (var tile in winner.Hand.Tiles)
            {
                if (!removed && tile.Equals(engine.WinningTile))
                {
                    removed = true;
                    continue;
                }

                before.Add(tile);
            }

            Assert.DoesNotContain(before.Waits(), wait => winner.Thrown.Contains(wait));
        }

        Assert.True(kans > 0, "No kan was made in 400 rounds.");
    }
}
