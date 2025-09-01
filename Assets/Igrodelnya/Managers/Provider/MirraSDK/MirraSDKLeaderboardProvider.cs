using MirraGames.SDK;
using MirraGames.SDK.Common;
using System;
using UnityEngine;

public class MirraSDKLeaderboardProvider : LeaderboardProvider
{
    public override void LoadLB(LBName LBTag, int topAmount, bool includePlayer, Action<LBData> onLoad)
    {
        MirraSDK.Achievements.GetLeaderboard(
            boardId: LBTag.ToString(),
            
            onLeaderboard: (scoreTable) => {
                LBData data = new();
                data.LBName = LBTag;
                data.Records = new();

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
                onLoad(data);
            }
        );
    }


    public override void SaveScore(string LBName, int score)
    {
        MirraSDK.Achievements.SetScore(
            boardId: LBName,
            score: score);
        //Debug.Log(LBName + " set score " + score);
    }
}