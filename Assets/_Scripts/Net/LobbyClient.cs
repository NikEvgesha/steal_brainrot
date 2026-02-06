using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LobbyHandItemDto
{
    public string type;
    public string id;
}

[Serializable]
public class LobbyPosSampleDto
{
    public float dt;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class LobbyMemberStateDto
{
    public string playerId;
    public string friendCode;
    public string displayName;
    public bool isFriend;
    public bool isOnline;
    public string updatedAt;
    public BaseSnapshotDto baseData;
    public string baseDataRaw;
    public LobbyHandItemDto hand;
    public List<LobbyPosSampleDto> positions;
}

[Serializable]
public class GiftItemDto
{
    public string giftId;
    public string fromPlayerId;
    public string fromFriendCode;
    public string fromDisplayName;
    public string itemType;
    public string itemId;
    public string createdAt;
}

[Serializable]
public class GiftAcceptResponseDto
{
    public bool ok;
    public string giftId;
    public string itemType;
    public string itemId;
}

[Serializable]
public class LobbyStateResponseDto
{
    public string lobbyId;
    public long version;
    public List<LobbyMemberStateDto> members = new();
}

public class LobbyClient : MonoBehaviour
{
    public static LobbyClient Instance { get; private set; }

    [Header("Behavior")]
    [SerializeField] private bool autoJoinOnStart = true;
    [SerializeField] private float updateIntervalSec = 1f;
    [SerializeField] private float stateIntervalSec = 1f;
    [SerializeField] private int maxConsecutiveErrors = 3;
    [SerializeField] private float positionSampleRate = 30f;

    [Header("Deps (optional)")]
    [SerializeField] private ZooBackendClient backend;
    [SerializeField] private ZooBaseSnapshotSync snapshotSync;

    public bool IsOnline { get; private set; }
    public string LobbyId { get; private set; }
    public IReadOnlyList<LobbyMemberStateDto> LastMembers => _lastMembers;

    public event Action<List<LobbyMemberStateDto>> LobbyStateUpdated;

    private readonly List<LobbyMemberStateDto> _lastMembers = new();
    private Coroutine _updateLoop;
    private Coroutine _stateLoop;
    private Coroutine _sampleLoop;
    private int _errors;
    private RemoteBasesApplier _remoteBases;
    private long _lastVersion;
    private readonly List<LobbyPosSampleDto> _pendingSamples = new();
    private float _batchStartTime;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (backend == null) backend = G.Backend;
        if (snapshotSync == null) snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();
        _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
    }

    private void Start()
    {
        if (autoJoinOnStart)
            StartCoroutine(JoinLobbyFlow());
    }

    public IEnumerator JoinLobbyFlow()
    {
        if (backend == null)
        {
            var tries = 0;
            while (backend == null && tries < 5)
            {
                backend = G.Backend != null ? G.Backend : FindAnyObjectByType<ZooBackendClient>();
                if (backend != null) break;
                tries++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (backend == null)
        {
            DisableOnline("backend_missing");
            yield break;
        }

        if (snapshotSync == null)
            snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();

        yield return backend.EnsureGuest();

        var joinOk = false;
        yield return JoinLobby(ok => joinOk = ok);
        if (!joinOk)
        {
            DisableOnline("join_failed");
            yield break;
        }

        IsOnline = true;
        _errors = 0;

        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);

        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());
    }

    public IEnumerator JoinWithFriend(string friendCode, Action<bool> onDone = null)
    {
        if (backend == null)
        {
            var tries = 0;
            while (backend == null && tries < 5)
            {
                backend = G.Backend != null ? G.Backend : FindAnyObjectByType<ZooBackendClient>();
                if (backend != null) break;
                tries++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (backend == null)
        {
            DisableOnline("backend_missing");
            onDone?.Invoke(false);
            yield break;
        }

        if (snapshotSync == null)
            snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();

        yield return backend.EnsureGuest();

        var ok = false;
        yield return JoinLobbyWith(friendCode, v => ok = v);
        if (!ok)
        {
            RegisterError();
            onDone?.Invoke(false);
            yield break;
        }

        IsOnline = true;
        _errors = 0;
        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);
        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());

        onDone?.Invoke(true);
    }

