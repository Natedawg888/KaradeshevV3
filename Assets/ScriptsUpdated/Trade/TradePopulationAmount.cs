using System;
using System.Collections.Generic;

[Serializable]
public class TradePopulationEntry
{
    public AgeGroup ageGroup;
    public Gender gender;
    public int count;
}

[Serializable]
public class TradePopulationSlot
{
    [UnityEngine.Tooltip("Which age group this slot covers.")]
    public AgeGroup ageGroup;
    [UnityEngine.Tooltip("Which sex this slot covers.")]
    public Gender gender;
}

[Serializable]
public class TradePopulationAmount
{
    public List<TradePopulationEntry> entries = new List<TradePopulationEntry>();

    public int Total
    {
        get
        {
            int t = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null) t += entries[i].count;
            return t;
        }
    }

    public bool IsEmpty => Total <= 0;

    public int Get(AgeGroup age, Gender gender)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.ageGroup == age && e.gender == gender) return e.count;
        }
        return 0;
    }

    public void Add(AgeGroup age, Gender gender, int count)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.ageGroup == age && e.gender == gender) { e.count += count; return; }
        }
        entries.Add(new TradePopulationEntry { ageGroup = age, gender = gender, count = count });
    }
}
