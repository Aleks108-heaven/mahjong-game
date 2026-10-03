using System;

namespace TestMahjongGame.Engine;

public sealed class ShantenCalculator
{
    // Lowest shanten over all three winning shapes. -1 means the hand is complete.
    public int CalculateShanten(int[] tileCounts)
    {
        return Math.Min(
            CalculateStandardShanten(tileCounts),
            Math.Min(CalculateSevenPairsShanten(tileCounts), CalculateThirteenOrphansShanten(tileCounts)));
    }

    public int CalculateStandardShanten(int[] tileCounts)
    {
        Validate(tileCounts);

        var counts = new int[34];
        Array.Copy(tileCounts, counts, 34);
        var minShanten = 8;

        Search(counts, 0, 0, 0, 0, ref minShanten);
        return minShanten;
    }

    public int CalculateSevenPairsShanten(int[] tileCounts)
    {
        Validate(tileCounts);

        var pairs = 0;
        var kinds = 0;
        foreach (var count in tileCounts)
        {
            if (count > 0)
            {
                kinds++;
            }

            if (count >= 2)
            {
                pairs++;
            }
        }

        // Seven pairs need seven different tiles, so a missing kind adds to the distance.
        return 6 - pairs + Math.Max(0, 7 - kinds);
    }

    public int CalculateThirteenOrphansShanten(int[] tileCounts)
    {
        Validate(tileCounts);

        var kinds = 0;
        var hasPair = false;
        for (var i = 0; i < 34; i++)
        {
            if (!IsTerminalOrHonorIndex(i) || tileCounts[i] == 0)
            {
                continue;
            }

            kinds++;
            if (tileCounts[i] >= 2)
            {
                hasPair = true;
            }
        }

        return 13 - kinds - (hasPair ? 1 : 0);
    }

    private static void Validate(int[] tileCounts)
    {
        if (tileCounts.Length != 34)
        {
            throw new ArgumentException("tileCounts must have length 34.", nameof(tileCounts));
        }
    }

    private static bool IsTerminalOrHonorIndex(int index)
    {
        return index >= 27 || index % 9 == 0 || index % 9 == 8;
    }

    private static void Search(int[] counts, int index, int melds, int pairs, int taatsu, ref int minShanten)
    {
        while (index < 34 && counts[index] == 0)
        {
            index++;
        }

        if (index >= 34)
        {
            var cappedTaatsu = taatsu;
            if (melds + cappedTaatsu > 4)
            {
                cappedTaatsu = 4 - melds;
            }

            var cappedPairs = Math.Min(pairs, 1);
            var shanten = 8 - (melds * 2) - cappedPairs - cappedTaatsu;
            if (shanten < minShanten)
            {
                minShanten = shanten;
            }

            return;
        }

        if (counts[index] >= 3)
        {
            counts[index] -= 3;
            Search(counts, index, melds + 1, pairs, taatsu, ref minShanten);
            counts[index] += 3;
        }

        if (IsSequenceStart(index) && counts[index + 1] > 0 && counts[index + 2] > 0)
        {
            counts[index]--;
            counts[index + 1]--;
            counts[index + 2]--;
            Search(counts, index, melds + 1, pairs, taatsu, ref minShanten);
            counts[index]++;
            counts[index + 1]++;
            counts[index + 2]++;
        }

        if (pairs < 1 && counts[index] >= 2)
        {
            counts[index] -= 2;
            Search(counts, index, melds, pairs + 1, taatsu, ref minShanten);
            counts[index] += 2;
        }

        if (counts[index] >= 2)
        {
            counts[index] -= 2;
            Search(counts, index, melds, pairs, taatsu + 1, ref minShanten);
            counts[index] += 2;
        }

        if (IsSequenceStart(index) && counts[index + 1] > 0)
        {
            counts[index]--;
            counts[index + 1]--;
            Search(counts, index, melds, pairs, taatsu + 1, ref minShanten);
            counts[index]++;
            counts[index + 1]++;
        }

        if (IsSequenceStart(index) && counts[index + 2] > 0)
        {
            counts[index]--;
            counts[index + 2]--;
            Search(counts, index, melds, pairs, taatsu + 1, ref minShanten);
            counts[index]++;
            counts[index + 2]++;
        }

        counts[index]--;
        Search(counts, index, melds, pairs, taatsu, ref minShanten);
        counts[index]++;
    }

    private static bool IsSequenceStart(int index)
    {
        return index < 27 && index % 9 <= 6;
    }
}