    private IEnumerator UpdateLoop()
    {
        while (IsOnline)
        {
            yield return UpdateLobby();
            yield return new WaitForSeconds(updateIntervalSec);
        }
    }

    private IEnumerator StateLoop()
    {
        while (IsOnline)
        {
            yield return FetchState();
            yield return new WaitForSeconds(stateIntervalSec);
        }
    }

    private void DisableOnline(string reason)
    {
        IsOnline = false;
        LobbyId = null;
        _errors = 0;
        _lastVersion = 0;
        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(true);
        if (_updateLoop != null) StopCoroutine(_updateLoop);
        if (_stateLoop != null) StopCoroutine(_stateLoop);
        if (_sampleLoop != null) StopCoroutine(_sampleLoop);
        _updateLoop = null;
        _stateLoop = null;
        _sampleLoop = null;
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.ApplyLobbyMembers(new List<LobbyMemberStateDto>());
        Debug.LogWarning($"[Lobby] Offline: {reason}");
    }

    private void OnApplicationQuit()
    {
        StartCoroutine(FlushSnapshotOnce());
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            StartCoroutine(FlushSnapshotOnce());
    }

    private IEnumerator FlushSnapshotOnce()
    {
        if (backend == null || snapshotSync == null) yield break;
        var json = snapshotSync.BuildSnapshotJson();
        if (string.IsNullOrEmpty(json)) yield break;
        yield return backend.SaveZoo(json);
    }

