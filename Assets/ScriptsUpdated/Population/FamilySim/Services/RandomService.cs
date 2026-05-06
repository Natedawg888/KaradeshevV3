using System;
using System.Collections.Generic;

public class RandomService
{
    private readonly Random _rng;

    public RandomService(int seed = 0)
    {
        _rng = (seed == 0) ? new Random() : new Random(seed);
    }

    public float Value01()
    {
        return (float)_rng.NextDouble();
    }

    /// <summary>Inclusive min, inclusive max.</summary>
    public int RangeInt(int inclusiveMin, int inclusiveMax)
    {
        if (inclusiveMax < inclusiveMin) (inclusiveMin, inclusiveMax) = (inclusiveMax, inclusiveMin);
        // Random.Next is [min, maxExclusive)
        return _rng.Next(inclusiveMin, inclusiveMax + 1);
    }

    public void Shuffle<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
