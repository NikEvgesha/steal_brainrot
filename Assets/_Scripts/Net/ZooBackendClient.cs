using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

[Serializable]
public class ZooLocationItem
{
    public string playerId;
    public string friendCode;
    public string displayName;
    public string lastSeenAt;
    public bool isOnline;
    public bool isFriend;
    public BaseSnapshotDto baseData;
    public string baseDataRaw;
}

[Serializable]
public class FriendBaseResponse
{
    public string friendCode;
    public string displayName;
    public string lastSeenAt;
    public bool isOnline;
    public BaseSnapshotDto data;
    public string dataRaw;
}

[Serializable]
public class ChestItemData
{
    public string id;
    public BrainrotDinamicData dinamic;
}

[Serializable]
public class ChestSlotDto
{
    public int slotIndex;
    public string itemType;
    public ChestItemData itemData;
    public string itemDataRaw;
}

[Serializable]
public class ChestStateResponse
{
    public string updatedAt;
    public List<ChestSlotDto> slots = new();
}

[Serializable]
public class ChestClaimResponse
{
    public bool ok;
    public int slotIndex;
    public string itemType;
    public ChestItemData itemData;
    public string itemDataRaw;
}

public class ZooBackendClient : MonoBehaviour
{
    private enum LocationsPollingMode
    {
        Default,
        Suspended,
        CapacitySnapshot
    }

    [Header("Server")]
    public string baseUrl = "https://api.igrodelnya-zoogame.ru";

    [Header("Deps")]
    private SaveManager saveManager; // можно не назначать — возьмём через G.Save
    [SerializeField] private FriendsApi friendsApi;   // опционально

    public FriendsApi FriendsApi { get { return friendsApi; } }

    [Header("Locations")]
    [SerializeField] private bool autoFetchLocations = true;
    [SerializeField] private float locationsRefreshSec = 30f;
    [SerializeField] private int locationsLimit = 5;
    [SerializeField] private int locationsOnlineSec = 60;

    public event Action<List<ZooLocationItem>> LocationsUpdated;
    public event Action<List<ZooLocationItem>> InitialLocationsLoaded;
    public event Action<FriendBaseResponse> FriendBaseLoaded;

    private readonly List<ZooLocationItem> _lastLocations = new();
    public IReadOnlyList<ZooLocationItem> LastLocations => _lastLocations;
    public FriendBaseResponse LastFriendBase { get; private set; }
    public bool IsInitialLocationsLoaded { get; private set; }

    private Coroutine _locationsLoop;
    private LocationsPollingMode _locationsPollingMode;
    private bool _capacitySnapshotPending;

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

    private void Start()
    {
        if (autoFetchLocations)
        {
            _locationsLoop = StartCoroutine(LocationsLoop());
        }
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
            RecordRequestFailure("/auth/guest", req);
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
            RecordRequestFailure("/zoo/me:get", req);
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
            RecordRequestFailure("/zoo/me:put", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            onOk?.Invoke(false);
            yield break;
        }

        onOk?.Invoke(true);
    }

    // ===== LOCATIONS =====
    private IEnumerator LocationsLoop()
    {
        while (true)
        {
            if (_locationsPollingMode == LocationsPollingMode.Suspended)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            if (_locationsPollingMode == LocationsPollingMode.CapacitySnapshot)
            {
                if (_capacitySnapshotPending)
                {
                    _capacitySnapshotPending = false;
                    yield return GetLocations(locationsLimit, locationsOnlineSec);
                }

                yield return new WaitForSeconds(1f);
                continue;
            }

            yield return GetLocations(locationsLimit, locationsOnlineSec);
            yield return new WaitForSeconds(locationsRefreshSec);
        }
    }

    public void SetRealtimeLobbyMode()
    {
        _locationsPollingMode = LocationsPollingMode.Suspended;
        _capacitySnapshotPending = false;
    }

    public void SetOfflineLocalOnlyMode()
    {
        _locationsPollingMode = LocationsPollingMode.Suspended;
        _capacitySnapshotPending = false;
    }

    public void RequestCapacitySnapshotOnce()
    {
        _locationsPollingMode = LocationsPollingMode.CapacitySnapshot;
        _capacitySnapshotPending = true;
    }

    public IEnumerator GetLocations(int limit, int onlineSec, Action<List<ZooLocationItem>> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/zoo/locations?limit={limit}&onlineSec={onlineSec}";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            RecordRequestFailure("/zoo/locations", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            CompleteInitialLocationsLoad(_lastLocations);
            yield break;
        }

        var list = new List<ZooLocationItem>();
        try
        {
            var arr = JArray.Parse(req.downloadHandler.text);
            foreach (var token in arr)
            {
                var item = token.ToObject<ZooLocationItem>() ?? new ZooLocationItem();
                var baseToken = token["baseData"];
                if (baseToken != null && baseToken.Type != JTokenType.Null)
                {
                    item.baseDataRaw = baseToken.ToString(Formatting.None);
                    try
                    {
                        item.baseData = JsonConvert.DeserializeObject<BaseSnapshotDto>(item.baseDataRaw);
                    }
                    catch
                    {
                        item.baseData = null;
                    }
                }

                list.Add(item);
            }
        }
        catch
        {
            AnalyticsManager.Instance.RecordBackendFailure("/zoo/locations", 500, "parse_error");
            onErr?.Invoke(500, "Failed to parse locations response");
            CompleteInitialLocationsLoad(_lastLocations);
            yield break;
        }

