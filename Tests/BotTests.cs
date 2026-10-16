using System.Security.Cryptography;
using System.Text;
using TestMahjongGame.Engine;
using Xunit;

namespace TestMahjongGame.Tests;

internal static class BotTable
{
    public static PlayerHand Hand(string concealed, params Meld[] melds)
    {
        var hand = new PlayerHand();
        foreach (var tile in T.Tiles(concealed))
        {
            hand.Add(tile);
        }

        hand.Sort();
        return hand;
    }

    public static SeatInfo Seat(int seat, string river = "", string thrown = "", params Meld[] melds)
    {
        var riverTiles = T.Tiles(river);
        var thrownTiles = T.Tiles(thrown.Length > 0 ? thrown : river);
        return new SeatInfo(seat, riverTiles, thrownTiles, melds);
    }

    // A table seen from `seat`, with the other three seats supplied (empty by default).
    public static TableView View(
        PlayerHand hand,
        int seat = 0,
        SeatInfo[]? others = null,
        string forbidden = "",
        string indicators = "",
        bool canKan = true)
    {
        var seats = new List<SeatInfo>();
        for (var i = 0; i < 4; i++)
        {
            seats.Add(i == seat
                ? new SeatInfo(i, Array.Empty<Tile>(), Array.Empty<Tile>(), hand.Melds)
                : others?.FirstOrDefault(s => s.Seat == i) ?? Seat(i));
        }

        return new TableView(seat, hand, seats, T.Tiles(indicators), 0, 60, canKan, T.Tiles(forbidden));
    }

    // Three open sets: the threat the Senior bot respects most.
    public static SeatInfo OpenHandedOpponent(int seat, string thrown = "")
    {
        return Seat(seat, thrown, thrown, T.Chi("1s 2s 3s"), T.Chi("4s 5s 6s"), T.Meld(MeldType.Pon, "7z"));
    }

    public static int[] Counts(PlayerHand hand) => hand.TileCounts();
}

public class BotLevelEngineTests
{
    [Fact]
    public void DefaultLevelIsMiddle()
    {
        var engine = new GameEngine(1, TextWriter.Null);
        for (var seat = 0; seat < 4; seat++)
        {
            Assert.Equal(BotLevel.Middle, engine.LevelOf(seat));
        }
    }

    [Fact]
    public void OneLevelCanBeGivenToEverySeat()
    {
        var engine = new GameEngine(1, TextWriter.Null, BotLevel.Senior);
        Assert.All(Enumerable.Range(0, 4), seat => Assert.Equal(BotLevel.Senior, engine.LevelOf(seat)));
    }

    [Fact]
    public void EachSeatKeepsItsOwnLevel()
    {
        var levels = new[] { BotLevel.Junior, BotLevel.Middle, BotLevel.Senior, BotLevel.Middle };
        var engine = new GameEngine(1, TextWriter.Null, levels);
        for (var seat = 0; seat < 4; seat++)
        {
            Assert.Equal(levels[seat], engine.LevelOf(seat));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void SeatLevelsNeedExactlyFourEntries(int count)
    {
        var levels = Enumerable.Repeat(BotLevel.Middle, count).ToArray();
        Assert.Throws<ArgumentException>(() => new GameEngine(1, TextWriter.Null, levels));
    }

    [Fact]
    public void NullSeatLevelsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new GameEngine(1, TextWriter.Null, (IReadOnlyList<BotLevel>)null!));
    }