    private IEnumerator SampleLoop()
    {
        _batchStartTime = Time.realtimeSinceStartup;
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, positionSampleRate));
        while (IsOnline)
        {
            var tr = G.Player != null ? G.Player.transform : null;
            if (tr != null)
            {
                var dt = (Time.realtimeSinceStartup - _batchStartTime) * 1000f;
                _pendingSamples.Add(new LobbyPosSampleDto
                {
                    dt = dt,
                    x = tr.position.x,
                    y = tr.position.y,
                    z = tr.position.z
                });
            }
            yield return wait;
        }
    }

    private string BaseUrl => backend != null ? backend.baseUrl : "";

    private void SetPlayerHeader(UnityWebRequest req)
    {
        var pid = G.Save != null ? G.Save.LoadBackendProfile().playerId : null;
        if (!string.IsNullOrEmpty(pid))
            req.SetRequestHeader("X-Player-Id", pid);
    }

    private IEnumerator JoinLobby(Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/join";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(false);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            ParseMembers(obj["members"] as JArray);
            onDone?.Invoke(true);
        }
        catch
        {
            onDone?.Invoke(false);
        }
    }

    private IEnumerator JoinLobbyWith(string friendCode, Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/join-with";
        var payload = new JObject
        {
            ["friendCode"] = (friendCode ?? "").Trim().ToUpperInvariant()
        };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(false);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            ParseMembers(obj["members"] as JArray);
            onDone?.Invoke(true);
        }
        catch
        {
            onDone?.Invoke(false);
        }
    }

    public IEnumerator SendGift(string toPlayerId, string itemType, string itemId, Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/gifts/send";
        var payload = new JObject
        {
            ["toPlayerId"] = toPlayerId,
            ["itemType"] = itemType,
            ["itemId"] = itemId
        };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    public IEnumerator GetPendingGifts(Action<List<GiftItemDto>> onOk, Action<long, string> onErr = null)
    {
        var url = $"{BaseUrl}/gifts/pending";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        try
        {
            var arr = JArray.Parse(req.downloadHandler.text);
            var list = new List<GiftItemDto>();
            foreach (var token in arr)
                list.Add(token.ToObject<GiftItemDto>() ?? new GiftItemDto());
            onOk?.Invoke(list);
        }
        catch
        {
            onErr?.Invoke(500, "Failed to parse gifts");
        }
    }

    public IEnumerator AcceptGift(string giftId, Action<GiftAcceptResponseDto> onOk = null, Action<long, string> onErr = null)
    {
        var url = $"{BaseUrl}/gifts/accept";
        var payload = new JObject { ["giftId"] = giftId };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            var resp = obj.ToObject<GiftAcceptResponseDto>();
            onOk?.Invoke(resp);
        }
        catch
        {
            onErr?.Invoke(500, "Failed to parse gift accept");
        }
    }

    public IEnumerator DeclineGift(string giftId, Action<bool> onOk = null)
    {
        var url = $"{BaseUrl}/gifts/decline";
        var payload = new JObject { ["giftId"] = giftId };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        onOk?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    private IEnumerator UpdateLobby()
    {
        var url = $"{BaseUrl}/lobby/update";

        var payload = new JObject();

        var snapshotJson = snapshotSync != null ? snapshotSync.ConsumeDirtySnapshot() : null;
        if (!string.IsNullOrEmpty(snapshotJson))
        {
            try { payload["baseData"] = JToken.Parse(snapshotJson); }
            catch { }
        }

        var hand = BuildHand();
        if (hand != null)
            payload["hand"] = JToken.FromObject(hand);

        List<LobbyPosSampleDto> samplesToSend = null;
        if (_pendingSamples.Count > 0)
        {
            samplesToSend = new List<LobbyPosSampleDto>(_pendingSamples);
            _pendingSamples.Clear();
            _batchStartTime = Time.realtimeSinceStartup;
        }

        if (samplesToSend != null)
            payload["positions"] = JToken.FromObject(samplesToSend);

        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            RegisterError();
            yield break;
        }

        _errors = 0;
    }

    private IEnumerator FetchState()
    {
        var url = $"{BaseUrl}/lobby/state?since={_lastVersion}";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.responseCode == 204)
            yield break;

        if (req.result != UnityWebRequest.Result.Success)
        {
            RegisterError();
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            ParseMembers(obj["members"] as JArray);
        }
        catch
        {
            RegisterError();
        }
    }

    private void ParseMembers(JArray arr)
    {
        _lastMembers.Clear();
        if (arr != null)
        {
            foreach (var token in arr)
            {
                var item = token.ToObject<LobbyMemberStateDto>() ?? new LobbyMemberStateDto();
                var baseToken = token["baseData"];
                if (baseToken != null && baseToken.Type != JTokenType.Null)
                {
                    item.baseDataRaw = baseToken.ToString(Formatting.None);
                    try { item.baseData = JsonConvert.DeserializeObject<BaseSnapshotDto>(item.baseDataRaw); }
                    catch { item.baseData = null; }
                }

                _lastMembers.Add(item);
            }
        }

        LobbyStateUpdated?.Invoke(_lastMembers);

        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.ApplyLobbyMembers(_lastMembers);
    }

    private void RegisterError()
    {
        _errors++;
        if (_errors >= maxConsecutiveErrors)
            DisableOnline("server_unreachable");
    }

    private LobbyHandItemDto BuildHand()
    {
        if (G.QuickAccess == null) return null;
        var active = G.QuickAccess.CurrentActive;
        if (active == null) return null;

        var type = MapHandType(active.Type);
        if (string.IsNullOrEmpty(type)) return null;

        return new LobbyHandItemDto
        {
            type = type,
            id = active.Name
        };
    }

    private string MapHandType(Item item)
    {
        return item switch
        {
            Item.Egg => "egg",
            Item.Brainrot => "brainrot",
            Item.Food => "food",
            Item.Hamer => "hammer",
            _ => null
        };
    }
}
