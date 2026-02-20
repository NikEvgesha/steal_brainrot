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
    public int? element;
    public float? weight;
    public double? income;
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
    private struct PositionHistorySample
    {
        public float t;
        public Vector3 pos;
    }

    public static LobbyClient Instance { get; private set; }

    [Header("Behavior")]
    [SerializeField] private bool autoJoinOnStart = true;
    [SerializeField] private float updateIntervalSec = 1f;
    [SerializeField] private float stateIntervalSec = 1f;
    [SerializeField] private float soloStateIntervalSec = 2.5f;
    [SerializeField] private int maxConsecutiveErrors = 8;
    [SerializeField] private float errorWindowSec = 25f;
    [SerializeField] private float reconnectIntervalSec = 60f;
    [SerializeField] private float reconnectFirstDelaySec = 5f;
    [SerializeField] private float positionSampleRate = 30f;
    [SerializeField] private float positionMinDistance = 0.05f;
    [SerializeField] private float positionSendWindowSec = 1.5f;
    [SerializeField] private int maxSamplesPerUpdate = 60;
    [SerializeField] private int positionTargetSamplesPerUpdate = 24;
    [Header("Lobby Slots")]
    [SerializeField] private bool debugSlots = false;
    [SerializeField] private int claimRetryCount = 10;
    [SerializeField] private float claimRetryDelaySec = 0.5f;
    [Header("Debug Mirror Bot")]
    [SerializeField] private bool debugMirrorBotEnabled = false;
    [SerializeField] private string debugMirrorBotPlayerId = "11111111-1111-1111-1111-111111111111";
    [SerializeField] private bool debugMirrorAroundWorldZero = true;
    [Header("Debug Traffic")]
    [SerializeField] private bool debugTrafficLogs = false;
    [SerializeField] private bool debugTrafficVerbose = false;
    [SerializeField] private float debugTrafficSummaryIntervalSec = 1f;

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
    private Coroutine _debugMirrorLoop;
    private int _errors;
    private readonly Queue<float> _errorTimes = new();
    private RemoteBasesApplier _remoteBases;
    private long _lastVersion;
    private readonly List<PositionHistorySample> _positionHistory = new();
    private readonly Dictionary<string, string> _remoteBaseRawCache = new();
    private readonly Dictionary<string, BaseSnapshotDto> _remoteBaseSnapshotCache = new();
    private Vector3 _lastSamplePos;
    private bool _hasSamplePos;
    private string _cachedLocalPlayerId;
    private bool _hasLastSentHand;
    private string _lastSentHandType;
    private string _lastSentHandId;
    private int? _lastSentHandElement;
    private float? _lastSentHandWeight;
    private double? _lastSentHandIncome;
    private float _trafficSummaryAt;
    private int _trafficReqCount;
    private long _trafficTxBytes;
    private long _trafficRxBytes;
    private float _trafficHttpMs;
    private float _trafficParseMs;
    private float _trafficApplyMs;
    private bool _isAppPaused;
    private bool _hasAppFocus = true;
    private bool _forceSnapshotUpload;

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
        ResetErrors();
        _forceSnapshotUpload = true;
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.SetOfflineLocalOnly(false);

        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);

        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());
        EnsureDebugMirrorLoopState();
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
            RegisterError("JoinWithFriend failed");
            onDone?.Invoke(false);
            yield break;
        }

        IsOnline = true;
        ResetErrors();
        _forceSnapshotUpload = true;
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.SetOfflineLocalOnly(false);
        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);
        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());
        EnsureDebugMirrorLoopState();

        onDone?.Invoke(true);
    }

    private IEnumerator UpdateLoop()
    {
        while (IsOnline)
        {
            if (IsAppBackgrounded())
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            yield return UpdateLobby();
            yield return new WaitForSeconds(updateIntervalSec);
        }
    }

    private IEnumerator StateLoop()
    {
        while (IsOnline)
        {
            if (IsAppBackgrounded())
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            yield return FetchState();
            var pollDelay = stateIntervalSec;
            if (_lastMembers.Count <= 1)
                pollDelay = Mathf.Max(stateIntervalSec, soloStateIntervalSec);
            yield return new WaitForSeconds(pollDelay);
        }
    }

    private void DisableOnline(string reason)
    {
        IsOnline = false;
        LobbyId = null;
        ResetErrors();
        _lastVersion = 0;
        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(true);
        if (_updateLoop != null) StopCoroutine(_updateLoop);
        if (_stateLoop != null) StopCoroutine(_stateLoop);
        if (_sampleLoop != null) StopCoroutine(_sampleLoop);
        _updateLoop = null;
        _stateLoop = null;
        _sampleLoop = null;
        if (_debugMirrorLoop != null) StopCoroutine(_debugMirrorLoop);
        _debugMirrorLoop = null;
        Debug.LogWarning($"[Lobby] Offline: {reason}");

        _positionHistory.Clear();
        _hasSamplePos = false;
        _hasLastSentHand = false;
        _lastSentHandType = null;
        _lastSentHandId = null;
        _lastSentHandElement = null;
        _lastSentHandWeight = null;
        _lastSentHandIncome = null;
        _forceSnapshotUpload = false;
        _remoteBaseRawCache.Clear();
        _remoteBaseSnapshotCache.Clear();
        _lastMembers.Clear();
        FlushTrafficSummary(force: true);

        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.ApplyOfflineLocalOnly();

        if (_reconnectLoop == null)
            _reconnectLoop = StartCoroutine(ReconnectLoop());
    }

    private bool IsAppBackgrounded()
    {
        if (_isAppPaused)
            return true;

        if (Application.platform == RuntimePlatform.WebGLPlayer && !_hasAppFocus)
            return true;

        return false;
    }

    private void EnsureDebugMirrorLoopState()
    {
        if (!debugMirrorBotEnabled || !IsOnline)
        {
            if (_debugMirrorLoop != null)
            {
                StopCoroutine(_debugMirrorLoop);
                _debugMirrorLoop = null;
            }
            return;
        }

        if (_debugMirrorLoop == null)
            _debugMirrorLoop = StartCoroutine(DebugMirrorBotLoop());
    }

    private IEnumerator ReconnectLoop()
    {
        var firstAttempt = true;
        while (!IsOnline)
        {
            var delay = firstAttempt
                ? Mathf.Max(1f, Mathf.Min(reconnectFirstDelaySec, reconnectIntervalSec))
                : Mathf.Max(1f, reconnectIntervalSec);
            firstAttempt = false;

            yield return new WaitForSeconds(delay);
            if (IsOnline)
                break;
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
        if (IsOnline)
            StartCoroutine(LeaveLobby());
        StartCoroutine(FlushSnapshotOnce());
    }

    private void OnApplicationPause(bool pause)
    {
        _isAppPaused = pause;
        if (pause)
            StartCoroutine(FlushSnapshotOnce());
    }

    private void OnApplicationFocus(bool focus)
    {
        _hasAppFocus = focus;
    }

    private IEnumerator FlushSnapshotOnce()
    {
        if (backend == null || snapshotSync == null) yield break;
        var json = snapshotSync.BuildSnapshotJson();
        if (string.IsNullOrEmpty(json)) yield break;
        yield return backend.SaveZoo(json);
    }

    private IEnumerator LeaveLobby()
    {
        if (backend == null)
            backend = G.Backend != null ? G.Backend : FindAnyObjectByType<ZooBackendClient>();
        if (backend == null)
            yield break;

        var url = $"{BaseUrl}/lobby/leave";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success || req.responseCode != 404)
            yield break;

        using var fallbackReq = new UnityWebRequest(url, "DELETE");
        fallbackReq.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(fallbackReq);
        yield return fallbackReq.SendWebRequest();
    }

    private IEnumerator SampleLoop()
    {
        var wait = new WaitForSeconds(1f / Mathf.Max(1f, positionSampleRate));
        while (IsOnline)
        {
            if (IsAppBackgrounded())
            {
                yield return wait;
                continue;
            }

            var tr = G.Player != null ? G.Player.transform : null;
            if (tr != null)
            {
                var pos = tr.position;
                var now = Time.realtimeSinceStartup;
                if (!_hasSamplePos)
                {
                    _hasSamplePos = true;
                    _lastSamplePos = pos;
                    _positionHistory.Add(new PositionHistorySample
                    {
                        t = now,
                        pos = pos
                    });
                }
                else
                {
                    var delta = pos - _lastSamplePos;
                    if (delta.sqrMagnitude >= positionMinDistance * positionMinDistance)
                    {
                        _positionHistory.Add(new PositionHistorySample
                        {
                            t = now,
                            pos = pos
                        });
                        _lastSamplePos = pos;
                    }
                }
                TrimPositionHistory(now);
            }
            yield return wait;
        }
    }

    private void TrimPositionHistory(float now)
    {
        var keepSec = Mathf.Max(positionSendWindowSec + 1f, 2f);
        var minTime = now - keepSec;
        while (_positionHistory.Count > 0 && _positionHistory[0].t < minTime)
            _positionHistory.RemoveAt(0);
    }

    private List<LobbyPosSampleDto> BuildPositionSamplesForSend(float now, bool mirror)
    {
        if (_positionHistory.Count == 0) return null;

        TrimPositionHistory(now);
        if (_positionHistory.Count == 0) return null;

        var last = _positionHistory[_positionHistory.Count - 1];
        var staleSec = now - last.t;
        if (staleSec > positionSendWindowSec + 0.05f)
            return null;

        var windowStart = now - Mathf.Max(positionSendWindowSec, updateIntervalSec);
        var startIndex = 0;
        while (startIndex < _positionHistory.Count && _positionHistory[startIndex].t < windowStart)
            startIndex++;
        if (startIndex >= _positionHistory.Count)
            startIndex = _positionHistory.Count - 1;

        var count = _positionHistory.Count - startIndex;
        if (maxSamplesPerUpdate > 0 && count > maxSamplesPerUpdate)
        {
            startIndex = _positionHistory.Count - maxSamplesPerUpdate;
            count = maxSamplesPerUpdate;
        }

        if (count <= 0) return null;

        var endIndex = _positionHistory.Count - 1;
        var targetSamples = Mathf.Clamp(positionTargetSamplesPerUpdate, 4, 60);
        if (maxSamplesPerUpdate > 0)
            targetSamples = Mathf.Min(targetSamples, maxSamplesPerUpdate);
        targetSamples = Mathf.Min(targetSamples, count);

        var selected = new List<int>(targetSamples);
        if (targetSamples == count)
        {
            for (var i = startIndex; i <= endIndex; i++)
                selected.Add(i);
        }
        else
        {
            for (var i = 0; i < targetSamples; i++)
            {
                var t = targetSamples == 1 ? 0f : (float)i / (targetSamples - 1);
                var idx = startIndex + Mathf.RoundToInt((endIndex - startIndex) * t);
                if (selected.Count > 0 && idx <= selected[selected.Count - 1])
                    idx = selected[selected.Count - 1] + 1;
                if (idx > endIndex) idx = endIndex;
                selected.Add(idx);
            }
        }

        var baseTime = _positionHistory[selected[0]].t;
        var samples = new List<LobbyPosSampleDto>(selected.Count);
        for (var k = 0; k < selected.Count; k++)
        {
            var i = selected[k];
            var s = _positionHistory[i];
            var x = s.pos.x;
            var z = s.pos.z;
            if (mirror && debugMirrorAroundWorldZero)
            {
                x = -x;
                z = -z;
            }

            samples.Add(new LobbyPosSampleDto
            {
                dt = (s.t - baseTime) * 1000f,
                x = x,
                y = s.pos.y,
                z = z
            });
        }

        return samples;
    }

    private string BaseUrl => backend != null ? backend.baseUrl : "";

    private void TrackHttpTraffic(string tag, UnityWebRequest req, int txBytes, float startedAt, string details = null)
    {
        if (!debugTrafficLogs) return;

        var elapsedMs = (Time.realtimeSinceStartup - startedAt) * 1000f;
        var rxBytes = req.downloadHandler != null && req.downloadHandler.data != null
            ? req.downloadHandler.data.Length
            : 0;
        var code = (int)req.responseCode;

        _trafficReqCount++;
        _trafficTxBytes += Mathf.Max(0, txBytes);
        _trafficRxBytes += Mathf.Max(0, rxBytes);
        _trafficHttpMs += Mathf.Max(0f, elapsedMs);

        if (debugTrafficVerbose)
            Debug.Log($"[LobbyTraffic] {tag} code={code} ms={elapsedMs:0.0} tx={txBytes}B rx={rxBytes}B {details}");

        FlushTrafficSummary(force: false);
    }

    private void TrackParseTraffic(int members, int remoteWithBase, int baseBytes, float parseMs, float applyMs)
    {
        if (!debugTrafficLogs) return;

        _trafficParseMs += Mathf.Max(0f, parseMs);
        _trafficApplyMs += Mathf.Max(0f, applyMs);

        if (debugTrafficVerbose)
            Debug.Log($"[LobbyTraffic] parse members={members} remoteWithBase={remoteWithBase} base={baseBytes / 1024f:0.00}KB parseMs={parseMs:0.00} applyMs={applyMs:0.00}");

        FlushTrafficSummary(force: false);
    }

    private void FlushTrafficSummary(bool force)
    {
        if (!debugTrafficLogs)
            return;

        var now = Time.realtimeSinceStartup;
        var interval = Mathf.Max(0.2f, debugTrafficSummaryIntervalSec);
        if (_trafficSummaryAt <= 0f)
            _trafficSummaryAt = now + interval;

        if (!force && now < _trafficSummaryAt)
            return;

        if (_trafficReqCount > 0 || _trafficParseMs > 0f || _trafficApplyMs > 0f)
        {
            Debug.Log($"[LobbyTraffic] summary req={_trafficReqCount} tx={_trafficTxBytes / 1024f:0.00}KB rx={_trafficRxBytes / 1024f:0.00}KB httpMs={_trafficHttpMs:0.00} parseMs={_trafficParseMs:0.00} applyMs={_trafficApplyMs:0.00}");
        }

        _trafficReqCount = 0;
        _trafficTxBytes = 0;
        _trafficRxBytes = 0;
        _trafficHttpMs = 0f;
        _trafficParseMs = 0f;
        _trafficApplyMs = 0f;
        _trafficSummaryAt = now + interval;
    }

    private string GetLocalPlayerId()
    {
        try
        {
            if (G.Save == null)
                return _cachedLocalPlayerId;

            var id = G.Save.LoadBackendProfile().playerId;
            if (!string.IsNullOrEmpty(id))
                _cachedLocalPlayerId = id;
            else
                _cachedLocalPlayerId = null;
        }
        catch
        {
            // Keep the previous id during bootstrap races.
        }

        return _cachedLocalPlayerId;
    }

    private void SetPlayerHeader(UnityWebRequest req)
    {
        var pid = G.Save != null ? G.Save.LoadBackendProfile().playerId : null;
        if (!string.IsNullOrEmpty(pid))
            req.SetRequestHeader("X-Player-Id", pid);
    }

    private void SetPlayerHeader(UnityWebRequest req, string playerId)
    {
        var pid = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
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
                slotPick = PickPreferredOrRandomSlot(availableSlotsCache);
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
                slotPick = PickPreferredOrRandomSlot(availableSlotsCache);
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


    private IEnumerator ClaimSlot(int slotIndex, Action<long> onDone = null, string playerIdOverride = null)
    {
        var url = $"{BaseUrl}/lobby/slot";
        var payload = new JObject { ["slotIndex"] = slotIndex };
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        if (string.IsNullOrEmpty(playerIdOverride))
            SetPlayerHeader(req);
        else
            SetPlayerHeader(req, playerIdOverride);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.responseCode);
    }

    private int PickRandomSlot(List<int> slots)
    {
        if (slots == null || slots.Count == 0) return -1;
        var idx = UnityEngine.Random.Range(0, slots.Count);
        return slots[idx];
    }

    private int PickPreferredOrRandomSlot(List<int> slots)
    {
        return PickRandomSlot(slots);
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

    private bool TryGetDebugMirrorPlayerId(out string playerId)
    {
        playerId = null;
        if (string.IsNullOrWhiteSpace(debugMirrorBotPlayerId))
            return false;
        if (!Guid.TryParse(debugMirrorBotPlayerId, out var pid))
            return false;
        playerId = pid.ToString();
        return true;
    }

    private IEnumerator JoinLobbyAs(string playerId, Action<bool, List<int>, int> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/join";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req, playerId);

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(false, new List<int>(), -1);
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            var membersArr = obj["members"] as JArray;
            var slotIndex = -1;
            if (membersArr != null)
            {
                foreach (var token in membersArr)
                {
                    if (token?["playerId"]?.ToString() == playerId)
                    {
                        slotIndex = token["slotIndex"]?.Value<int>() ?? -1;
                        break;
                    }
                }
            }

            var available = obj["availableSlots"]?.ToObject<List<int>>() ?? new List<int>();
            onDone?.Invoke(true, available, slotIndex);
        }
        catch
        {
            onDone?.Invoke(false, new List<int>(), -1);
        }
    }

    private IEnumerator ClaimSlotAutoAs(string playerId, Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/slot/auto";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req, playerId);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    private IEnumerator UpdateLobbyAs(string playerId, JObject payload, Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/update";
        var json = payload.ToString(Formatting.None);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req, playerId);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.result == UnityWebRequest.Result.Success);
    }

    private IEnumerator DebugMirrorBotLoop()
    {
        if (!TryGetDebugMirrorPlayerId(out var mirrorPlayerId))
            yield break;

        var mirrorJoined = false;
        while (IsOnline && debugMirrorBotEnabled)
        {
            if (!mirrorJoined)
            {
                var joined = false;
                var slotIndex = -1;
                var available = new List<int>();
                yield return JoinLobbyAs(mirrorPlayerId, (ok, slots, slot) =>
                {
                    joined = ok;
                    available = slots ?? new List<int>();
                    slotIndex = slot;
                });

                if (!joined)
                {
                    yield return new WaitForSeconds(updateIntervalSec);
                    continue;
                }

                if (slotIndex < 0)
                {
                    if (available.Count > 0)
                    {
                        var pick = PickRandomSlot(available);
                        if (pick >= 0)
                            yield return ClaimSlot(pick, _ => { }, mirrorPlayerId);
                    }
                    else
                    {
                        yield return ClaimSlotAutoAs(mirrorPlayerId, _ => { });
                    }
                }

                mirrorJoined = true;
            }

            var payload = new JObject();
            var snapshotJson = snapshotSync != null ? snapshotSync.BuildSnapshotJson() : null;
            if (!string.IsNullOrEmpty(snapshotJson))
            {
                try { payload["baseData"] = JToken.Parse(snapshotJson); } catch { }
            }

            var hand = BuildHand();
            if (hand != null)
                payload["hand"] = JToken.FromObject(hand);

            var mirrorSamples = BuildPositionSamplesForSend(Time.realtimeSinceStartup, mirror: true);
            if (mirrorSamples != null)
                payload["positions"] = JToken.FromObject(mirrorSamples);

            if (payload.Count > 0)
            {
                var updateOk = false;
                yield return UpdateLobbyAs(mirrorPlayerId, payload, ok => updateOk = ok);
                if (!updateOk)
                    mirrorJoined = false;
            }

            yield return new WaitForSeconds(updateIntervalSec);
        }

        _debugMirrorLoop = null;
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

        var forceSnapshot = _forceSnapshotUpload;
        var snapshotJson = snapshotSync == null
            ? null
            : (forceSnapshot ? snapshotSync.BuildSnapshotJson() : snapshotSync.ConsumeDirtySnapshot());
        var sentBaseData = false;
        if (!string.IsNullOrEmpty(snapshotJson))
        {
            try { payload["baseData"] = JToken.Parse(snapshotJson); }
            catch { payload.Remove("baseData"); }
            sentBaseData = payload["baseData"] != null;
        }

        var hand = BuildHand();
        if (HasHandChanged(hand))
            payload["hand"] = hand != null ? JToken.FromObject(hand) : JValue.CreateNull();

        // No reason to stream positions while alone in lobby.
        var samplesToSend = _lastMembers.Count > 1
            ? BuildPositionSamplesForSend(Time.realtimeSinceStartup, mirror: false)
            : null;

        if (samplesToSend != null)
            payload["positions"] = JToken.FromObject(samplesToSend);

        if (payload.Count == 0)
        {
            if (debugTrafficLogs && debugTrafficVerbose)
                Debug.Log("[LobbyTraffic] POST /lobby/update skipped (empty payload)");
            yield break;
        }

        var json = payload.ToString(Formatting.None);
        var txBytes = System.Text.Encoding.UTF8.GetByteCount(json);
        var startedAt = Time.realtimeSinceStartup;

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        TrackHttpTraffic(
            "POST /lobby/update",
            req,
            txBytes,
            startedAt,
            $"fields={payload.Count} samples={(samplesToSend != null ? samplesToSend.Count : 0)}");

        if (req.result != UnityWebRequest.Result.Success)
        {
            RegisterError(req, "POST /lobby/update");
            yield break;
        }

        ResetErrors();
        if (forceSnapshot && sentBaseData)
            _forceSnapshotUpload = false;
    }

    private IEnumerator FetchState()
    {
        var sinceVersion = _lastVersion;
        var url = $"{BaseUrl}/lobby/state?since={sinceVersion}";
        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        SetPlayerHeader(req);

        var startedAt = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        TrackHttpTraffic("GET /lobby/state", req, 0, startedAt, $"since={sinceVersion}");

        if (req.responseCode == 204)
            yield break;

        if (req.result != UnityWebRequest.Result.Success)
        {
            RegisterError(req, "GET /lobby/state");
            yield break;
        }

        try
        {
            var obj = JObject.Parse(req.downloadHandler.text);
            LobbyId = obj["lobbyId"]?.ToString();
            _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
            ParseMembers(obj["members"] as JArray);
        }
        catch (Exception ex)
        {
            RegisterError($"GET /lobby/state parse: {ex.GetType().Name}");
        }
    }

    private void ParseMembers(JArray arr)
    {
        var collectTraffic = debugTrafficLogs;
        var parseStartedAt = collectTraffic ? Time.realtimeSinceStartup : 0f;
        var remoteWithBase = 0;
        var remoteBaseBytes = 0;

        _lastMembers.Clear();
        var localId = GetLocalPlayerId();
        var seenRemoteIds = new HashSet<string>();
        if (arr != null)
        {
            foreach (var token in arr)
            {
                var obj = token as JObject;
                if (obj == null)
                    continue;

                var item = ParseMemberFast(obj);
                var isLocalMember = !string.IsNullOrEmpty(localId) && item.playerId == localId;
                var baseToken = obj["baseData"];
                if (!isLocalMember && baseToken != null && baseToken.Type != JTokenType.Null)
                {
                    item.baseDataRaw = baseToken.ToString(Formatting.None);
                    if (collectTraffic && !string.IsNullOrEmpty(item.baseDataRaw))
                    {
                        remoteWithBase++;
                        remoteBaseBytes += System.Text.Encoding.UTF8.GetByteCount(item.baseDataRaw);
                    }

                    if (!string.IsNullOrEmpty(item.playerId) &&
                        _remoteBaseRawCache.TryGetValue(item.playerId, out var cachedRaw) &&
                        cachedRaw == item.baseDataRaw &&
                        _remoteBaseSnapshotCache.TryGetValue(item.playerId, out var cachedSnapshot))
                    {
                        item.baseData = cachedSnapshot;
                    }
                    else
                    {
                        try { item.baseData = JsonConvert.DeserializeObject<BaseSnapshotDto>(item.baseDataRaw); }
                        catch { item.baseData = null; }

                        if (!string.IsNullOrEmpty(item.playerId))
                        {
                            _remoteBaseRawCache[item.playerId] = item.baseDataRaw;
                            _remoteBaseSnapshotCache[item.playerId] = item.baseData;
                        }
                    }
                }
                else if (!isLocalMember && !string.IsNullOrEmpty(item.playerId))
                {
                    // Some state updates may omit baseData; keep last known snapshot
                    // so remote bases do not appear empty until the next full update.
                    if (_remoteBaseRawCache.TryGetValue(item.playerId, out var cachedRaw))
                        item.baseDataRaw = cachedRaw;
                    if (_remoteBaseSnapshotCache.TryGetValue(item.playerId, out var cachedSnapshot))
                        item.baseData = cachedSnapshot;
                }

                if (!isLocalMember && !string.IsNullOrEmpty(item.playerId))
                    seenRemoteIds.Add(item.playerId);

                _lastMembers.Add(item);
            }
        }

        if (_remoteBaseRawCache.Count > 0)
        {
            var remove = new List<string>();
            foreach (var kv in _remoteBaseRawCache)
            {
                if (!seenRemoteIds.Contains(kv.Key))
                    remove.Add(kv.Key);
            }

            for (int i = 0; i < remove.Count; i++)
            {
                _remoteBaseRawCache.Remove(remove[i]);
                _remoteBaseSnapshotCache.Remove(remove[i]);
            }
        }

        LobbyStateUpdated?.Invoke(_lastMembers);
        var parseMs = collectTraffic ? (Time.realtimeSinceStartup - parseStartedAt) * 1000f : 0f;

        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        var applyStartedAt = collectTraffic ? Time.realtimeSinceStartup : 0f;
        if (_remoteBases != null)
            _remoteBases.ApplyLobbyMembers(_lastMembers);
        var applyMs = collectTraffic ? (Time.realtimeSinceStartup - applyStartedAt) * 1000f : 0f;

        if (collectTraffic)
            TrackParseTraffic(_lastMembers.Count, remoteWithBase, remoteBaseBytes, parseMs, applyMs);
    }

    private LobbyMemberStateDto ParseMemberFast(JObject obj)
    {
        var item = new LobbyMemberStateDto
        {
            playerId = obj["playerId"]?.ToString(),
            friendCode = obj["friendCode"]?.ToString(),
            displayName = obj["displayName"]?.ToString(),
            isFriend = obj["isFriend"]?.Value<bool>() ?? false,
            isOnline = obj["isOnline"]?.Value<bool>() ?? false,
            updatedAt = obj["updatedAt"]?.ToString(),
            slotIndex = obj["slotIndex"]?.Value<int>() ?? -1
        };

        if (obj["hand"] is JObject handObj)
        {
            item.hand = new LobbyHandItemDto
            {
                type = handObj["type"]?.ToString(),
                id = handObj["id"]?.ToString(),
                element = handObj["element"]?.Type == JTokenType.Null ? null : handObj["element"]?.Value<int?>(),
                weight = handObj["weight"]?.Type == JTokenType.Null ? null : handObj["weight"]?.Value<float?>(),
                income = handObj["income"]?.Type == JTokenType.Null ? null : handObj["income"]?.Value<double?>()
            };
        }

        if (obj["positions"] is JArray posArr && posArr.Count > 0)
        {
            var positions = new List<LobbyPosSampleDto>(posArr.Count);
            foreach (var p in posArr)
            {
                var posObj = p as JObject;
                if (posObj == null) continue;
                positions.Add(new LobbyPosSampleDto
                {
                    dt = posObj["dt"]?.Value<float>() ?? 0f,
                    x = posObj["x"]?.Value<float>() ?? 0f,
                    y = posObj["y"]?.Value<float>() ?? 0f,
                    z = posObj["z"]?.Value<float>() ?? 0f
                });
            }
            item.positions = positions;
        }

        return item;
    }

    private void ResetErrors()
    {
        _errors = 0;
        _errorTimes.Clear();
    }

    private void RegisterError(UnityWebRequest req, string context)
    {
        var code = req != null ? req.responseCode : 0;
        var reqError = req != null ? req.error : "unknown";
        RegisterError($"{context}: code={code} err={reqError}");
    }

    private void RegisterError(string details = null)
    {
        if (IsAppBackgrounded())
            return;

        var now = Time.realtimeSinceStartup;
        _errorTimes.Enqueue(now);

        var keepSince = now - Mathf.Max(1f, errorWindowSec);
        while (_errorTimes.Count > 0 && _errorTimes.Peek() < keepSince)
            _errorTimes.Dequeue();

        _errors = _errorTimes.Count;
        if (!string.IsNullOrEmpty(details))
            Debug.LogWarning($"[Lobby] Request failed ({_errors}/{maxConsecutiveErrors} in {errorWindowSec:0.#}s): {details}");

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

        var dto = new LobbyHandItemDto
        {
            type = type,
            id = active.Name
        };

        if (active.Type == Item.Brainrot)
        {
            var brainrot = active as Brainrot;
            if (brainrot == null)
                brainrot = active.GetComponent<Brainrot>();
            if (brainrot != null)
            {
                dto.element = (int)brainrot.DinamicData.ElementType;
                dto.weight = brainrot.DinamicData.WeightMultiplier;
                dto.income = brainrot.DinamicData.ResultIncome;
            }
        }
        else if (active.Type == Item.Egg)
        {
            var egg = active as Egg;
            if (egg == null)
                egg = active.GetComponent<Egg>();
            if (egg != null)
            {
                dto.element = (int)egg.Data.DinamicData.ElementType;
                dto.weight = egg.Data.DinamicData.WeightMultiplier;
                dto.income = egg.Data.DinamicData.ResultIncome;
            }
        }

        return dto;
    }

    private bool HasHandChanged(LobbyHandItemDto hand)
    {
        var type = hand != null ? hand.type : null;
        var id = hand != null ? hand.id : null;
        var element = hand != null ? hand.element : null;
        var weight = hand != null ? hand.weight : null;
        var income = hand != null ? hand.income : null;
        if (!_hasLastSentHand)
        {
            _hasLastSentHand = true;
            _lastSentHandType = type;
            _lastSentHandId = id;
            _lastSentHandElement = element;
            _lastSentHandWeight = weight;
            _lastSentHandIncome = income;
            return hand != null;
        }

        if (_lastSentHandType == type &&
            _lastSentHandId == id &&
            _lastSentHandElement == element &&
            NullableFloatEquals(_lastSentHandWeight, weight) &&
            NullableDoubleEquals(_lastSentHandIncome, income))
            return false;

        _lastSentHandType = type;
        _lastSentHandId = id;
        _lastSentHandElement = element;
        _lastSentHandWeight = weight;
        _lastSentHandIncome = income;
        return true;
    }

    private static bool NullableFloatEquals(float? a, float? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return Mathf.Abs(a.Value - b.Value) <= 0.0001f;
    }

    private static bool NullableDoubleEquals(double? a, double? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return Math.Abs(a.Value - b.Value) <= 0.0001d;
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
