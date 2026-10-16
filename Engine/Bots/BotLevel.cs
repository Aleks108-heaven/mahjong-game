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
