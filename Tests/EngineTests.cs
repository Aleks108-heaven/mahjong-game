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

public class StepApiTests
{
    private static GameEngine NewEngine(int seed) => new(seed, TextWriter.Null);

    [Fact]
    public void PlayTurn_BeforeStartRound_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewEngine(1).PlayTurn());
    }

    [Fact]
    public void PlayTurn_AdvancesExactlyOneTurnAndOneDiscard()
    {
        var engine = NewEngine(1);
        engine.StartRound();
        Assert.Equal(0, engine.Turns);
        Assert.Equal(70, engine.WallRemaining);

        engine.PlayTurn();

        if (!engine.IsFinished)
        {
            Assert.Equal(1, engine.Turns);
            Assert.Equal(69, engine.WallRemaining);
            Assert.Single(engine.Discards);
            Assert.Single(engine.Players[0].Discards);
            Assert.Equal(1, engine.CurrentSeat);
        }
    }

    [Fact]
    public void RoundIsFinishedAsSoonAsTheLastTurnEnds()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var engine = NewEngine(seed);
            engine.StartRound();
            while (!engine.IsFinished)
            {
                engine.PlayTurn();
            }

            if (engine.Result!.Outcome != RoundOutcome.ExhaustiveDraw)
            {
                continue;
            }

            // No extra "failed draw" turn is needed: the round ends right after turn 70.
            Assert.Equal(70, engine.Turns);
            Assert.Equal(0, engine.WallRemaining);
            return;
        }

        Assert.Fail("No exhaustive draw found in 300 seeds.");
    }

    [Fact]
    public void PlayTurn_IsNoOpAfterRoundEnds()
    {
        var engine = NewEngine(3);
        engine.StartRound();
        while (!engine.IsFinished)
        {
            engine.PlayTurn();
        }

        var turns = engine.Turns;
        engine.PlayTurn();
        Assert.Equal(turns, engine.Turns);
    }

    [Fact]
    public void StartRound_ResetsStateForANewRound()
    {
        var engine = NewEngine(5);
        engine.StartRound();
        while (!engine.IsFinished)
        {
            engine.PlayTurn();
        }

        engine.StartRound();

        Assert.False(engine.IsFinished);
        Assert.Null(engine.Result);
        Assert.Equal(0, engine.Turns);
        Assert.Empty(engine.Discards);
        Assert.All(engine.Players, p => Assert.Equal(13, p.Hand.Tiles.Count));
        Assert.All(engine.Players, p => Assert.Empty(p.Discards));
    }

    [Fact]
    public void Logged_RaisesTheSameLinesThatAreWritten()
    {
        var writer = new StringWriter();
        var engine = new GameEngine(9, writer);
        var lines = new List<string>();
        engine.Logged += lines.Add;

        engine.RunSimulation();

        Assert.Equal(writer.ToString(), string.Join(string.Empty, lines.Select(l => l + writer.NewLine)));
    }

    [Fact]
    public void SteppingMatchesRunSimulation()
    {
        var full = new StringWriter();
        var expected = new GameEngine(11, full).RunSimulation();

        var stepped = NewEngine(11);
        stepped.StartRound();
        while (!stepped.IsFinished)
        {
            stepped.PlayTurn();
        }

        Assert.Equal(expected, stepped.Result);
    }
}

public class HumanSeatTests
{
    private static GameEngine NewEngine(int seed) => new(seed, TextWriter.Null);

