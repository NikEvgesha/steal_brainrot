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

    [Serializable] private class FriendAddRequest { public string friendCode; }
    [Serializable] private class RenameRequest { public string displayName; }

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

}
