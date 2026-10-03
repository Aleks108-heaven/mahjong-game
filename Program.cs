using TestMahjongGame.Engine;

// Usage: TestMahjongGame [seed]   (a seed makes the round reproducible)
int? seed = null;
if (args.Length > 0)
{
    if (!int.TryParse(args[0], out var parsed))
    {
        Console.Error.WriteLine("Usage: TestMahjongGame [seed]  (seed must be an integer)");
        return 1;
    }

    seed = parsed;
}

var engine = new GameEngine(seed);
engine.RunSimulation();
return 0;
