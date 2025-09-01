using UnityEngine;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using UnityEngine.UI;
using System.Collections.Generic;

public class Leaderboard : MonoBehaviour
{
    [SerializeField] private LBName LBname;
    [SerializeField] private int topAmount;

    [SerializeField] private Text _lbTitle;
    [SerializeField] private Transform _lb;
    [SerializeField] private LeaderboardRow _rowPrefab;

    public LBName Name => LBname;
    public int TopAmount => topAmount;


    public void Start()
    {
        LeaderboardManager.Instance.OnLBLoad(this);
    }

    private void OnDestroy()
    {
        LeaderboardManager.Instance.OnLBDestroy(this);
    }


    public void DisplayTable(LBData scoreTable)
    {
        if (scoreTable.Records != null)
        {
            for (int i = 0; i < scoreTable.Records.Count; i++)
            {
                LeaderboardRow row = Instantiate(_rowPrefab, _lb);
                row.Init(scoreTable.Records[i]);
            }
        } else
        {
            _lbTitle.text = scoreTable.LBName.ToString() + " error";
        }
    }
}
