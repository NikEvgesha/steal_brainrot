using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AuthGuestResponse
{
    public string playerId;
    public string friendCode;
    public string displayName;
    public bool renameFreeAvailable;
}

[Serializable]
public class ZooSaveRequest
{
    public string data;
}

public class ZooBackendClient : MonoBehaviour
{
    [Header("Server")]
    public string baseUrl = "https://api.igrodelnya-zoogame.ru";

    [Header("Deps")]
    private SaveManager saveManager; // можно не назначать — возьмём через G.Save
    [SerializeField] private FriendsApi friendsApi;   // опционально

    public FriendsApi FriendsApi { get { return friendsApi; } }

    private void Awake()
    {
        if (G.Backend == null)
        {

            G.Backend = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        if (saveManager == null) saveManager = G.Save;

    }

    private (string playerId, string friendCode, string displayName) Profile()
    {
        if (saveManager == null) saveManager = G.Save;
        return saveManager.LoadBackendProfile();
    }

    private string PlayerId => Profile().playerId;

    private void SetPlayerHeader(UnityWebRequest req)
    {
        var pid = PlayerId;
        if (!string.IsNullOrEmpty(pid))
            req.SetRequestHeader("X-Player-Id", pid);
    }

    // ===== AUTH (guest) =====
    public IEnumerator EnsureGuest(Action<AuthGuestResponse> onOk = null, Action<long, string> onErr = null)
    {
        var p = Profile();
        if (!string.IsNullOrEmpty(p.playerId) && !string.IsNullOrEmpty(p.friendCode))
        {
            onOk?.Invoke(new AuthGuestResponse
            {
                playerId = p.playerId,
                friendCode = p.friendCode,
                displayName = p.displayName,
                renameFreeAvailable = false // сервер отдаёт, но локально не храним — можно расширить позже
            });
            yield break;
        }

        yield return AuthGuest(onOk, onErr);
    }

    public IEnumerator AuthGuest(Action<AuthGuestResponse> onOk = null, Action<long, string> onErr = null)
    {
        var url = $"{baseUrl}/auth/guest";

        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        var resp = JsonUtility.FromJson<AuthGuestResponse>(req.downloadHandler.text);

        if (saveManager == null) saveManager = G.Save;
        saveManager.SaveBackendProfile(resp.playerId, resp.friendCode, resp.displayName);

        onOk?.Invoke(resp);
    }

    // ===== PRESENCE =====
    public IEnumerator PresencePing()
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/presence/ping";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        // можно игнорировать ошибки — это просто индикатор онлайна
    }

    // ===== ZOO =====
    // Примечание: эти эндпоинты должны существовать на сервере и принимать X-Player-Id.
    // Если /zoo/me пока нет — этот блок просто не вызывай.
    public IEnumerator LoadZoo(Action<string> onDone, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/zoo/me";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            onDone?.Invoke(null);
            yield break;
        }

        onDone?.Invoke(req.downloadHandler.text);
    }

    public IEnumerator SaveZoo(string zooJson, Action<bool> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/zoo/me";
        var body = new ZooSaveRequest { data = zooJson };
        var json = JsonUtility.ToJson(body);

        using var req = new UnityWebRequest(url, "PUT");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            onOk?.Invoke(false);
            yield break;
        }

        onOk?.Invoke(true);
    }
}