    [Fact]
    public void UnknownLevelIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameEngine(1, TextWriter.Null, (BotLevel)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => BotBrains.For((BotLevel)(-1)));
    }

    [Fact]
    public void ViewNeedsAStartedRound()
    {
        var engine = new GameEngine(1, TextWriter.Null);
        Assert.Throws<InvalidOperationException>(() => engine.ViewFor(0));
    }

    [Theory]
    [InlineData(BotLevel.Junior)]
    [InlineData(BotLevel.Middle)]
    [InlineData(BotLevel.Senior)]
    public void EveryLevelFinishesEveryRoundWithoutLosingATile(BotLevel level)
    {
        for (var seed = 1; seed <= 120; seed++)
        {
            var engine = new GameEngine(seed, TextWriter.Null, level);
            var result = engine.RunSimulation();

            var inHands = engine.Players.Sum(p => p.Hand.PhysicalTileCount);
            var inRivers = engine.Players.Sum(p => p.Discards.Count);
            var expected = 136 + (result.Outcome == RoundOutcome.Ron ? 1 : 0);
            Assert.Equal(expected, inHands + inRivers + engine.WallRemaining + 14);

            // A win always has a yaku.
            if (result.Outcome != RoundOutcome.ExhaustiveDraw)
            {
                Assert.True(engine.WinningYaku!.HasYaku);
            }
        }
    }

    [Fact]
    public void MixedTablesFinishToo()
    {
        var table = new[] { BotLevel.Junior, BotLevel.Middle, BotLevel.Senior, BotLevel.Senior };
        for (var seed = 1; seed <= 80; seed++)
        {
            var seats = Enumerable.Range(0, 4).Select(s => table[(s + seed) % 4]).ToArray();
            var engine = new GameEngine(seed, TextWriter.Null, seats);
            engine.RunSimulation();
            Assert.True(engine.IsFinished);
        }
    }

    [Fact]
    public void HumanSeatStillWorksAgainstSeniorBots()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var engine = new GameEngine(seed, TextWriter.Null, BotLevel.Senior);
            engine.StartRound(seed % 4);

            var guard = 0;
            while (!engine.IsFinished)
            {
                Assert.True(guard++ < 1000, "Round did not end.");
                HumanSeatTests.HumanStep(engine, takeCalls: true);
            }
        }
    }

    [Fact]
    public void EachRoundIsRepeatableForEveryLevel()
    {
        foreach (var level in Enum.GetValues<BotLevel>())
        {
            var first = new StringWriter();
            var second = new StringWriter();
            new GameEngine(21, first, level).RunSimulation();
            new GameEngine(21, second, level).RunSimulation();
            Assert.Equal(first.ToString(), second.ToString());
        }
    }

    [Fact]
    public void MiddleBotStillPlaysTheOriginalGameExactly()
    {
        // Seeds 1-30 were played by the original bot before the levels existed; this is the hash of those logs.
        // If it changes, the Middle bot no longer plays as it did.
        var all = new StringBuilder();
        for (var seed = 1; seed <= 30; seed++)
        {
            var writer = new StringWriter { NewLine = "\n" };
            new GameEngine(seed, writer).RunSimulation();
            all.Append(writer);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(all.ToString())));
        Assert.Equal("50EE1127666BF44D40536260E2F4EBCE4DEC21618F67E8EB3BDCBCBB9F2E8360", hash);
    }
}

public class TableViewTests
{
    [Fact]
    public void VisibleCountsAddUpOwnTilesRiversMeldsAndIndicators()
    {
        var hand = BotTable.Hand("1m 2m 3m 4p 5p 6p 7s 8s 9s 1z 1z 5z 6z");
        var view = BotTable.View(
            hand,
            seat: 0,
            others: new[]
            {
                BotTable.Seat(1, river: "1m 9p"),
                BotTable.Seat(2, "1z", "1z", T.Chi("1s 2s 3s")),
                BotTable.Seat(3, "", "", T.Meld(MeldType.Pon, "5z"))
            },
            indicators: "1m");

        var visible = view.VisibleCounts();
        Assert.Equal(13 + 2 + 1 + 3 + 3 + 1, visible.Sum());
        Assert.Equal(3, visible[T.Parse("1m").ToTileIndex()]);   // hand + river + indicator
        Assert.Equal(3, visible[T.Parse("1z").ToTileIndex()]);   // two in hand, one in a river
        Assert.Equal(4, visible[T.Parse("5z").ToTileIndex()]);   // one in hand, a pon of three
        Assert.Equal(0, visible[T.Parse("4s").ToTileIndex()]);
    }

    [Fact]
    public void VisibleCountsAreACopy()
    {
        var view = BotTable.View(BotTable.Hand("1m 2m 3m 4p 5p 6p 7s 8s 9s 1z 1z 5z 6z"));
        view.VisibleCounts()[0] = 99;
        Assert.NotEqual(99, view.VisibleCounts()[0]);
    }

