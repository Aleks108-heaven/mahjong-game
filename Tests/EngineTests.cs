using TestMahjongGame.Engine;
using Xunit;

namespace TestMahjongGame.Tests;

public class TileTests
{
    [Theory]
    [InlineData(Suit.Manzu, 0)]
    [InlineData(Suit.Manzu, 10)]
    [InlineData(Suit.Pinzu, -1)]
    public void Constructor_RejectsInvalidRank(Suit suit, int rank)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tile(suit, rank));
    }

    [Fact]
    public void Constructor_RejectsHonorSuitWithRank()
    {
        Assert.Throws<ArgumentException>(() => new Tile(Suit.Honors, 3));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new Tile(Suit.Manzu, 1), new Tile(Suit.Manzu, 1));
        Assert.NotEqual(new Tile(Suit.Manzu, 1), new Tile(Suit.Pinzu, 1));
        Assert.Equal(new Tile(Honor.Red).GetHashCode(), new Tile(Honor.Red).GetHashCode());
    }

    [Fact]
    public void FromTileIndex_RoundTripsAllKinds()
    {
        for (var i = 0; i < 34; i++)
        {
            Assert.Equal(i, Tile.FromTileIndex(i).ToTileIndex());
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(34)]
    public void FromTileIndex_RejectsOutOfRange(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Tile.FromTileIndex(index));
    }

    [Fact]
    public void ToString_UsesStandardNotation()
    {
        Assert.Equal("5p", new Tile(Suit.Pinzu, 5).ToString());
        Assert.Equal("1z", new Tile(Honor.East).ToString());
        Assert.Equal("7z", new Tile(Honor.Red).ToString());
    }
}

public class ShantenTests
{
    private readonly ShantenCalculator _calc = new();

    private static int[] Counts(string hand)
    {
        var counts = new int[34];
        foreach (var token in hand.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rank = token[0] - '0';
            var index = token[1] switch
            {
                'm' => rank - 1,
                'p' => 9 + rank - 1,
                's' => 18 + rank - 1,
                _ => 27 + rank - 1
            };
            counts[index]++;
        }

        return counts;
    }

    [Theory]
    [InlineData("1m 2m 3m 4m 5m 6m 7m 8m 9m 1p 2p 3p 1p 1p", -1)]
    [InlineData("1m 2m 3m 4m 5m 6m 7m 8m 9m 1p 2p 1s 1s", 0)]
    [InlineData("1m 2m 3m 4m 5m 6m 7m 8m 1p 2p 1s 1s 5s", 1)]
    public void Standard_KnownHands(string hand, int expected)
    {
        Assert.Equal(expected, _calc.CalculateStandardShanten(Counts(hand)));
    }

    [Fact]
    public void SevenPairs_TenpaiAndComplete()
    {
        Assert.Equal(0, _calc.CalculateSevenPairsShanten(Counts("1m 1m 2p 2p 3s 3s 1z 1z 2z 2z 3z 3z 4z")));
        Assert.Equal(-1, _calc.CalculateSevenPairsShanten(Counts("1m 1m 2p 2p 3s 3s 1z 1z 2z 2z 3z 3z 4z 4z")));
    }

    [Fact]
    public void SevenPairs_FourOfAKindIsNotTwoPairs()
    {
        // 1m x4 counts as one pair kind: 5 pair kinds, 6 distinct kinds -> (6 - 5) + (7 - 6) = 2.
        Assert.Equal(2,_calc.CalculateSevenPairsShanten(Counts("1m 1m 1m 1m 2p 2p 3s 3s 1z 1z 2z 2z 3z")));
    }

    [Fact]
    public void ThirteenOrphans_TenpaiAndComplete()
    {
        Assert.Equal(0, _calc.CalculateThirteenOrphansShanten(Counts("1m 9m 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z")));
        Assert.Equal(-1, _calc.CalculateThirteenOrphansShanten(Counts("1m 1m 9m 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z")));
    }

    [Fact]
    public void Overall_UsesLowestOfAllShapes()
    {
        Assert.Equal(0, _calc.CalculateShanten(Counts("1m 9m 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z")));
        Assert.Equal(0, _calc.CalculateShanten(Counts("1m 1m 2p 2p 3s 3s 1z 1z 2z 2z 3z 3z 4z")));
    }

    [Fact]
    public void RejectsWrongLength()
    {
        Assert.Throws<ArgumentException>(() => _calc.CalculateShanten(new int[10]));
    }
}

public class WallTests
{
    [Fact]
    public void Wall_Has136TilesSplit122And14_WithFourOfEach()
    {
        var wall = new Wall(new Random(1));
        Assert.Equal(122, wall.LiveCount);
        Assert.Equal(14, wall.DeadWall.Count);

        var counts = new int[34];
        while (wall.Draw() is { } tile)
        {
            counts[tile.ToTileIndex()]++;
        }

        foreach (var tile in wall.DeadWall)
        {
            counts[tile.ToTileIndex()]++;
        }

        Assert.All(counts, c => Assert.Equal(4, c));
    }

