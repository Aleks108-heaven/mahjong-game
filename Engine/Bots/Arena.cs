using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TestMahjongGame.Engine;

// What one level achieved over an arena run. Rates are per seat played, so levels that sit at the table
// more than once are comparable with those that sit once.
public sealed record LevelStats(BotLevel Level, int SeatsPlayed, int Wins, int Tsumo, int DealIns)
{
    public double WinRate => SeatsPlayed == 0 ? 0 : (double)Wins / SeatsPlayed;
    public double DealInRate => SeatsPlayed == 0 ? 0 : (double)DealIns / SeatsPlayed;
}

public sealed record ArenaReport(int Rounds, int ExhaustiveDraws, IReadOnlyList<LevelStats> Levels)
{
    public LevelStats For(BotLevel level)
    {
        return Levels.Single(l => l.Level == level);
    }
}

// Plays many seeded rounds between bots to compare how strong the levels are.
public static class Arena
{
    // table says which level sits in each seat in round 0. The levels shift one seat every round, so over a
    // multiple of four rounds each level spends equally long in every seat (East draws first).
    // Round i uses seed firstSeed + i, so a run can be repeated exactly.
    public static ArenaReport Run(IReadOnlyList<BotLevel> table, int rounds, int firstSeed = 1)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count != 4)
        {
            throw new ArgumentException("A table has four seats.", nameof(table));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rounds);

        var results = new (BotLevel[] Seats, RoundResult Result)[rounds];
        Parallel.For(0, rounds, i =>
        {
            var seats = Enumerable.Range(0, 4).Select(seat => table[(seat + i) % 4]).ToArray();
            var engine = new GameEngine(unchecked(firstSeed + i), TextWriter.Null, seats);
            results[i] = (seats, engine.RunSimulation());
        });

        var seatsPlayed = new Dictionary<BotLevel, int>();
        var wins = new Dictionary<BotLevel, int>();
        var tsumo = new Dictionary<BotLevel, int>();
        var dealIns = new Dictionary<BotLevel, int>();
        var draws = 0;

        foreach (var (seats, result) in results)
        {
            foreach (var level in seats)
            {
                seatsPlayed[level] = seatsPlayed.GetValueOrDefault(level) + 1;
            }

            if (result.Outcome == RoundOutcome.ExhaustiveDraw)
            {
                draws++;
                continue;
            }

            var winner = seats[result.WinnerSeat!.Value];
            wins[winner] = wins.GetValueOrDefault(winner) + 1;
            if (result.Outcome == RoundOutcome.Tsumo)
            {
                tsumo[winner] = tsumo.GetValueOrDefault(winner) + 1;
            }
            else
            {
                var loser = seats[result.LoserSeat!.Value];
                dealIns[loser] = dealIns.GetValueOrDefault(loser) + 1;
            }
        }

        var levels = seatsPlayed.Keys
            .OrderBy(level => level)
            .Select(level => new LevelStats(
                level,
                seatsPlayed[level],
                wins.GetValueOrDefault(level),
                tsumo.GetValueOrDefault(level),
                dealIns.GetValueOrDefault(level)))
            .ToList();
        return new ArenaReport(rounds, draws, levels);
    }
}