    [Fact]
    public void ATableHasFourSeats()
    {
        var hand = BotTable.Hand("1m 2m 3m");
        Assert.Throws<ArgumentException>(() =>
            new TableView(0, hand, new[] { BotTable.Seat(0) }, Array.Empty<Tile>(), 0, 60, true, Array.Empty<Tile>()));
    }

    [Fact]
    public void OnlyOpenMeldsCountAsOpen()
    {
        var seat = BotTable.Seat(1, "", "", T.Chi("1s 2s 3s"), T.Meld(MeldType.Pon, "5z"), T.Meld(MeldType.Ankan, "9m"));
        Assert.Equal(2, seat.OpenMeldCount);
    }

    [Fact]
    public void AViewOfARealRoundOnlyEverHoldsPublicInformation()
    {
        // Every kind has 4 copies in the game, so no seat may ever see more than 4 of one tile, and the total
        // it sees is exactly its own tiles plus what lies face-up on the table.
        for (var seed = 1; seed <= 60; seed++)
        {
            var engine = new GameEngine(seed, TextWriter.Null, BotLevel.Senior);
            engine.StartRound();
            var guard = 0;
            while (!engine.IsFinished)
            {
                Assert.True(guard++ < 1000);
                engine.PlayTurn();
                for (var seat = 0; seat < 4; seat++)
                {
                    var view = engine.ViewFor(seat);
                    var visible = view.VisibleCounts();
                    Assert.All(visible, count => Assert.InRange(count, 0, 4));

                    var expected = engine.Players[seat].Hand.Tiles.Count
                        + engine.Players.Sum(p => p.Discards.Count + p.Hand.Melds.Sum(m => m.Tiles.Count))
                        + engine.DoraIndicators.Count;
                    Assert.Equal(expected, visible.Sum());
                    Assert.Same(engine.Players[seat].Hand, view.Hand);
                }
            }
        }
    }
}

public class JuniorBotTests
{
    private readonly JuniorBrain _junior = new();

    [Fact]
    public void MakesRoughlyTheStatedShareOfMistakes()
    {
        // Throwing 1m leaves a tenpai hand; every other discard leaves it one step further away.
        var hand = BotTable.Hand("1m 3p 4p 5p 6p 7p 8p 2s 3s 4s 7s 8s 5z 5z");
        var view = BotTable.View(hand);
        var best = hand.SuggestDiscards();
        Assert.Single(best);

        var rng = new Random(5);
        var mistakes = 0;
        const int samples = 4000;
        for (var i = 0; i < samples; i++)
        {
            if (!best.Contains(_junior.ChooseDiscard(view, rng)))
            {
                mistakes++;
            }
        }

        // 20% of discards are random, and a random pick is the right tile 1 time in 14.
        var rate = (double)mistakes / samples;
        Assert.InRange(rate, 0.14, 0.23);
    }

    [Fact]
    public void NeverThrowsAForbiddenTileWhileAnotherIsAvailable()
    {
        var hand = BotTable.Hand("1m 3p 4p 5p 6p 7p 8p 2s 3s 4s 7s 8s 5z 5z");
        var view = BotTable.View(hand, forbidden: "1m");
        var rng = new Random(9);
        for (var i = 0; i < 2000; i++)
        {
            Assert.NotEqual(T.Parse("1m"), _junior.ChooseDiscard(view, rng));
        }
    }

    [Fact]
    public void StillDiscardsWhenEveryTileIsForbidden()
    {
        var hand = BotTable.Hand("1m 1m 1m");
        var view = BotTable.View(hand, forbidden: "1m");
        Assert.Equal(T.Parse("1m"), _junior.ChooseDiscard(view, new Random(1)));
    }

    [Fact]
    public void OnlyPonsTilesThatScore()
    {
        // Seat 0 is East in an East round: dragons and East count, 5p does not, and neither does South.
        var hand = BotTable.Hand("5z 5z 5p 5p 2z 2z 1m 2m 3m 4s 5s 6s 9s");
        var view = BotTable.View(hand);
        Assert.True(_junior.WantsPon(view, T.Parse("5z")));
        Assert.False(_junior.WantsPon(view, T.Parse("5p")));
        Assert.False(_junior.WantsPon(view, T.Parse("2z")));
    }

