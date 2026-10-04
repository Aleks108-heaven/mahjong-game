using System.Collections.Generic;
using System.Linq;

namespace TestMahjongGame.Engine;

public static class Dora
{
    // The dora is the tile after the indicator: 9 wraps to 1 within a suit, winds go E>S>W>N>E,
    // dragons go white>green>red>white.
    public static Tile FromIndicator(Tile indicator)
    {
        if (!indicator.IsHonor)
        {
            return new Tile(indicator.Suit, indicator.Rank % 9 + 1);
        }

        var honor = (int)indicator.Honor!.Value;
        var next = honor <= (int)Honor.North
            ? (honor + 1) % 4
            : (int)Honor.White + (honor - (int)Honor.White + 1) % 3;
        return new Tile((Honor)next);
    }

    // How many dora a hand holds: every tile (melds included, all four of a kan) that is a dora,
    // counted once per indicator showing.
    public static int Count(IEnumerable<Tile> concealed, IEnumerable<Meld> melds, IEnumerable<Tile> indicators)
    {
        var all = concealed.Concat(melds.SelectMany(m => m.Tiles)).ToList();
        return indicators.Sum(indicator => all.Count(t => t.Equals(FromIndicator(indicator))));
    }
}