        _lastLocations.Clear();
        _lastLocations.AddRange(list);
        LocationsUpdated?.Invoke(_lastLocations);
        CompleteInitialLocationsLoad(_lastLocations);
        onOk?.Invoke(_lastLocations);
    }

    private void CompleteInitialLocationsLoad(List<ZooLocationItem> locations)
    {
        if (IsInitialLocationsLoaded)
            return;

        IsInitialLocationsLoaded = true;
        InitialLocationsLoaded?.Invoke(locations);
    }

    // ===== FRIEND BASE =====
    public IEnumerator GetFriendBase(string friendCode, Action<FriendBaseResponse> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var code = (friendCode ?? "").Trim().ToUpperInvariant();
        var url = $"{baseUrl}/friends/by-code/{code}/base";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            RecordRequestFailure("/friends/by-code/base", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            var resp = obj.ToObject<FriendBaseResponse>() ?? new FriendBaseResponse();
            var dataToken = obj["data"];
            if (dataToken != null && dataToken.Type != JTokenType.Null)
            {
                resp.dataRaw = dataToken.ToString(Formatting.None);
                try
                {
                    resp.data = JsonConvert.DeserializeObject<BaseSnapshotDto>(resp.dataRaw);
                }
                catch
                {
                    resp.data = null;
                }
            }

            LastFriendBase = resp;
            FriendBaseLoaded?.Invoke(resp);
            onOk?.Invoke(resp);
        }
        catch
        {
            AnalyticsManager.Instance.RecordBackendFailure("/friends/by-code/base", 500, "parse_error");
            onErr?.Invoke(500, "Failed to parse friend base response");
        }
    }

    // ===== CHEST =====
    public IEnumerator GetChest(Action<ChestStateResponse> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/chest";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            RecordRequestFailure("/chest:get", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            var resp = obj.ToObject<ChestStateResponse>() ?? new ChestStateResponse();
            resp.updatedAt = obj["updatedAt"]?.ToString();
            resp.slots = new List<ChestSlotDto>();

            var slots = obj["slots"] as JArray;
            if (slots != null)
            {
                foreach (var token in slots)
                {
                    var slot = token.ToObject<ChestSlotDto>() ?? new ChestSlotDto();
                    slot.slotIndex = token["slotIndex"]?.Value<int>() ?? slot.slotIndex;
                    slot.itemType = token["itemType"]?.ToString();
                    var dataToken = token["itemData"];
                    if (dataToken != null && dataToken.Type != JTokenType.Null)
                    {
                        slot.itemDataRaw = dataToken.ToString(Formatting.None);
                        try
                        {
                            slot.itemData = JsonConvert.DeserializeObject<ChestItemData>(slot.itemDataRaw);
                        }
                        catch
                        {
                            slot.itemData = null;
                        }
                    }
                    resp.slots.Add(slot);
                }
            }

            onOk?.Invoke(resp);
        }
        catch
        {
            AnalyticsManager.Instance.RecordBackendFailure("/chest:get", 500, "parse_error");
            onErr?.Invoke(500, "Failed to parse chest response");
        }
    }

    public IEnumerator DepositToChest(string targetFriendCode, string itemType, ChestItemData itemData, Action<bool> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/chest/deposit";
        var payload = new JObject
        {
            ["targetFriendCode"] = (targetFriendCode ?? "").Trim().ToUpperInvariant(),
            ["itemType"] = itemType,
            ["itemData"] = itemData != null ? JToken.FromObject(itemData) : null
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
            RecordRequestFailure("/chest/deposit", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            onOk?.Invoke(false);
            yield break;
        }

        onOk?.Invoke(true);
    }

    public IEnumerator ClaimChest(int slotIndex, Action<ChestClaimResponse> onOk = null, Action<long, string> onErr = null)
    {
        yield return EnsureGuest();

        var url = $"{baseUrl}/chest/claim";
        var payload = new JObject
        {
            ["slotIndex"] = slotIndex
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
            RecordRequestFailure("/chest/claim", req);
            onErr?.Invoke(req.responseCode, req.downloadHandler.text);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            var resp = obj.ToObject<ChestClaimResponse>() ?? new ChestClaimResponse();
            resp.itemType = obj["itemType"]?.ToString();
            var dataToken = obj["itemData"];
            if (dataToken != null && dataToken.Type != JTokenType.Null)
            {
                resp.itemDataRaw = dataToken.ToString(Formatting.None);
                try
                {
                    resp.itemData = JsonConvert.DeserializeObject<ChestItemData>(resp.itemDataRaw);
                }
                catch
                {
                    resp.itemData = null;
                }
            }
            onOk?.Invoke(resp);
        }
        catch
        {
            AnalyticsManager.Instance.RecordBackendFailure("/chest/claim", 500, "parse_error");
            onErr?.Invoke(500, "Failed to parse chest claim response");
        }
    }

    private static void RecordRequestFailure(string endpoint, UnityWebRequest request)
    {
        AnalyticsManager.Instance.RecordBackendFailure(
            endpoint,
            request != null ? request.responseCode : 0,
            request != null ? request.result.ToString().ToLowerInvariant() : "request_missing");
    }
}