    [Fact]
    public void NeverChisOrKans()
    {
        var hand = BotTable.Hand("4m 4m 4m 4m 5p 6p 7p 1s 2s 3s 7s 8s 5z");
        var view = BotTable.View(hand);
        var chi = new[] { new ChiOption(T.Parse("5p"), T.Parse("6p")) };
        Assert.Null(_junior.ChooseChi(view, T.Parse("7p"), chi));
        Assert.Null(_junior.ChooseSelfKan(view));
        Assert.False(_junior.WantsOpenKan(view, T.Parse("4m")));
    }
}

public class SeniorBotTests
{
    private readonly SeniorBrain _senior = new();
    private readonly MiddleBrain _middle = new();

    [Fact]
    public void KeepsLegalAndIsStatelessBetweenCalls()
    {
        var hand = BotTable.Hand("1m 4m 7m 2p 5p 5p 8p 3s 6s 9s 1z 2z 3z 4z");
        var view = BotTable.View(hand);
        var first = _senior.ChooseDiscard(view, new Random(3));
        _senior.ChooseDiscard(BotTable.View(BotTable.Hand("1m 2m 3m 4p 5p 6p 7s 8s 9s 1z 1z 5m 6m 9p")), new Random(8));
        Assert.Equal(first, _senior.ChooseDiscard(view, new Random(3)));
        Assert.Contains(first, hand.Tiles);
    }

    [Fact]
    public void KeepsTenpaiWhenItIsAvailable()
    {
        // Throwing 9p leaves a tenpai hand waiting on 3m and 6m.
        var hand = BotTable.Hand("1m 2m 3m 4m 5m 4p 5p 6p 7s 8s 9s 1z 1z 9p");
        var view = BotTable.View(hand);
        Assert.Equal(T.Parse("9p"), _senior.ChooseDiscard(view, new Random(1)));
    }

    [Fact]
    public void PicksTheDiscardWithTheMostTilesLeftToImproveWith()
    {
        // The property behind the Senior level: among the discards that keep the lowest shanten, it chooses one
        // with the most unseen improving tiles. Checked against an independent count on 300 random hands.
        var rng = new Random(2024);
        var calc = new ShantenCalculator();
        var checkedHands = 0;
        for (var round = 0; round < 300; round++)
        {
            var hand = RandomHand(rng, 14);
            var view = BotTable.View(hand);
            var counts = hand.TileCounts();
            var visible = view.VisibleCounts();

            var bestShanten = int.MaxValue;
            var ukeire = new Dictionary<int, (int Shanten, int Ukeire)>();
            for (var kind = 0; kind < 34; kind++)
            {
                if (counts[kind] == 0)
                {
                    continue;
                }

                counts[kind]--;
                var shanten = calc.CalculateShanten(counts, 0);
                ukeire[kind] = (shanten, TileEfficiency.UkeireExhaustive(calc, counts, 0, visible));
                counts[kind]++;
                bestShanten = Math.Min(bestShanten, shanten);
            }

            if (bestShanten < 1)
            {
                continue;
            }

            var chosen = _senior.ChooseDiscard(view, new Random(round)).ToTileIndex();
            var bestUkeire = ukeire.Values.Where(v => v.Shanten == bestShanten).Max(v => v.Ukeire);
            Assert.Equal(bestShanten, ukeire[chosen].Shanten);
            Assert.Equal(bestUkeire, ukeire[chosen].Ukeire);
            checkedHands++;
        }

        Assert.True(checkedHands > 200, $"Only {checkedHands} hands were usable.");
    }

    [Fact]
    public void FoldsToASafeTileAgainstAnOpenHandWhenFarFromWinning()
    {
        // A scattered hand facing three open sets: 5p is worth keeping (a pair), but the opponent threw it
        // earlier, so it can't be ron'd. That is the tile to throw.
        var hand = BotTable.Hand("1m 4m 7m 2p 5p 5p 8p 3s 6s 9s 1z 2z 3z 4z");
        var view = BotTable.View(hand, others: new[] { BotTable.OpenHandedOpponent(1, thrown: "5p") });
        for (var seed = 0; seed < 20; seed++)
        {
            Assert.Equal(T.Parse("5p"), _senior.ChooseDiscard(view, new Random(seed)));
        }
    }