    // Starts a round with a human at the given seat and plays bot turns until the human must discard.
    private static GameEngine ToHumanDiscard(int seed, int seat)
    {
        var engine = NewEngine(seed);
        engine.StartRound(seat);
        while (!engine.IsFinished && !engine.AwaitingHumanDiscard)
        {
            engine.PlayTurn();
        }

        return engine;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void StartRound_RejectsBadSeat(int seat)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewEngine(1).StartRound(seat));
    }

    [Fact]
    public void WithoutHuman_NeverWaits()
    {
        var engine = NewEngine(1);
        engine.StartRound();
        Assert.Null(engine.HumanSeat);
        Assert.False(engine.AwaitingHumanDiscard);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void PausesAtTheHumansDiscard_WithFourteenTiles(int seat)
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var engine = ToHumanDiscard(seed, seat);
            if (engine.IsFinished)
            {
                continue; // a bot won before the human's first turn
            }

            Assert.True(engine.AwaitingHumanDiscard);
            Assert.Equal(seat, engine.CurrentSeat);
            Assert.Equal(14, engine.Players[seat].Hand.Tiles.Count);
            Assert.Equal(seat, engine.LastDrawSeat);
            Assert.NotNull(engine.LastDrawnTile);
            Assert.Contains(engine.LastDrawnTile!, engine.Players[seat].Hand.Tiles);
            return;
        }

        Assert.Fail("No seed reached the human's discard.");
    }

    [Fact]
    public void PlayTurn_IsNoOpWhileWaitingForHuman()
    {
        var engine = ToHumanDiscard(2, 0);
        var turns = engine.Turns;
        var tiles = engine.Players[0].Hand.Tiles.Count;

        engine.PlayTurn();

        Assert.True(engine.AwaitingHumanDiscard);
        Assert.Equal(turns, engine.Turns);
        Assert.Equal(tiles, engine.Players[0].Hand.Tiles.Count);
    }

    [Fact]
    public void DiscardHuman_RemovesTileAndPassesTheTurn()
    {
        var engine = ToHumanDiscard(2, 0);
        Assert.False(engine.IsFinished);
        var tile = engine.Players[0].Hand.Tiles[0];

        engine.DiscardHuman(tile);

        Assert.Equal(13, engine.Players[0].Hand.Tiles.Count);
        Assert.Equal(tile, engine.Players[0].Discards[^1]);
        Assert.Equal(tile, engine.LastDiscard);
        Assert.Equal(0, engine.LastDiscardSeat);
        Assert.False(engine.AwaitingHumanDiscard);
        if (!engine.IsFinished)
        {
            Assert.Equal(1, engine.CurrentSeat);
        }
    }

    [Fact]
    public void DiscardHuman_Throws_WhenNotWaiting()
    {
        var engine = NewEngine(1);
        engine.StartRound();
        Assert.Throws<InvalidOperationException>(() => engine.DiscardHuman(new Tile(Suit.Manzu, 1)));
    }

    [Fact]
    public void DiscardHuman_Throws_ForTileNotInHand_AndKeepsWaiting()
    {
        var engine = ToHumanDiscard(2, 0);
        var hand = engine.Players[0].Hand;
        var missing = Enumerable.Range(0, 34).Select(Tile.FromTileIndex).First(t => !hand.Tiles.Contains(t));

        Assert.Throws<ArgumentException>(() => engine.DiscardHuman(missing));

        Assert.True(engine.AwaitingHumanDiscard);
        Assert.Equal(14, hand.Tiles.Count);
    }

    [Fact]
    public void FullRoundWithHumanPlayingSuggestedDiscards_AlwaysEnds()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var engine = NewEngine(seed);
            engine.StartRound(seed % 4);

            var guard = 0;
            while (!engine.IsFinished)
            {
                Assert.True(guard++ < 500, "Round did not end.");
                if (engine.AwaitingHumanDiscard)
                {
                    engine.DiscardHuman(engine.Players[engine.HumanSeat!.Value].Hand.SuggestDiscards()[0]);
                }
                else
                {
                    engine.PlayTurn();
                }
            }

            Assert.InRange(engine.Turns, 1, 70);
        }
    }

    [Fact]
    public void Human_CanWinByTsumo_OrRon_WhenPlayingSuggestions()
    {
        // The suggestion always minimises shanten, so over many seeds the human must win some rounds.
        var wins = 0;
        for (var seed = 0; seed < 300; seed++)
        {
            var engine = NewEngine(seed);
            engine.StartRound(0);
            while (!engine.IsFinished)
            {
                if (engine.AwaitingHumanDiscard)
                {
                    engine.DiscardHuman(engine.Players[0].Hand.SuggestDiscards()[0]);
                }
                else
                {
                    engine.PlayTurn();
                }
            }

            if (engine.Result!.WinnerSeat == 0)
            {
                wins++;
                Assert.True(engine.Players[0].Hand.IsComplete());
            }
        }

        Assert.True(wins > 0, "The human never won in 300 rounds.");
    }

    [Fact]
    public void SuggestDiscards_AndShantenAfterDiscard_AgreeOnTenpai()
    {
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

        var suggestions = hand.SuggestDiscards();

        Assert.Equal(new Tile(Honor.East), Assert.Single(suggestions));
        Assert.Equal(0, hand.ShantenAfterDiscard(new Tile(Honor.East)));
        Assert.True(hand.ShantenAfterDiscard(new Tile(Suit.Manzu, 1)) > 0);
        Assert.Throws<ArgumentException>(() => hand.ShantenAfterDiscard(new Tile(Suit.Souzu, 9)));
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
