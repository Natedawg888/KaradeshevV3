using System;
using System.Collections.Generic;

[Serializable]
public class ScoreboardData
{
    public List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
}

[Serializable]
public class ScoreboardEntry
{
    public int score;
    public string playerName;
    public string civilizationName;
    public string avatarName;
}