    [Fact]
    public void DoesNotFoldWhileTenpai()
    {
        // Same threat, but this hand is one tile from winning: throw 9p and stay tenpai, not the safe 4m.
        var hand = BotTable.Hand("1m 2m 3m 4m 5m 4p 5p 6p 7s 8s 9s 1z 1z 9p");
        var view = BotTable.View(hand, others: new[] { BotTable.OpenHandedOpponent(1, thrown: "4m") });
        Assert.Equal(T.Parse("9p"), _senior.ChooseDiscard(view, new Random(1)));
    }

    [Fact]
    public void TwoOpenSetsEarlyIsNotEnoughToFold()
    {
        // The same tiles are face-up either way (so every unseen count matches), but here they are two open
        // sets that opponent has only just made, which is not yet a reason to give up.
        var hand = BotTable.Hand("1m 4m 7m 2p 5p 5p 8p 3s 6s 9s 1z 2z 3z 4z");
        var early = BotTable.Seat(1, "", "9m 5p", T.Chi("1s 2s 3s"), T.Chi("4s 5s 6s"));
        var sameTilesInARiver = BotTable.Seat(1, "1s 2s 3s 4s 5s 6s", "9m 5p");

        var calm = BotTable.View(hand, others: new[] { early });
        var plain = BotTable.View(hand, others: new[] { sameTilesInARiver });
        for (var seed = 0; seed < 10; seed++)
        {
            Assert.Equal(_senior.ChooseDiscard(plain, new Random(seed)), _senior.ChooseDiscard(calm, new Random(seed)));
        }
    }

    [Fact]
    public void TwoOpenSetsLateInTheRoundIsWorthFoldingFor()
    {
        var hand = BotTable.Hand("1m 4m 7m 2p 5p 5p 8p 3s 6s 9s 1z 2z 3z 4z");
        var late = BotTable.Seat(1, "", "9m 9p 8m 6p 6m 5p", T.Chi("1s 2s 3s"), T.Chi("4s 5s 6s"));
        var view = BotTable.View(hand, others: new[] { late });
        Assert.Equal(T.Parse("5p"), _senior.ChooseDiscard(view, new Random(1)));
    }
    [Fact]
    public void RespectsTheSwapCallingRule()
    {
        var hand = BotTable.Hand("1m 4m 7m 2p 5p 5p 8p 3s 6s 9s 1z 2z 3z 4z");
        for (var forbidden = 0; forbidden < 14; forbidden++)
        {
            var barred = hand.Tiles[forbidden];
            var view = BotTable.View(hand, forbidden: barred.ToString());
            for (var seed = 0; seed < 5; seed++)
            {
                Assert.NotEqual(barred, _senior.ChooseDiscard(view, new Random(seed)));
            }
        }
    }

    [Fact]
    public void NeverTakesAnOpenKan()
    {
        var hand = BotTable.Hand("5z 5z 5z 1m 2m 3m 4p 5p 6p 7s 8s 9s 1z");
        var view = BotTable.View(hand);
        Assert.True(_middle.WantsOpenKan(view, T.Parse("5z")));
        Assert.False(_senior.WantsOpenKan(view, T.Parse("5z")));
    }

    [Fact]
    public void WillNotOpenItsHandWhileFolding()
    {
        // A pair of green dragons next to three finished sets: the Middle bot pons, and so does the Senior bot...
        var hand = BotTable.Hand("6z 6z 1m 2m 3m 4p 5p 6p 7s 8s 9s 1z 2z");
        var calm = BotTable.View(hand);
        Assert.True(_middle.WantsPon(calm, T.Parse("6z")));
        Assert.True(_senior.WantsPon(calm, T.Parse("6z")));

        // ...but not while an opponent has three open sets.
        var threatened = BotTable.View(hand, others: new[] { BotTable.OpenHandedOpponent(1) });
        Assert.False(_senior.WantsPon(threatened, T.Parse("6z")));
    }

