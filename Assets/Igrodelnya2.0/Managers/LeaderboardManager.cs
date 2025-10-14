using System;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    [SerializeField] private LeaderboardProvider _provider;
    [SerializeField] private List<Leaderboard> _leaderboards = new();

    public LeaderboardProvider Provider => _provider;

    private static LeaderboardManager _instance;
    public static LeaderboardManager Instance => _instance;

    public Action<List<LBRecord>> LBLoaded;


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    public void SaveScore(string LBName, double score)
    {
        _provider.SaveScore(LBName, score);
    }


    public void OnLBLoad(Leaderboard lb)
    {
        _leaderboards.Add(lb);
        _provider.LoadLB(lb.Name, lb.TopAmount, false, (table) => lb.DisplayTable(table));
    }

    public void OnLBDestroy(Leaderboard lb)
    {
        _leaderboards.Remove(lb);
    }

}