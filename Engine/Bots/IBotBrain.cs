using System;
using System.Collections.Generic;

namespace TestMahjongGame.Engine;

// The decisions a computer player makes. The engine asks, checks that the answer is legal, and carries it out.
// A brain keeps no state between calls; everything it needs is in the TableView.
public interface IBotBrain
{
    BotLevel Level { get; }

    // Which tile to throw from a hand that has just drawn (or just called). Must not be a forbidden tile
    // unless every tile in the hand is forbidden.
    Tile ChooseDiscard(TableView view, Random rng);

    // A closed or added kan to declare on its own turn, or null to carry on.
    KanOption? ChooseSelfKan(TableView view);

    // Whether to take another player's discard as an open kan or a pon. The engine only asks when the hand
    // holds enough matching tiles and (for a kan) the table rules allow it.
    bool WantsOpenKan(TableView view, Tile discard);
    bool WantsPon(TableView view, Tile discard);

    // Which run to chi the discard into, or null to pass. options is never empty.
    ChiOption? ChooseChi(TableView view, Tile discard, IReadOnlyList<ChiOption> options);
}

public static class BotBrains
{
    public static IBotBrain For(BotLevel level)
    {
        return level switch
        {
            BotLevel.Junior => new JuniorBrain(),
            BotLevel.Middle => new MiddleBrain(),
            BotLevel.Senior => new SeniorBrain(),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown bot level.")
        };
    }
}