    [Fact]
    public void WillNotChiWhileFolding()
    {
        var hand = BotTable.Hand("4p 5p 2m 3m 4m 6m 7m 3s 4s 5s 6s 7s 8s");
        var options = new[] { new ChiOption(T.Parse("4p"), T.Parse("5p")) };

        var calm = BotTable.View(hand);
        Assert.NotNull(_middle.ChooseChi(calm, T.Parse("6p"), options));
        Assert.NotNull(_senior.ChooseChi(calm, T.Parse("6p"), options));

        var threatened = BotTable.View(hand, others: new[] { BotTable.OpenHandedOpponent(2) });
        Assert.Null(_senior.ChooseChi(threatened, T.Parse("6p"), options));
    }

    [Fact]
    public void AddedKanIsSkippedWhenAnOpponentLooksDangerous()
    {
        // Three 9s in hand, two of them go to a pon, and the third is the fourth tile that makes an added kan.
        var hand = BotTable.Hand("9s 9s 9s 1m 2m 3m 4p 5p 6p 7p 8p 9p 5z");
        hand.Pon(T.Parse("9s"), 1);
        Assert.Contains(hand.KanOptions(), o => o.Type == MeldType.Kakan);

        Assert.NotNull(_senior.ChooseSelfKan(BotTable.View(hand)));
        Assert.Null(_senior.ChooseSelfKan(BotTable.View(hand, others: new[] { BotTable.OpenHandedOpponent(1) })));
    }

    internal static PlayerHand RandomHand(Random rng, int tiles)
    {
        var pool = Enumerable.Range(0, 34).SelectMany(kind => Enumerable.Repeat(kind, 4)).OrderBy(_ => rng.Next()).ToList();
        var hand = new PlayerHand();
        foreach (var kind in pool.Take(tiles))
        {
            hand.Add(Tile.FromTileIndex(kind));
        }

        hand.Sort();
        return hand;
    }
}

public class TileEfficiencyTests
{
    private static readonly ShantenCalculator Calc = new();

    [Fact]
    public void CountsTheTilesThatCompleteATenpaiHand()
    {
        // 123m 456p 789s 11z and 56m wait on 4m or 7m.
        var hand = BotTable.Hand("1m 2m 3m 5m 6m 4p 5p 6p 7s 8s 9s 1z 1z");
        var counts = hand.TileCounts();

        Assert.Equal(new[] { T.Parse("4m").ToTileIndex(), T.Parse("7m").ToTileIndex() }, TileEfficiency.WaitKinds(Calc, counts, 0));
        Assert.Equal(8, TileEfficiency.Ukeire(Calc, counts, 0, counts));
    }

    [Fact]
    public void TilesAlreadySeenAreNotCounted()
    {
        var hand = BotTable.Hand("1m 2m 3m 5m 6m 4p 5p 6p 7s 8s 9s 1z 1z");
        var counts = hand.TileCounts();
        var visible = (int[])counts.Clone();
        visible[T.Parse("4m").ToTileIndex()] = 3;   // three 4m on the table
        visible[T.Parse("7m").ToTileIndex()] = 4;   // all four 7m gone

        Assert.Equal(1, TileEfficiency.Ukeire(Calc, counts, 0, visible));
    }

    [Fact]
    public void AHandThatIsNotTenpaiHasNoWaits()
    {
        var counts = BotTable.Hand("1m 4m 7m 2p 5p 8p 3s 6s 9s 1z 2z 3z 4z").TileCounts();
        Assert.Empty(TileEfficiency.WaitKinds(Calc, counts, 0));
    }

    [Fact]
    public void WaitKindsIncludeTilesWithNoCopiesLeft()
    {
        // Furiten depends on the wait itself, not on whether a copy is still out there.
        var counts = BotTable.Hand("1m 2m 3m 5m 6m 4p 5p 6p 7s 8s 9s 1z 1z").TileCounts();
        Assert.Equal(2, TileEfficiency.WaitKinds(Calc, counts, 0).Count);
    }

    [Fact]
    public void PrunedSearchGivesTheSameAnswerAsTryingEveryTile()
    {
        var rng = new Random(77);
        for (var i = 0; i < 1500; i++)
        {
            var meldCount = i % 3;
            var concealed = 13 - 3 * meldCount;
            var counts = i % 5 == 0 ? PairsHand(rng, concealed) : i % 7 == 0 ? OrphansHand(rng, concealed) : SeniorBotTests.RandomHand(rng, concealed).TileCounts();
            var visible = (int[])counts.Clone();
            for (var extra = 0; extra < 12; extra++)
            {
                var kind = rng.Next(34);
                if (visible[kind] < 4)
                {
                    visible[kind]++;
                }
            }

            Assert.Equal(
                TileEfficiency.UkeireExhaustive(Calc, counts, meldCount, visible),
                TileEfficiency.Ukeire(Calc, counts, meldCount, visible));
        }
    }

