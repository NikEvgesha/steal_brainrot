using MirraGames.SDK;
using MirraGames.SDK.Common;
using System;
using UnityEngine;

public class MirraSDKLeaderboardProvider : LeaderboardProvider
{
    public override void LoadLB(LBName LBTag, int topAmount, bool includePlayer, Action<LBData> onLoad)
    {
        if (!LeaderboardService.RuntimeEnabled || !LeaderboardService.RemoteRequestsEnabled)
        {
            onLoad?.Invoke(new LBData
            {
                LBName = LBTag,
                Records = new()
            });
            return;
        }

        MirraLeaderboardBridge.GetLeaderboard(
            LBTag.ToString(),
            (success, scoreTable) =>
            {
                LBData data = new();
                data.LBName = LBTag;
                data.Records = new();

                if (!success || scoreTable == null)
                {
                    onLoad?.Invoke(data);
                    return;
                }

                foreach (PlayerScore player in scoreTable.players)
                {
                    LBRecord rec = new LBRecord();
                    rec.Name = player.displayName;
                    rec.Rank = player.position;
                    rec.Score = player.score;
                    rec.URL = player.profilePictureUrl;
                    data.Records.Add(rec);
                }

                Debug.Log("onScoreTableResolve : " + scoreTable.players.Length);
                onLoad?.Invoke(data);
            }
        );
    }


    public override void SaveScore(string LBName, double score)
    {
        if (!LeaderboardService.RuntimeEnabled || !LeaderboardService.RemoteRequestsEnabled)
            return;

        int safeScore = double.IsNaN(score) || score <= 0d
            ? 0
            : score >= int.MaxValue
                ? int.MaxValue
                : (int)Math.Round(score);
        MirraLeaderboardBridge.SetScore(LBName, safeScore);
        Debug.Log(LBName + " set score " + safeScore);
    }
}
