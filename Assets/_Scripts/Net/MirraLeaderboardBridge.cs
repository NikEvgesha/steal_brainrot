using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using MirraGames.SDK;
using MirraGames.SDK.Common;
using UnityEngine;

/// <summary>
/// Wraps Mirra leaderboard promises on WebGL so a missing/unpublished board
/// becomes a normal failed request instead of an unhandled JavaScript error.
/// </summary>
public static class MirraLeaderboardBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void zooMirraLeaderboardGetScore(int senderId, string boardId, DelegateInt callback);

    [DllImport("__Internal")]
    private static extern void zooMirraLeaderboardGetEntries(int senderId, string boardId, DelegateString callback);

    [DllImport("__Internal")]
    private static extern void zooMirraLeaderboardSetScore(string boardId, int score);

    private static readonly Dictionary<int, Action<bool, int>> ScoreCallbacks = new();
    private static readonly Dictionary<int, Action<bool, MirraGames.SDK.Common.Leaderboard>> EntriesCallbacks = new();
    private static int nextSenderId;
#endif

    public static void GetScore(string boardId, Action<bool, int> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int senderId = ++nextSenderId;
        ScoreCallbacks[senderId] = callback;
        zooMirraLeaderboardGetScore(senderId, boardId, OnScoreReceived);
#else
        MirraSDK.Achievements.GetScore(boardId, score => callback?.Invoke(true, score));
#endif
    }

    public static void GetLeaderboard(
        string boardId,
        Action<bool, MirraGames.SDK.Common.Leaderboard> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        int senderId = ++nextSenderId;
        EntriesCallbacks[senderId] = callback;
        zooMirraLeaderboardGetEntries(senderId, boardId, OnEntriesReceived);
#else
        MirraSDK.Achievements.GetLeaderboard(boardId, leaderboard => callback?.Invoke(leaderboard != null, leaderboard));
#endif
    }

    public static void SetScore(string boardId, int score)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        zooMirraLeaderboardSetScore(boardId, score);
#else
        MirraSDK.Achievements.SetScore(boardId, score);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [MonoPInvokeCallback(typeof(DelegateInt))]
    private static void OnScoreReceived(int senderId, int value)
    {
        if (!ScoreCallbacks.Remove(senderId, out var callback))
            return;

        bool success = value >= 0;
        callback?.Invoke(success, success ? value : 0);
    }

    [MonoPInvokeCallback(typeof(DelegateString))]
    private static void OnEntriesReceived(int senderId, string json)
    {
        if (!EntriesCallbacks.Remove(senderId, out var callback))
            return;

        if (string.IsNullOrEmpty(json))
        {
            callback?.Invoke(false, null);
            return;
        }

        try
        {
            callback?.Invoke(true, JsonUtility.FromJson<MirraGames.SDK.Common.Leaderboard>(json));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Leaderboards] Cannot parse Mirra response: {exception.Message}");
            callback?.Invoke(false, null);
        }
    }
#endif
}
