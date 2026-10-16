using System.Globalization;
using TestMahjongGame.Engine;

const string Usage = """
    Usage:
      TestMahjongGame [seed] [--level junior|middle|senior]
          Plays one round between four bots and prints it. A seed repeats the same round.
      TestMahjongGame arena [rounds] [--first-seed N] [--table L,L,L,L]
          Seats bots of different levels against each other and prints win and deal-in rates.
          The table lists the level in each of the four seats, for example junior,middle,senior,middle.
    """;

if (args.Length > 0 && args[0].Equals("arena", StringComparison.OrdinalIgnoreCase))
{
    return RunArena(args[1..]);
}

return RunRound(args);

static int RunRound(string[] args)
{
    int? seed = null;
    var level = BotLevel.Middle;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--level" && i + 1 < args.Length && TryParseLevel(args[i + 1], out level))
        {
            i++;
        }
        else if (!args[i].StartsWith("--", StringComparison.Ordinal)
                 && seed is null
                 && int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            seed = parsed;
        }
        else
        {
            return Fail("Unknown or invalid argument: " + args[i]);
        }
    }

    new GameEngine(seed, botLevel: level).RunSimulation();
    return 0;
}

static int RunArena(string[] args)
{
    var rounds = 400;
    var firstSeed = 1;
    var table = new[] { BotLevel.Junior, BotLevel.Middle, BotLevel.Senior, BotLevel.Senior };

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--first-seed" && i + 1 < args.Length
            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out firstSeed))
        {
            i++;
        }
        else if (args[i] == "--table" && i + 1 < args.Length && TryParseTable(args[i + 1], out table))
        {
            i++;
        }
        else if (i == 0 && int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out rounds) && rounds > 0)
        {
            // The first argument is the number of rounds.
        }
        else
        {
            return Fail("Unknown or invalid argument: " + args[i]);
        }
    }

    Console.WriteLine($"Arena: {rounds} rounds, seeds {firstSeed}..{firstSeed + rounds - 1}, seats {string.Join(", ", table)} (rotated every round)");
    var report = Arena.Run(table, rounds, firstSeed);

    Console.WriteLine();
    Console.WriteLine($"{"Level",-8} {"Seats",6} {"Wins",6} {"Win rate",9} {"Tsumo",6} {"Deal-ins",9} {"Deal-in rate",13}");
    foreach (var stats in report.Levels)
    {
        Console.WriteLine(
            $"{stats.Level,-8} {stats.SeatsPlayed,6} {stats.Wins,6} {stats.WinRate,9:P1} {stats.Tsumo,6} {stats.DealIns,9} {stats.DealInRate,13:P1}");
    }

    Console.WriteLine();
    Console.WriteLine($"Exhaustive draws: {report.ExhaustiveDraws} of {report.Rounds}");
    return 0;
}

static bool TryParseLevel(string text, out BotLevel level)
{
    return Enum.TryParse(text, ignoreCase: true, out level) && Enum.IsDefined(level);
}

static bool TryParseTable(string text, out BotLevel[] table)
{
    var parts = text.Split(',', StringSplitOptions.TrimEntries);
    table = new BotLevel[4];
    if (parts.Length != 4)
    {
        return false;
    }

    for (var i = 0; i < 4; i++)
    {
        if (!TryParseLevel(parts[i], out table[i]))
        {
            return false;
        }
    }

    return true;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Usage);
    return 1;
}