    [Fact]
    public void Draw_ReturnsNullWhenEmpty()
    {
        var wall = new Wall(new Random(1));
        while (wall.Draw() is not null)
        {
        }

        Assert.Null(wall.Draw());
    }
}

public class HandTests
{
    [Fact]
    public void Remove_MatchesEqualTileByValue()
    {
        var hand = new PlayerHand();
        hand.Add(new Tile(Suit.Manzu, 1));
        Assert.True(hand.Remove(new Tile(Suit.Manzu, 1)));
        Assert.Empty(hand.Tiles);
        Assert.False(hand.Remove(new Tile(Suit.Manzu, 1)));
    }

    [Fact]
    public void IsComplete_DetectsWinningHand()
    {
        var hand = new PlayerHand();
        foreach (var t in new[] { "1m", "2m", "3m", "4m", "5m", "6m", "7m", "8m", "9m", "1p", "2p", "3p", "1p", "1p" })
        {
            hand.Add(new Tile(t[1] == 'm' ? Suit.Manzu : Suit.Pinzu, t[0] - '0'));
        }

        Assert.True(hand.IsComplete());
    }

    [Fact]
    public void DiscardBest_BreaksUpCompleteTenpaiCorrectly()
    {
        // 123m 456m 789m 12p 11s plus a stray East: discarding East is the only way to stay tenpai.
        var hand = new PlayerHand();
        foreach (var tile in new[]
                 {
                     new Tile(Suit.Manzu, 1), new Tile(Suit.Manzu, 2), new Tile(Suit.Manzu, 3),
                     new Tile(Suit.Manzu, 4), new Tile(Suit.Manzu, 5), new Tile(Suit.Manzu, 6),
                     new Tile(Suit.Manzu, 7), new Tile(Suit.Manzu, 8), new Tile(Suit.Manzu, 9),
                     new Tile(Suit.Pinzu, 1), new Tile(Suit.Pinzu, 2),
                     new Tile(Suit.Souzu, 1), new Tile(Suit.Souzu, 1),
                     new Tile(Honor.East)
                 })
        {
            hand.Add(tile);
        }

        var discarded = hand.DiscardBest(new Random(1));
        Assert.Equal(new Tile(Honor.East), discarded);
        Assert.True(hand.IsTenpai());
    }
}

public class EngineTests
{
    private static (RoundResult Result, string Log, GameEngine Engine) Run(int seed)
    {
        var writer = new StringWriter();
        var engine = new GameEngine(seed, writer);
        var result = engine.RunSimulation();
        return (result, writer.ToString(), engine);
    }

    [Fact]
    public void SameSeed_GivesIdenticalRound()
    {
        Assert.Equal(Run(42).Log, Run(42).Log);
    }

    [Fact]
    public void EveryTurnThatStartsCompletes_AndRoundAlwaysTerminates()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var (result, _, engine) = Run(seed);

            // 70 draws is the maximum; a finished turn means 13 tiles in hand, except for a tsumo winner (14).
            Assert.InRange(result.Turns, 1, 70);
            foreach (var player in engine.Players)
            {
                var isTsumoWinner = result.Outcome == RoundOutcome.Tsumo && result.WinnerSeat == player.Seat;
                var isRonWinner = result.Outcome == RoundOutcome.Ron && result.WinnerSeat == player.Seat;
                var expected = isTsumoWinner || isRonWinner ? 14 : 13;

                // The player who drew the last tile of an exhaustive draw has also discarded, so still 13.
                Assert.Equal(expected, player.Hand.Tiles.Count);
            }
        }
    }

    [Fact]
    public void ExhaustiveDraw_UsesAll70DrawsAnd70Discards()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var (result, _, engine) = Run(seed);
            if (result.Outcome == RoundOutcome.ExhaustiveDraw)
            {
                Assert.Equal(70, result.Turns);
                Assert.Equal(70, engine.Discards.Count);
                return;
            }
        }

        Assert.Fail("No exhaustive draw found in 500 seeds; the bot may be winning every round.");
    }

    [Fact]
    public void Winners_AreActuallyComplete_AndReportedLosersAreConsistent()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var (result, _, engine) = Run(seed);
            if (result.Outcome == RoundOutcome.ExhaustiveDraw)
            {
                Assert.Null(result.WinnerSeat);
                continue;
            }

            Assert.True(engine.Players[result.WinnerSeat!.Value].Hand.IsComplete());
            Assert.Equal(result.Outcome == RoundOutcome.Ron, result.LoserSeat.HasValue);
        }
    }
}
