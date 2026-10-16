using System;

namespace TestMahjongGame.Engine;

// How well a computer player plays, from a beginner to a strong club player.
public enum BotLevel
{
    // Still learning: slips up on a fifth of its discards, only pons value tiles, never chis or kans, never defends.
    Junior,

    // The original bot: lowest shanten with random ties, calls when a yaku stays possible, kans when it is free.
    Middle,

    // Counts the tiles it can see, aims for hands that can really win, and folds against open hands.
    Senior
}

public static class BotLevels
{
    // Accepts only the level names, in any case. Numbers such as "1" are not levels, although Enum.TryParse allows them.
    public static bool TryParse(string? text, out BotLevel level)
    {
        foreach (var candidate in Enum.GetValues<BotLevel>())
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                level = candidate;
                return true;
            }
        }

        level = default;
        return false;
    }
}