    // Mostly pairs, so seven pairs is the closest shape.
    private static int[] PairsHand(Random rng, int tiles)
    {
        var counts = new int[34];
        var placed = 0;
        while (placed + 2 <= tiles - 1)
        {
            var kind = rng.Next(34);
            if (counts[kind] == 0)
            {
                counts[kind] = 2;
                placed += 2;
            }
        }

        while (placed < tiles)
        {
            var kind = rng.Next(34);
            if (counts[kind] < 2)
            {
                counts[kind]++;
                placed++;
            }
        }

        return counts;
    }

    // Mostly terminals and honours, so thirteen orphans is the closest shape.
    private static int[] OrphansHand(Random rng, int tiles)
    {
        var orphans = Enumerable.Range(0, 34).Where(k => k >= 27 || k % 9 == 0 || k % 9 == 8).OrderBy(_ => rng.Next()).ToList();
        var counts = new int[34];
        foreach (var kind in orphans.Take(Math.Min(tiles, 11)))
        {
            counts[kind]++;
        }

        var placed = counts.Sum();
        while (placed < tiles)
        {
            var kind = rng.Next(34);
            if (counts[kind] < 4)
            {
                counts[kind]++;
                placed++;
            }
        }

        return counts;
    }
}

public class ArenaTests
{
    private static readonly BotLevel[] Ladder = { BotLevel.Junior, BotLevel.Middle, BotLevel.Senior, BotLevel.Senior };

    [Fact]
    public void LevelsRotateThroughEverySeat()
    {
        var report = Arena.Run(Ladder, 40);
        Assert.Equal(40, report.For(BotLevel.Junior).SeatsPlayed);
        Assert.Equal(40, report.For(BotLevel.Middle).SeatsPlayed);
        Assert.Equal(80, report.For(BotLevel.Senior).SeatsPlayed);
    }

    [Fact]
    public void WinsAndDealInsAddUp()
    {
        var report = Arena.Run(Ladder, 100);
        var wins = report.Levels.Sum(l => l.Wins);
        var tsumo = report.Levels.Sum(l => l.Tsumo);
        var dealIns = report.Levels.Sum(l => l.DealIns);

        Assert.Equal(100, wins + report.ExhaustiveDraws);
        Assert.Equal(wins - tsumo, dealIns);
    }

    [Fact]
    public void ARunCanBeRepeatedExactly()
    {
        Assert.Equal(Arena.Run(Ladder, 24, firstSeed: 5).Levels, Arena.Run(Ladder, 24, firstSeed: 5).Levels);
    }

    [Fact]
    public void BadArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Arena.Run(null!, 10));
        Assert.Throws<ArgumentException>(() => Arena.Run(new[] { BotLevel.Middle }, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => Arena.Run(Ladder, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Arena.Run(Ladder, -4));
    }

    [Fact]
    public void LevelsFormALadderFromJuniorToSenior()
    {
        // 400 fixed seeds: Junior wins about 3%, Middle about 15%, Senior about 29%, so these margins are wide.
        var report = Arena.Run(Ladder, 400);
        var junior = report.For(BotLevel.Junior);
        var middle = report.For(BotLevel.Middle);
        var senior = report.For(BotLevel.Senior);

        Assert.True(junior.WinRate < middle.WinRate, $"Junior {junior.WinRate:P1} vs Middle {middle.WinRate:P1}");
        Assert.True(middle.WinRate < senior.WinRate, $"Middle {middle.WinRate:P1} vs Senior {senior.WinRate:P1}");
        Assert.True(senior.WinRate > 2 * junior.WinRate);
        Assert.True(senior.DealInRate < junior.DealInRate, $"Senior {senior.DealInRate:P1} vs Junior {junior.DealInRate:P1}");
    }
}
