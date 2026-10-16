using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// A beginner: plays for a fast hand but slips up. About one discard in five is a random tile instead of a
// good one. It never chis or kans and only pons the tiles it was told score (dragons and the winds that
// count), so it rarely builds an open hand. It never thinks about what the others might be holding.
public sealed class JuniorBrain : IBotBrain
{
    // Chance that a discard is a random tile instead of the best one.
    public const double MistakeRate = 0.20;

    public BotLevel Level => BotLevel.Junior;

    public Tile ChooseDiscard(TableView view, Random rng)
    {
        var hand = view.Hand;
        if (rng.NextDouble() < MistakeRate)
        {
            var allowed = hand.Tiles.Where(t => !view.Forbidden.Contains(t)).ToList();
            if (allowed.Count == 0)
            {
                allowed = hand.Tiles.ToList();
            }

            return allowed[rng.Next(allowed.Count)];
        }

        return hand.ChooseBestDiscard(rng, view.Forbidden, preferEdges: false);
    }

    public KanOption? ChooseSelfKan(TableView view)
    {
        return null;
    }

    public bool WantsOpenKan(TableView view, Tile discard)
    {
        return false;
    }

    public bool WantsPon(TableView view, Tile discard)
    {
        return view.Hand.CanPon(discard) && PlayerHand.IsValueTile(discard, view.Seat, view.RoundWind);
    }

    public ChiOption? ChooseChi(TableView view, Tile discard, IReadOnlyList<ChiOption> options)
    {
        return null;
    }
}
