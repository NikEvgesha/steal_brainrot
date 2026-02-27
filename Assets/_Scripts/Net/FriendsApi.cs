using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class FriendsApi : MonoBehaviour
{
    [SerializeField] private string baseUrl = "https://api.igrodelnya-zoogame.ru";
    private SaveManager saveManager;

    [Serializable]
    public class GuestAuthResponse
    {
        public string playerId;
        public string friendCode;
        public string displayName;
        public bool renameFreeAvailable;
    }

    [Serializable]
    public class FriendItem
    {
        public string friendCode;
        public string displayName;
        public bool isOnline;
        public string lastSeenAt;
    }

    [Serializable]
    public class FriendRequestItem
    {
        public string requestId;
        public string friendCode;
        public string displayName;
        public string createdAt;
    }

    [Serializable]
    public class LikeStateResponse
    {
        public int likesCount;
        public bool canLike;
        public bool likedToday;
        public string nextLikeAtUtc;
        public string targetPlayerId;
        public string error;
    }

    [Serializable]
    public class LikeSendResponse
    {
        public bool ok;
        public int likesCount;
        public bool canLike;
        public bool likedToday;
        public string nextLikeAtUtc;
        public string targetPlayerId;
        public string error;
    }


    [Serializable] private class FriendAddRequest { public string friendCode; }
    [Serializable] private class FriendRequestCreateRequest { public string targetFriendCode; }
    [Serializable] private class FriendRequestDecisionRequest { public string requestId; }
    [Serializable] private class RenameRequest { public string displayName; }
    [Serializable] private class LikeActionRequest { public string targetPlayerId; public string targetFriendCode; }

    private void Awake()
    {
        if (saveManager == null) saveManager = G.Save;
    }

    public (string playerId, string friendCode, string displayName) LocalProfile()
        => saveManager.LoadBackendProfile();

    public IEnumerator EnsureGuest()
    {
        var p = LocalProfile();
        if (!string.IsNullOrEmpty(p.playerId) && !string.IsNullOrEmpty(p.friendCode))
            yield break;

        yield return AuthGuest();
    }

    public IEnumerator AuthGuest(Action<GuestAuthResponse> onOk = null, Action<long, string> onErr = null)
    {
        var url = baseUrl + "/auth/guest";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        var resp = JsonUtility.FromJson<GuestAuthResponse>(req.downloadHandler.text);
        saveManager.SaveBackendProfile(resp.playerId, resp.friendCode, resp.displayName);
        onOk?.Invoke(resp);
    }

    public IEnumerator PresencePing()
    {
        var p = LocalProfile();
        if (string.IsNullOrEmpty(p.playerId)) yield break;

        var url = baseUrl + "/presence/ping";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        // ошибки можно игнорить (просто онлайн-индикатор)
    }

    public IEnumerator GetFriends(Action<List<FriendItem>> onOk, Action<long, string> onErr = null)
    {
        var p = LocalProfile();
        if (string.IsNullOrEmpty(p.playerId))
        {
            onErr?.Invoke(0, "No playerId. Call EnsureGuest first.");
            yield break;
        }

        var url = baseUrl + "/friends";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        // массив -> обёртка
        var wrapper = JsonUtility.FromJson<FriendsWrapper>("{\"items\":" + req.downloadHandler.text + "}");
        onOk?.Invoke(wrapper.items ?? new List<FriendItem>());
    }

    [Serializable] private class FriendsWrapper { public List<FriendItem> items; }
    [Serializable] private class FriendRequestsWrapper { public List<FriendRequestItem> items; }

    public IEnumerator AddFriend(string friendCode, Action<bool> onOk = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/friends/add";

        var body = new FriendAddRequest { friendCode = (friendCode ?? "").Trim().ToUpperInvariant() };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    public IEnumerator SendFriendRequest(string friendCode, Action<bool> onOk = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/friends/request";

        var body = new FriendRequestCreateRequest { targetFriendCode = (friendCode ?? "").Trim().ToUpperInvariant() };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    public IEnumerator GetFriendRequests(Action<List<FriendRequestItem>> onOk, Action<long, string> onErr = null)
    {
        var p = LocalProfile();
        if (string.IsNullOrEmpty(p.playerId))
        {
            onErr?.Invoke(0, "No playerId. Call EnsureGuest first.");
            yield break;
        }

        var url = baseUrl + "/friends/requests";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        var wrapper = JsonUtility.FromJson<FriendRequestsWrapper>("{\"items\":" + req.downloadHandler.text + "}");
        onOk?.Invoke(wrapper.items ?? new List<FriendRequestItem>());
    }

    public IEnumerator AcceptFriendRequest(string requestId, Action<bool> onOk = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/friends/requests/accept";

        var body = new FriendRequestDecisionRequest { requestId = requestId };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    public IEnumerator DeclineFriendRequest(string requestId, Action<bool> onOk = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/friends/requests/decline";

        var body = new FriendRequestDecisionRequest { requestId = requestId };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    public IEnumerator RemoveFriend(string friendCode, Action<bool> onOk = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/friends/remove";

        var body = new FriendAddRequest { friendCode = (friendCode ?? "").Trim().ToUpperInvariant() };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    [Serializable]
    private class PaidRequiredResponse
    {
        public string error;
        public string currentDisplayName;
    }

    public IEnumerator RenameMe(string newName, Action<bool> onOk = null, Action<string> onFail = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/me/name";

        var body = new RenameRequest { displayName = (newName ?? "").Trim() };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            // 403: сервер вернёт currentDisplayName — восстановим его в сохранениях (на всякий случай)
            if (req.responseCode == 403)
            {
                try
                {
                    var resp = JsonUtility.FromJson<PaidRequiredResponse>(req.downloadHandler.text);
                    if (!string.IsNullOrEmpty(resp?.currentDisplayName))
                        saveManager.SaveBackendProfile(p.playerId, p.friendCode, resp.currentDisplayName);
                }
                catch { /* ignore */ }

                onFail?.Invoke("Бесплатная смена уже использована. Нажми кнопку платной смены.");
            }
            else
            {
                onFail?.Invoke(string.IsNullOrEmpty(req.downloadHandler.text) ? "Не удалось сменить ник" : req.downloadHandler.text);
            }

            onOk?.Invoke(false);
            yield break;
        }

        // success -> сохраняем
        saveManager.SaveBackendProfile(p.playerId, p.friendCode, body.displayName);
        onOk?.Invoke(true);
    }
    public IEnumerator RenameMePaid(string newName, Action<bool> onOk = null, Action<string> onFail = null)
    {
        var p = LocalProfile();
        var url = baseUrl + "/me/name/paid";

        var body = new RenameRequest { displayName = (newName ?? "").Trim() };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onFail?.Invoke(string.IsNullOrEmpty(req.downloadHandler.text) ? "Не удалось сменить ник" : req.downloadHandler.text);
            onOk?.Invoke(false);
            yield break;
        }

        saveManager.SaveBackendProfile(p.playerId, p.friendCode, body.displayName);
        onOk?.Invoke(true);
    }

    public IEnumerator GetLikeState(
        string targetPlayerId,
        string targetFriendCode,
        Action<LikeStateResponse> onOk = null,
        Action<long, string> onErr = null)
    {
        var p = LocalProfile();
        if (string.IsNullOrWhiteSpace(p.playerId))
        {
            onErr?.Invoke(0, "No playerId. Call EnsureGuest first.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(targetPlayerId) && string.IsNullOrWhiteSpace(targetFriendCode))
        {
            onErr?.Invoke(0, "targetPlayerId or targetFriendCode required.");
            yield break;
        }

        var url = baseUrl + "/likes/state";
        var body = new LikeActionRequest
        {
            targetPlayerId = string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId.Trim(),
            targetFriendCode = string.IsNullOrWhiteSpace(targetFriendCode) ? null : targetFriendCode.Trim().ToUpperInvariant()
        };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        LikeStateResponse resp;
        try
        {
            resp = JsonUtility.FromJson<LikeStateResponse>(req.downloadHandler.text) ?? new LikeStateResponse();
        }
        catch
        {
            resp = new LikeStateResponse();
        }

        onOk?.Invoke(resp);
    }

    public IEnumerator SendLike(
        string targetPlayerId,
        string targetFriendCode,
        Action<LikeSendResponse> onOk = null,
        Action<long, string> onErr = null)
    {
        var p = LocalProfile();
        if (string.IsNullOrWhiteSpace(p.playerId))
        {
            onErr?.Invoke(0, "No playerId. Call EnsureGuest first.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(targetPlayerId) && string.IsNullOrWhiteSpace(targetFriendCode))
        {
            onErr?.Invoke(0, "targetPlayerId or targetFriendCode required.");
            yield break;
        }

        var url = baseUrl + "/likes/send";
        var body = new LikeActionRequest
        {
            targetPlayerId = string.IsNullOrWhiteSpace(targetPlayerId) ? null : targetPlayerId.Trim(),
            targetFriendCode = string.IsNullOrWhiteSpace(targetFriendCode) ? null : targetFriendCode.Trim().ToUpperInvariant()
        };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("X-Player-Id", p.playerId);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        LikeSendResponse resp;
        try
        {
            resp = JsonUtility.FromJson<LikeSendResponse>(req.downloadHandler.text) ?? new LikeSendResponse();
        }
        catch
        {
            resp = new LikeSendResponse();
        }

        onOk?.Invoke(resp);
    }

}
