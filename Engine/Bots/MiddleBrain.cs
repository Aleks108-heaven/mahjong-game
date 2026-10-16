using System;
using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

// The original bot, moved here unchanged so existing seeds play out exactly as before:
// discard for the lowest shanten (ties random), call only when it lowers shanten and a yaku stays possible,
// and kan whenever it doesn't make the hand worse.
public sealed class MiddleBrain : IBotBrain
{
    public BotLevel Level => BotLevel.Middle;

    public Tile ChooseDiscard(TableView view, Random rng)
    {
        var hand = view.Hand;
        return hand.ChooseBestDiscard(
            rng,
            view.Forbidden,
            preferEdges: hand.IsPlayingForSimples(view.Seat, view.RoundWind));
    }

    public KanOption? ChooseSelfKan(TableView view)
    {
        if (!view.CanKan)
        {
            return null;
        }

        var hand = view.Hand;
        var options = hand.KanOptions();
        if (options.Count == 0)
        {
            return null;
        }

        var current = hand.BestDiscardShanten();
        foreach (var option in options)
        {
            if (hand.ShantenAfterKan(option) <= current)
            {
                return option;
            }
        }

        return null;
    }

    public bool WantsOpenKan(TableView view, Tile discard)
    {
        var hand = view.Hand;
        return view.CanKan
            && hand.CanDaiminkan(discard)
            && hand.ShantenAfterDaiminkan(discard) <= hand.GetShanten()
            && hand.OpenCallKeepsAYaku(
                new[] { discard, discard, discard, discard },
                new[] { discard, discard, discard },
                true,
                view.Seat,
                view.RoundWind);
    }

    public bool WantsPon(TableView view, Tile discard)
    {
        var hand = view.Hand;
        return hand.CanPon(discard)
            && hand.ShantenAfterPon(discard) < hand.GetShanten()
            && hand.OpenCallKeepsAYaku(
                new[] { discard, discard, discard },
                new[] { discard, discard },
                true,
                view.Seat,
                view.RoundWind);
    }

    public ChiOption? ChooseChi(TableView view, Tile discard, IReadOnlyList<ChiOption> options)
    {
        var hand = view.Hand;
        var best = options.OrderBy(option => hand.ShantenAfterChi(discard, option)).First();
        var run = new[] { discard, best.A, best.B }.OrderBy(t => t).ToList();
        var keepsYaku = hand.OpenCallKeepsAYaku(run, new[] { best.A, best.B }, false, view.Seat, view.RoundWind);
        return hand.ShantenAfterChi(discard, best) < hand.GetShanten() && keepsYaku ? best : null;
    }
}
