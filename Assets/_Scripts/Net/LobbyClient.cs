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
    public int slotIndex;
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

public class LobbyJoinResponseDto
{
    public string lobbyId;
    public long version;
    public List<LobbyMemberStateDto> members = new();
    public List<int> availableSlots = new();
}

public class LobbyClient : MonoBehaviour
{
    public static LobbyClient Instance { get; private set; }

    [Header("Behavior")]
    [SerializeField] private bool autoJoinOnStart = true;
    [SerializeField] private float updateIntervalSec = 1f;
    [SerializeField] private float stateIntervalSec = 2f;
    [SerializeField] private int maxConsecutiveErrors = 3;
    [SerializeField] private float reconnectIntervalSec = 60f;
    [SerializeField] private float positionSampleRate = 30f;
    [SerializeField] private float positionMinDistance = 0.05f;
    [SerializeField] private int maxSamplesPerUpdate = 20;
    [Header("Lobby Slots")]
    [SerializeField] private bool debugSlots = false;
    [SerializeField] private int claimRetryCount = 10;
    [SerializeField] private float claimRetryDelaySec = 0.5f;

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
    private Coroutine _reconnectLoop;
    private int _errors;
    private RemoteBasesApplier _remoteBases;
    private long _lastVersion;
    private readonly List<LobbyPosSampleDto> _pendingSamples = new();
    private float _batchStartTime;
    private Vector3 _lastSamplePos;
    private bool _hasSamplePos;

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
        if (IsOnline) yield break;
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

        _pendingSamples.Clear();
        _hasSamplePos = false;

