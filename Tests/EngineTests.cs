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

            // A bot may call that discard, which moves the turn to the caller instead of seat 1.
            if (engine.CallCount == 0)
            {
                Assert.Single(engine.Players[0].Discards);
                Assert.Equal(1, engine.CurrentSeat);
            }
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
            Assert.Equal(70 - engine.KanCount, engine.Turns);
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
    // Any call offered along the way is declined, so the human never holds a meld here.
    private static GameEngine ToHumanDiscard(int seed, int seat)
    {
        var engine = NewEngine(seed);
        engine.StartRound(seat);
        while (!engine.IsFinished && !engine.AwaitingHumanDiscard)
        {
            if (engine.AwaitingHumanCall)
            {
                engine.HumanPass();
            }
            else
            {
                engine.PlayTurn();
            }
        }

        return engine;
    }

    // Plays one human decision: best discard, and either every available call or none.
    internal static void HumanStep(GameEngine engine, bool takeCalls)
    {
        if (engine.AwaitingHumanDiscard)
        {
            engine.DiscardHuman(engine.SuggestHumanDiscards()[0]);
        }
        else if (engine.AwaitingHumanCall)
        {
            var call = engine.PendingCall!;
            if (takeCalls && call.CanPon)
            {
                engine.HumanPon();
            }
            else if (takeCalls && call.ChiOptions.Count > 0)
            {
                engine.HumanChi(call.ChiOptions[0]);
            }
            else
            {
                engine.HumanPass();
            }
        }
        else
        {
            engine.PlayTurn();
        }
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
        Assert.Equal(tile, engine.Discards[^1]);
        Assert.False(engine.AwaitingHumanDiscard);

        // Unless a bot called the tile, it sits in the human's river and the turn moves to seat 1.
        if (engine.CallCount == 0 && !engine.IsFinished && !engine.AwaitingHumanCall)
        {
            Assert.Equal(tile, engine.Players[0].Discards[^1]);
            Assert.Equal(tile, engine.LastDiscard);
            Assert.Equal(0, engine.LastDiscardSeat);
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
                Assert.True(guard++ < 800, "Round did not end.");
                HumanStep(engine, takeCalls: seed % 2 == 0);
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
                HumanStep(engine, takeCalls: false);
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

public class HandCallTests
{
    private static PlayerHand Hand(string tiles)
    {
        var hand = new PlayerHand();
        foreach (var token in tiles.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            hand.Add(Parse(token));
        }

        return hand;
    }

    private static Tile Parse(string token)
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

    private static string Pairs(IEnumerable<ChiOption> options)
    {
        return string.Join(",", options.Select(o => $"{o.A}{o.B}"));
    }

    [Fact]
    public void CanPon_NeedsTwoMatchingTiles()
    {
        Assert.True(Hand("5p 5p 1m").CanPon(Parse("5p")));
        Assert.True(Hand("5p 5p 5p 1m").CanPon(Parse("5p")));
        Assert.False(Hand("5p 1m").CanPon(Parse("5p")));
        Assert.False(Hand("4p 6p").CanPon(Parse("5p")));
        Assert.True(Hand("1z 1z").CanPon(Parse("1z")));
    }

    [Fact]
    public void ChiOptions_ListsEverySequenceTheDiscardCompletes()
    {
        Assert.Equal("3p4p,4p6p,6p7p", Pairs(Hand("3p 4p 6p 7p 9p").ChiOptions(Parse("5p"))));
    }

    [Fact]
    public void ChiOptions_RespectsTheEdgesOfTheSuit()
    {
        Assert.Equal("2m3m", Pairs(Hand("2m 3m 4m").ChiOptions(Parse("1m"))));
        Assert.Equal("7s8s", Pairs(Hand("7s 8s 6s").ChiOptions(Parse("9s"))));
    }

    [Fact]
    public void ChiOptions_NeedsTheSameSuit_AndNeverAppliesToHonors()
    {
        Assert.Empty(Hand("3m 4m 6m 7m").ChiOptions(Parse("5p")));
        Assert.Empty(Hand("1z 2z 3z 4z").ChiOptions(Parse("2z")));
    }

    [Fact]
    public void Pon_MovesTwoTilesIntoAMeld()
    {
        var hand = Hand("1m 2m 3m 4m 5m 6m 7p 8p 9p 1s 1s 5s 5s");
        var meld = hand.Pon(Parse("5s"), fromSeat: 2);

        Assert.Equal(MeldType.Pon, meld.Type);
        Assert.Equal(2, meld.FromSeat);
        Assert.Equal(11, hand.Tiles.Count);
        Assert.Equal(1, hand.MeldCount);
        Assert.Equal(14, hand.TotalTiles);
        Assert.DoesNotContain(Parse("5s"), hand.Tiles);
        Assert.Equal("[5s 5s 5s]", meld.ToString());
        Assert.EndsWith("[5s 5s 5s]", hand.ToString());
    }

    [Fact]
    public void Chi_MovesTwoTilesIntoASortedMeld()
    {
        var hand = Hand("4p 6p 1m 1m 2m");
        var meld = hand.Chi(Parse("5p"), new ChiOption(Parse("4p"), Parse("6p")), fromSeat: 3);

        Assert.Equal(MeldType.Chi, meld.Type);
        Assert.Equal("[4p 5p 6p]", meld.ToString());
        Assert.Equal(3, hand.Tiles.Count);
        Assert.Equal(1, hand.MeldCount);
    }

    [Fact]
    public void Calls_ThrowWhenTheHandCannotMakeThem()
    {
        Assert.Throws<InvalidOperationException>(() => Hand("5p 1m").Pon(Parse("5p"), 1));
        Assert.Throws<InvalidOperationException>(() =>
            Hand("1m 2m").Chi(Parse("5p"), new ChiOption(Parse("4p"), Parse("6p")), 1));
    }

    [Fact]
    public void OpenHand_CompleteAndWaits_CountMeldsAsThreeTiles()
    {
        var complete = Hand("1m 2m 3m 4m 5m 6m 7p 8p 9p 1s 1s 5s 5s");
        complete.Pon(Parse("5s"), 1);
        Assert.Equal(14, complete.TotalTiles);
        Assert.True(complete.IsComplete());

        var waiting = Hand("1m 2m 3m 4m 5m 6m 7p 8p 1s 1s 5s 5s 7z");
        waiting.Pon(Parse("5s"), 1);
        waiting.Remove(Parse("7z"));
        Assert.Equal(13, waiting.TotalTiles);
        Assert.True(waiting.IsTenpai());
        Assert.True(waiting.CanWinOn(Parse("6p")));
        Assert.True(waiting.CanWinOn(Parse("9p")));
        Assert.False(waiting.CanWinOn(Parse("1p")));
    }

    [Fact]
    public void ShantenAfterPon_ImprovesWhenTheCallHelps()
    {
        var hand = Hand("1m 2m 3m 4m 5m 6m 7p 8p 1s 1s 5s 5s 7z");

        Assert.Equal(1, hand.GetShanten());
        Assert.Equal(0, hand.ShantenAfterPon(Parse("5s")));
    }

    [Fact]
    public void ShantenAfterChi_ImprovesWhenTheCallHelps()
    {
        var hand = Hand("1m 2m 3m 4m 5m 6m 7p 8p 1s 1s 5s 7s 7z");
        var before = hand.GetShanten();

        Assert.True(hand.ShantenAfterChi(Parse("6s"), new ChiOption(Parse("5s"), Parse("7s"))) < before);
    }

    [Fact]
    public void MeldCount_ChangesTheShantenOfTheSameConcealedTiles()
    {
        var calc = new ShantenCalculator();
        var counts = new int[34];
        foreach (var token in "1m 2m 3m 4m 5m 6m 7p 8p 1s 1s".Split(' '))
        {
            counts[Parse(token).ToTileIndex()]++;
        }

        // 10 concealed tiles: two sets, 78p waiting, 11s pair. With one meld already made that is tenpai.
        Assert.Equal(0, calc.CalculateShanten(counts, meldCount: 1));
        Assert.Equal(2, calc.CalculateStandardShanten(counts, meldCount: 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void MeldCount_MustBeBetweenZeroAndFour(int meldCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShantenCalculator().CalculateShanten(new int[34], meldCount));
    }

    [Fact]
    public void OpenHand_NeverUsesSevenPairsOrOrphans()
    {
        var calc = new ShantenCalculator();
        var counts = new int[34];
        foreach (var token in "1m 1m 2p 2p 3s 3s 1z 1z 2z 2z".Split(' '))
        {
            counts[Parse(token).ToTileIndex()]++;
        }

        // Five pairs would be a seven-pairs tenpai-ish shape for a closed hand, but an open hand
        // only ever scores as the standard shape.
        Assert.Equal(calc.CalculateStandardShanten(counts, meldCount: 1), calc.CalculateShanten(counts, meldCount: 1));
        Assert.Equal(
            Math.Min(
                calc.CalculateStandardShanten(counts),
                Math.Min(calc.CalculateSevenPairsShanten(counts), calc.CalculateThirteenOrphansShanten(counts))),
            calc.CalculateShanten(counts));
    }

    [Theory]
    [InlineData("pon", "5m", "5m 5m 5m", "5m")]
    [InlineData("chi", "3m", "3m 4m 5m", "3m,6m")]
    [InlineData("chi", "5m", "3m 4m 5m", "5m,2m")]
    [InlineData("chi", "4m", "3m 4m 5m", "4m")]
    [InlineData("chi", "1m", "1m 2m 3m", "1m,4m")]
    [InlineData("chi", "7m", "7m 8m 9m", "7m")]
    [InlineData("chi", "9m", "7m 8m 9m", "9m,6m")]
    public void ForbiddenDiscards_FollowTheSwapCallingRule(string kind, string called, string meld, string expected)
    {
        var type = kind == "pon" ? MeldType.Pon : MeldType.Chi;
        var tiles = meld.Split(' ').Select(Parse).ToList();

        var forbidden = Meld.ForbiddenDiscards(type, Parse(called), tiles);

        Assert.Equal(expected, string.Join(",", forbidden));
    }

    [Fact]
    public void SuggestDiscards_SkipsForbiddenTiles_UnlessNothingElseIsLeft()
    {
        var hand = Hand("1m 2m 3m 4m 5m 6m 7p 8p 1s 1s 5s 5s 7z");
        var forbidden = new[] { Parse("7z") };

        var suggestions = hand.SuggestDiscards(forbidden);
        Assert.DoesNotContain(Parse("7z"), suggestions);

        var onlyForbidden = Hand("7z");
        Assert.Equal(Parse("7z"), Assert.Single(onlyForbidden.SuggestDiscards(forbidden)));
    }
}

public class HumanCallTests
{
    private static GameEngine NewEngine(int seed) => new(seed, TextWriter.Null);

    // Plays rounds (declining every call except the one wanted) until the human is offered a matching call.
    private static GameEngine FindPrompt(Func<CallOptions, bool> wanted)
    {
        for (var seed = 0; seed < 800; seed++)
        {
            for (var seat = 0; seat < 4; seat++)
            {
                var engine = NewEngine(seed);
                engine.StartRound(seat);

                var guard = 0;
                while (!engine.IsFinished && guard++ < 800)
                {
                    if (engine.AwaitingHumanCall && wanted(engine.PendingCall!))
                    {
                        return engine;
                    }

                    HumanSeatTests.HumanStep(engine, takeCalls: false);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No round offered the human a matching call.");
    }

    [Fact]
    public void Prompt_OffersOnlyCallsTheHandCanMake()
    {
        var engine = FindPrompt(_ => true);
        var call = engine.PendingCall!;
        var human = engine.HumanSeat!.Value;
        var hand = engine.Players[human].Hand;

        Assert.NotEqual(human, call.FromSeat);
        Assert.Equal(hand.CanPon(call.Discard), call.CanPon);
        if (call.ChiOptions.Count > 0)
        {
            Assert.Equal((call.FromSeat + 1) % 4, human);
            Assert.Equal(hand.ChiOptions(call.Discard), call.ChiOptions);
        }

        Assert.True(call.CanPon || call.ChiOptions.Count > 0);
    }

    [Fact]
    public void WhileDeciding_TheRoundIsPaused()
    {
        var engine = FindPrompt(_ => true);
        var turns = engine.Turns;

        engine.PlayTurn();

        Assert.True(engine.AwaitingHumanCall);
        Assert.False(engine.AwaitingHumanDiscard);
        Assert.Equal(turns, engine.Turns);
        Assert.Throws<InvalidOperationException>(() => engine.DiscardHuman(new Tile(Suit.Manzu, 1)));
    }

    [Fact]
    public void Pass_ContinuesWithoutChangingTheHand()
    {
        var engine = FindPrompt(_ => true);
        var hand = engine.Players[engine.HumanSeat!.Value].Hand;
        var tiles = hand.Tiles.ToList();

        engine.HumanPass();

        Assert.Null(engine.PendingCall);
        Assert.Equal(tiles, hand.Tiles);
        Assert.Equal(0, hand.MeldCount);
        Assert.False(engine.AwaitingHumanCall && engine.PendingCall is null);
    }

    [Fact]
    public void Pon_TakesTheTile_AndForcesADiscardThatIsNotTheCalledTile()
    {
        var engine = FindPrompt(c => c.CanPon);
        var call = engine.PendingCall!;
        var human = engine.HumanSeat!.Value;
        var hand = engine.Players[human].Hand;
        var riverBefore = engine.Players[call.FromSeat].Discards.Count;
        var calls = engine.CallCount;

        engine.HumanPon();

        Assert.Equal(calls + 1, engine.CallCount);
        Assert.Equal(1, hand.MeldCount);
        Assert.Equal(MeldType.Pon, hand.Melds[0].Type);
        Assert.Equal(14, hand.TotalTiles);
        Assert.True(engine.AwaitingHumanDiscard);
        Assert.Equal(human, engine.CurrentSeat);
        Assert.Equal(riverBefore - 1, engine.Players[call.FromSeat].Discards.Count);
        Assert.Null(engine.LastDiscard);
        Assert.Contains(call.Discard, engine.ForbiddenDiscards);

        // Throwing the called tile straight back is the swap-calling violation.
        if (hand.Tiles.Contains(call.Discard))
        {
            Assert.False(engine.IsDiscardAllowed(call.Discard));
            Assert.Throws<ArgumentException>(() => engine.DiscardHuman(call.Discard));
            Assert.True(engine.AwaitingHumanDiscard);
        }

        var allowed = hand.Tiles.First(engine.IsDiscardAllowed);
        engine.DiscardHuman(allowed);
        Assert.Equal(13, hand.TotalTiles);
        Assert.Empty(engine.ForbiddenDiscards);
    }

    [Fact]
    public void Chi_TakesTheTile_AndForbidsTheCalledTile()
    {
        var engine = FindPrompt(c => c.ChiOptions.Count > 0);
        var call = engine.PendingCall!;
        var human = engine.HumanSeat!.Value;
        var hand = engine.Players[human].Hand;
        var option = call.ChiOptions[0];

        engine.HumanChi(option);

        Assert.Equal(1, hand.MeldCount);
        Assert.Equal(MeldType.Chi, hand.Melds[0].Type);
        Assert.Contains(call.Discard, hand.Melds[0].Tiles);
        Assert.Equal(14, hand.TotalTiles);
        Assert.True(engine.AwaitingHumanDiscard);
        Assert.Contains(call.Discard, engine.ForbiddenDiscards);
        Assert.False(engine.IsDiscardAllowed(call.Discard) && hand.Tiles.Contains(call.Discard));
    }

    [Fact]
    public void SuggestedDiscardsAfterACall_AreAlwaysAllowed()
    {
        var engine = FindPrompt(c => c.CanPon || c.ChiOptions.Count > 0);
        var call = engine.PendingCall!;
        if (call.CanPon)
        {
            engine.HumanPon();
        }
        else
        {
            engine.HumanChi(call.ChiOptions[0]);
        }

        Assert.All(engine.SuggestHumanDiscards(), tile => Assert.True(engine.IsDiscardAllowed(tile)));
    }

    [Fact]
    public void UnavailableCalls_Throw_AndKeepWaiting()
    {
        var chiOnly = FindPrompt(c => !c.CanPon && c.ChiOptions.Count > 0);
        Assert.Throws<InvalidOperationException>(() => chiOnly.HumanPon());
        Assert.Throws<ArgumentException>(() =>
            chiOnly.HumanChi(new ChiOption(new Tile(Honor.East), new Tile(Honor.South))));
        Assert.True(chiOnly.AwaitingHumanCall);
    }

    [Fact]
    public void CallMethods_Throw_WhenNothingIsPending()
    {
        var engine = NewEngine(1);
        engine.StartRound(0);

        Assert.Throws<InvalidOperationException>(() => engine.HumanPon());
        Assert.Throws<InvalidOperationException>(() => engine.HumanPass());
        Assert.Throws<InvalidOperationException>(() =>
            engine.HumanChi(new ChiOption(new Tile(Suit.Manzu, 1), new Tile(Suit.Manzu, 2))));
    }

    [Fact]
    public void HumanTakingEveryCall_StillFinishesEveryRound_WithoutLosingTiles()
    {
        for (var seed = 0; seed < 150; seed++)
        {
            var engine = NewEngine(seed);
            engine.StartRound(seed % 4);

            var guard = 0;
            while (!engine.IsFinished)
            {
                Assert.True(guard++ < 1000, "Round did not end.");
                HumanSeatTests.HumanStep(engine, takeCalls: true);
            }

            var inHands = engine.Players.Sum(p => p.Hand.PhysicalTileCount);
            var inRivers = engine.Players.Sum(p => p.Discards.Count);
            var expected = 136 + (engine.Result!.Outcome == RoundOutcome.Ron ? 1 : 0);
            Assert.Equal(expected, inHands + inRivers + engine.WallRemaining + 14);
        }
    }

    [Fact]
    public void NobodyCallsTheVeryLastDiscard()
    {
        // The last discard of the round can't be called, so the round ends with the wall empty and
        // every player still at 13 (counting melds) unless someone won.
        for (var seed = 0; seed < 300; seed++)
        {
            var writer = new StringWriter();
            var engine = new GameEngine(seed, writer);
            var result = engine.RunSimulation();
            if (result.Outcome != RoundOutcome.ExhaustiveDraw)
            {
                continue;
            }

            var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var lastDiscardIndex = Array.FindLastIndex(lines, l => l.Contains("discards"));
            Assert.DoesNotContain(lines.Skip(lastDiscardIndex + 1), l => l.Contains("pons") || l.Contains("chis"));
        }
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

            // 70 draws is the maximum; a finished turn means 13 tiles in hand (counting each meld as 3),
            // except for a winner (14).
            Assert.InRange(result.Turns, 1, 70);
            foreach (var player in engine.Players)
            {
                var isWinner = result.WinnerSeat == player.Seat;
                var expected = isWinner ? 14 : 13;

                // The player who drew the last tile of an exhaustive draw has also discarded, so still 13.
                Assert.Equal(expected, player.Hand.TotalTiles);
            }
        }
    }

    [Fact]
    public void ExhaustiveDraw_UsesAll70Draws_AndOneDiscardPerDrawAndCall()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var (result, _, engine) = Run(seed);
            if (result.Outcome == RoundOutcome.ExhaustiveDraw)
            {
                // Every kan shortens the live wall by one tile. A call (pon, chi, open kan) adds a discard;
                // a closed or added kan does not, since the replacement draw still ends in one discard.
                Assert.Equal(70 - engine.KanCount, result.Turns);
                Assert.Equal(result.Turns + engine.CallCount, engine.Discards.Count);
                return;
            }
        }

        Assert.Fail("No exhaustive draw found in 500 seeds; the bot may be winning every round.");
    }

    [Fact]
    public void NoTileIsEverLostOrDuplicated()
    {
        // 136 tiles = concealed + melds + rivers + live wall + 14 dead wall.
        // On a ron the winning tile is in both the winner's hand and the discarder's river, so +1.
        for (var seed = 0; seed < 300; seed++)
        {
            var (result, _, engine) = Run(seed);
            var inHands = engine.Players.Sum(p => p.Hand.PhysicalTileCount);
            var inRivers = engine.Players.Sum(p => p.Discards.Count);
            var expected = 136 + (result.Outcome == RoundOutcome.Ron ? 1 : 0);

            Assert.Equal(expected, inHands + inRivers + engine.WallRemaining + 14);
        }
    }

    [Fact]
    public void BotsActuallyCall_AndMeldsAreWellFormed()
    {
        var calls = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var (_, _, engine) = Run(seed);
            calls += engine.CallCount;

            foreach (var player in engine.Players)
            {
                Assert.True(player.Hand.MeldCount <= 4);
                foreach (var meld in player.Hand.Melds)
                {
                    Assert.Equal(meld.IsKan ? 4 : 3, meld.Tiles.Count);
                    Assert.NotEqual(player.Seat, meld.FromSeat);
                    Assert.Contains(meld.CalledTile, meld.Tiles);
                    if (meld.IsTriplet)
                    {
                        Assert.All(meld.Tiles, t => Assert.Equal(meld.CalledTile, t));
                    }
                    else
                    {
                        // A chi is three consecutive ranks in one numbered suit, and comes from the player on the left.
                        var ranks = meld.Tiles.Select(t => t.Rank).OrderBy(r => r).ToArray();
                        Assert.All(meld.Tiles, t => Assert.Equal(meld.CalledTile.Suit, t.Suit));
                        Assert.Equal(ranks[0] + 1, ranks[1]);
                        Assert.Equal(ranks[1] + 1, ranks[2]);
                        Assert.Equal((player.Seat + 3) % 4, meld.FromSeat);
                    }
                }
            }
        }

        Assert.True(calls > 50, $"Expected bots to call regularly but saw only {calls} calls in 200 rounds.");
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
