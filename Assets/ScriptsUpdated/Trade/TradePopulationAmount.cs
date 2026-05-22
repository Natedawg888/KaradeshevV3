using System;

[Serializable]
public class TradePopulationAmount
{
    public int children;
    public int teens;
    public int adults;
    public int elders;

    public int Total => children + teens + adults + elders;
    public bool IsEmpty => Total <= 0;
}