        if (_reconnectLoop == null)
            _reconnectLoop = StartCoroutine(ReconnectLoop());
    }

    private IEnumerator ReconnectLoop()
    {
        while (!IsOnline)
        {
            yield return new WaitForSeconds(reconnectIntervalSec);
            if (IsOnline) yield break;
            if (backend == null)
                backend = G.Backend != null ? G.Backend : FindAnyObjectByType<ZooBackendClient>();

            if (backend == null) continue;
            if (snapshotSync == null)
                snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();

            yield return JoinLobbyFlow();
        }

        _reconnectLoop = null;
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
                var pos = tr.position;
                if (!_hasSamplePos)
                {
                    _hasSamplePos = true;
                    _lastSamplePos = pos;
                    _batchStartTime = Time.realtimeSinceStartup;
                    _pendingSamples.Add(new LobbyPosSampleDto
                    {
                        dt = 0f,
                        x = pos.x,
                        y = pos.y,
                        z = pos.z
                    });
                }
                else
                {
                    var delta = pos - _lastSamplePos;
                    if (delta.sqrMagnitude >= positionMinDistance * positionMinDistance)
                    {
                        if (_pendingSamples.Count == 0)
                            _batchStartTime = Time.realtimeSinceStartup;
                        var dt = (Time.realtimeSinceStartup - _batchStartTime) * 1000f;
                        _pendingSamples.Add(new LobbyPosSampleDto
                        {
                            dt = dt,
                            x = pos.x,
                            y = pos.y,
                            z = pos.z
                        });
                        _lastSamplePos = pos;
                    }
                }
            }
            yield return wait;
        }
    }

    private string BaseUrl => backend != null ? backend.baseUrl : "";

    private string GetLocalPlayerId()
    {
        if (G.Save == null) return null;
        return G.Save.LoadBackendProfile().playerId;
    }

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

        int slotPick = -1;
        bool needAutoClaim = false;
        List<int> availableSlotsCache = new List<int>();
        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            var membersArr = obj["members"] as JArray;
            ParseMembers(membersArr);

            var slotIndex = -1;
            var myId = GetLocalPlayerId();
            if (!string.IsNullOrEmpty(myId) && membersArr != null)
            {
                foreach (var token in membersArr)
                {
                    if (token?["playerId"]?.ToString() == myId)
                    {
                        slotIndex = token["slotIndex"]?.Value<int>() ?? -1;
                        break;
                    }
                }
            }

            if (slotIndex < 0)
            {
                availableSlotsCache = obj["availableSlots"]?.ToObject<List<int>>() ?? new List<int>();
                slotPick = PickRandomSlot(availableSlotsCache);
                if (slotPick < 0) needAutoClaim = true;
            }

            if (debugSlots) Debug.Log($"[Lobby] join slotIndex={slotIndex} available={availableSlotsCache.Count} auto={needAutoClaim}");
            onDone?.Invoke(true);
        }
        catch
        {
            onDone?.Invoke(false);
        }

        if (slotPick >= 0)
        {
            bool claimed = false;
            yield return TryClaimSlotWithRetry(availableSlotsCache, v => claimed = v);
            if (claimed)
                yield return FetchState();
        }
        else if (needAutoClaim)
        {
            yield return ClaimSlotAuto();
            yield return FetchState();
        }

        if (IsOnline && _reconnectLoop != null)
        {
            StopCoroutine(_reconnectLoop);
            _reconnectLoop = null;
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

        int slotPick = -1;
        bool needAutoClaim = false;
        List<int> availableSlotsCache = new List<int>();
        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            var membersArr = obj["members"] as JArray;
            ParseMembers(membersArr);

            var slotIndex = -1;
            var myId = GetLocalPlayerId();
            if (!string.IsNullOrEmpty(myId) && membersArr != null)
            {
                foreach (var token in membersArr)
                {
                    if (token?["playerId"]?.ToString() == myId)
                    {
                        slotIndex = token["slotIndex"]?.Value<int>() ?? -1;
                        break;
                    }
                }
            }

            if (slotIndex < 0)
            {
                availableSlotsCache = obj["availableSlots"]?.ToObject<List<int>>() ?? new List<int>();
                slotPick = PickRandomSlot(availableSlotsCache);
                if (slotPick < 0) needAutoClaim = true;
            }

            if (debugSlots) Debug.Log($"[Lobby] join slotIndex={slotIndex} available={availableSlotsCache.Count} auto={needAutoClaim}");
            onDone?.Invoke(true);
        }
        catch
        {
            onDone?.Invoke(false);
        }

        if (slotPick >= 0)
        {
            bool claimed = false;
            yield return TryClaimSlotWithRetry(availableSlotsCache, v => claimed = v);
            if (claimed)
                yield return FetchState();
        }
        else if (needAutoClaim)
        {
            yield return ClaimSlotAuto();
            yield return FetchState();
        }
    }


    private IEnumerator ClaimSlot(int slotIndex, Action<long> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/slot";
        var payload = new JObject { ["slotIndex"] = slotIndex };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.responseCode);
    }

    private int PickRandomSlot(List<int> slots)
    {
        if (slots == null || slots.Count == 0) return -1;
        var idx = UnityEngine.Random.Range(0, slots.Count);
        return slots[idx];
    }



    private IEnumerator ClaimSlotAuto()
    {
        var url = $"{BaseUrl}/lobby/slot/auto";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
    }

    private IEnumerator TryClaimSlotWithRetry(List<int> availableSlots, Action<bool> onDone = null)
    {
        var slots = availableSlots != null ? new List<int>(availableSlots) : new List<int>();
        for (int attempt = 0; attempt < claimRetryCount && slots.Count > 0; attempt++)
        {
            var pick = PickRandomSlot(slots);
            if (pick < 0) break;
            long code = 0;
            if (debugSlots) Debug.Log($"[Lobby] claim attempt slot={pick}");
            yield return ClaimSlot(pick, v => code = v);
            if (debugSlots) Debug.Log($"[Lobby] claim response {code}");
            if (code >= 200 && code < 300)
            {
                onDone?.Invoke(true);
                yield break;
            }

            // remove claimed slot and retry
            slots.Remove(pick);
            if (claimRetryDelaySec > 0f)
                yield return new WaitForSeconds(claimRetryDelaySec);
        }

        // fallback: let server assign slot
        yield return ClaimSlotAuto();
        onDone?.Invoke(true);
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
            if (maxSamplesPerUpdate > 0 && _pendingSamples.Count > maxSamplesPerUpdate)
            {
                var skip = _pendingSamples.Count - maxSamplesPerUpdate;
                samplesToSend = _pendingSamples.GetRange(skip, maxSamplesPerUpdate);
                var baseDt = samplesToSend[0].dt;
                if (baseDt > 0.001f)
                {
                    for (var i = 0; i < samplesToSend.Count; i++)
                        samplesToSend[i].dt -= baseDt;
                }
            }
            else
            {
                samplesToSend = new List<LobbyPosSampleDto>(_pendingSamples);
            }
            _pendingSamples.Clear();
            _batchStartTime = Time.realtimeSinceStartup;
        }

        if (samplesToSend != null)
            payload["positions"] = JToken.FromObject(samplesToSend);

        if (payload.Count == 0)
            yield break;

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
