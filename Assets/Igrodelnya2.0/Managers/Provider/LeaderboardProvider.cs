using System;
using System.Collections.Generic;
using UnityEngine;

public struct LBRecord
{
    public string Name;
    public int Rank;
    public int Score;
    public string URL;
}


public struct LBData
{
    public LBName LBName;
    public List<LBRecord> Records;
}
public abstract class LeaderboardProvider: MonoBehaviour
{
    public abstract void LoadLB(LBName LBTag, int topAmount, bool includePlayer, Action<LBData> onLoad);
    public abstract void SaveScore(string LBName, double score);
}