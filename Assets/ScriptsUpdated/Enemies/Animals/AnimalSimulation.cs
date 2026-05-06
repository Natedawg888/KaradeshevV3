using System;
using System.Collections.Generic;
using System.Linq;

public partial class AnimalSimulation
{

}

public struct HuntResult
{
    public bool success;
    public int animalsKilled;
    public int meatGained;
    public int hidesGained;

    public static HuntResult Invalid => new HuntResult
    {
        success = false,
        animalsKilled = 0,
        meatGained = 0,
        hidesGained = 0
    };
}