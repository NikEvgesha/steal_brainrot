using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif
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
    public bool isReturned;
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

public enum LobbyNetworkMode
{
    OfflineLocal,
    CapacitySnapshot,
    Online
}

public class LobbyClient : MonoBehaviour
{
    private enum LobbyJoinResult
    {
        Failed,
        CapacityDegraded,
        Success
    }

    private struct PositionHistorySample
    {
        public float t;
        public Vector3 pos;
    }

    public static LobbyClient Instance { get; private set; }

    [Header("Behavior")]
    [SerializeField] private bool autoJoinOnStart = true;
    [SerializeField, Min(5)] private int initialRequestTimeoutSec = 15;
    [SerializeField] private float updateIntervalSec = 1f;
    [SerializeField] private float stateIntervalSec = 1f;
    [SerializeField] private float soloStateIntervalSec = 2.5f;
    [SerializeField] private int maxConsecutiveErrors = 8;
    [SerializeField] private float errorWindowSec = 25f;
    [SerializeField] private int maxState404BeforeRecover = 3;
    [SerializeField] private float state404RecoverCooldownSec = 5f;
    [SerializeField] private float reconnectIntervalSec = 60f;
    [SerializeField] private float reconnectFirstDelaySec = 5f;
    [SerializeField] private float heartbeatIntervalSec = 3f;
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
    [Header("Debug Network")]
    [SerializeField] private bool debugSimulateOffline = false;
    [Header("WebSocket (pilot)")]
    [SerializeField] private bool useWebSocketLobby = true;
    [SerializeField] private float webSocketReconnectDelaySec = 3f;
    [SerializeField, Min(0.1f)] private float webSocketUpdateIntervalSec = 0.2f;
    [SerializeField, Min(0.1f)] private float webSocketPositionWindowSec = 0.35f;
    [SerializeField, Range(4, 24)] private int webSocketPositionTargetSamples = 8;
    [SerializeField] private float webSocketPingIntervalSec = 10f;
    [SerializeField] private float webSocketSyncRequestIntervalSec = 8f;
    [SerializeField] private int maxWebSocketMessagesPerFrame = 2;
    [SerializeField] private bool webSocketDebugLogs = false;

    [Header("Deps (optional)")]
    [SerializeField] private ZooBackendClient backend;
    [SerializeField] private ZooBaseSnapshotSync snapshotSync;

    public bool IsOnline { get; private set; }
    public bool IsInitialJoinResolved { get; private set; }
    public LobbyNetworkMode NetworkMode { get; private set; } = LobbyNetworkMode.OfflineLocal;
    public bool DebugSimulateOffline => debugSimulateOffline;
    public string LobbyId { get; private set; }
    public IReadOnlyList<LobbyMemberStateDto> LastMembers => _lastMembers;

    public event Action<List<LobbyMemberStateDto>> LobbyStateUpdated;
    public event Action<LobbyNetworkMode> NetworkModeChanged;

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
    private float _capacityRetryAfterSec = 60f;
    private bool _wsReady;
    private float _nextWsPingAt;
    private float _nextWsSyncRequestAt;
    private bool _wsSupportLogged;
    private Coroutine _wsConnectLoop;
    private readonly Queue<string> _wsInbox = new();
    private readonly object _wsInboxLock = new();
    private bool _wsConnected;
    private bool _wsConnecting;
#if !UNITY_WEBGL || UNITY_EDITOR
    private ClientWebSocket _ws;
    private CancellationTokenSource _wsCts;
    private Task _wsReceiveTask;
    private readonly SemaphoreSlim _wsSendLock = new(1, 1);
    private int _wsGeneration;
#endif
    private int _state404Count;
    private bool _stateRecoverInProgress;
    private float _lastStateRecoverAt;
    private float _stateBackoffUntil;
    private float _lastHeartbeatSentAt;
    private bool _networkAudioReady;

    private sealed class MemberParseCandidate
    {
        public LobbyMemberStateDto item;
        public JObject raw;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browser callbacks need a stable receiver name across scene loads.
            gameObject.name = "LobbyClientWebSocket";
#endif
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
        NormalizeErrorTuning();
    }

    private void NormalizeErrorTuning()
    {
        if (maxConsecutiveErrors < 8)
        {
            Debug.LogWarning($"[Lobby] maxConsecutiveErrors={maxConsecutiveErrors} is too low, forcing 8");
            maxConsecutiveErrors = 8;
        }

        if (errorWindowSec < 10f)
            errorWindowSec = 10f;

        if (maxState404BeforeRecover < 2)
            maxState404BeforeRecover = 2;

        // Keep recovery responsive even if old serialized values (e.g. 6) remain in scene/prefab.
        if (maxState404BeforeRecover > 3)
        {
            Debug.LogWarning($"[Lobby] maxState404BeforeRecover={maxState404BeforeRecover} is too high, forcing 3");
            maxState404BeforeRecover = 3;
        }

    }

    private void Start()
    {
        if (autoJoinOnStart)
            StartCoroutine(JoinLobbyFlow());
        else
            IsInitialJoinResolved = true;
    }

    private void Update()
    {
        ProcessWebSocketInbox();
        TickWebSocketHeartbeat();
    }

    private bool IsWebSocketRuntimeSupported()
    {
        return true;
    }

    private bool IsWebSocketDesired()
    {
        if (!useWebSocketLobby)
            return false;

        if (!IsWebSocketRuntimeSupported())
        {
            if (!_wsSupportLogged && webSocketDebugLogs)
            {
                Debug.Log("[LobbyWS] Disabled: runtime platform does not support WebSocket transport.");
                _wsSupportLogged = true;
            }

            return false;
        }

        return true;
    }

    private bool IsWebSocketActive()
    {
        return _wsConnected && _wsReady;
    }

    private void StartWebSocketLoopIfNeeded()
    {
        if (!IsOnline || !IsWebSocketDesired())
            return;

        if (_wsConnectLoop == null)
            _wsConnectLoop = StartCoroutine(WebSocketLoop());
    }

    private void StopWebSocketTransport()
    {
        if (_wsConnectLoop != null)
        {
            StopCoroutine(_wsConnectLoop);
            _wsConnectLoop = null;
        }

        TeardownWebSocketClient();
        _wsReady = false;
        _wsConnected = false;
    }

    private IEnumerator WebSocketLoop()
    {
        while (IsOnline && IsWebSocketDesired())
        {
            ProcessWebSocketInbox();

            if (!_wsConnected && !_wsConnecting)
                yield return ConnectWebSocket();

            if (_wsConnected)
            {
                yield return null;
                continue;
            }

            var retryDelay = Mathf.Max(0.5f, webSocketReconnectDelaySec);
            yield return new WaitForSeconds(retryDelay);
        }

        _wsConnectLoop = null;
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private IEnumerator ConnectWebSocket()
    {
        if (_wsConnecting || _wsConnected)
            yield break;

        if (!TryBuildWebSocketUrl(out var wsUrl))
            yield break;

        TeardownWebSocketClient();
        _wsConnecting = true;
        _wsReady = false;

        _wsCts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(Mathf.Max(5f, webSocketPingIntervalSec));

        Task connectTask;
        try
        {
            connectTask = _ws.ConnectAsync(new Uri(wsUrl), _wsCts.Token);
        }
        catch (Exception ex)
        {
            _wsConnecting = false;
            TeardownWebSocketClient();
            if (webSocketDebugLogs)
                Debug.LogWarning($"[LobbyWS] Connect setup failed: {ex.GetType().Name}");
            yield break;
        }

        while (!connectTask.IsCompleted)
            yield return null;

        _wsConnecting = false;
        if (connectTask.IsFaulted || _ws == null || _ws.State != WebSocketState.Open)
        {
            if (webSocketDebugLogs)
                Debug.LogWarning("[LobbyWS] Connect failed.");
            TeardownWebSocketClient();
            yield break;
        }

        _wsConnected = true;
        _wsReady = false;
        _nextWsPingAt = Time.unscaledTime + Mathf.Max(3f, webSocketPingIntervalSec);
        _nextWsSyncRequestAt = Time.unscaledTime + 1f;
        var gen = ++_wsGeneration;
        _wsReceiveTask = ReceiveWebSocketLoop(gen, _ws, _wsCts.Token);

        if (webSocketDebugLogs)
            Debug.Log($"[LobbyWS] Connected: {wsUrl}");
    }

    private async Task ReceiveWebSocketLoop(int generation, ClientWebSocket socket, CancellationToken token)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        try
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                stream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;
                    if (result.Count > 0)
                        stream.Write(buffer, 0, result.Count);
                    if (stream.Length > 128 * 1024)
                        return;
                }
                while (!result.EndOfMessage);

                if (stream.Length == 0)
                    continue;

                var msg = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                lock (_wsInboxLock)
                    _wsInbox.Enqueue(msg);
            }
        }
        catch
        {
            // Swallow here; fallback transport stays active.
        }
        finally
        {
            if (_wsGeneration == generation)
            {
                _wsConnected = false;
                _wsReady = false;
            }
        }
    }

    private void TeardownWebSocketClient()
    {
        _wsReady = false;
        _wsConnected = false;
        _wsConnecting = false;

        try
        {
            _wsCts?.Cancel();
        }
        catch
        {
        }

        if (_ws != null)
        {
            try
            {
                _ws.Abort();
            }
            catch
            {
            }

            _ws.Dispose();
            _ws = null;
        }

        _wsCts?.Dispose();
        _wsCts = null;

        lock (_wsInboxLock)
            _wsInbox.Clear();
    }

    private bool TryBuildWebSocketUrl(out string wsUrl)
    {
        wsUrl = null;
        var playerId = GetLocalPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return false;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
            return false;

        string scheme;
        if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            scheme = "wss";
        else if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            scheme = "ws";
        else
            return false;

        var builder = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
        };

        var path = (builder.Path ?? string.Empty).TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(path) ? "/ws/lobby" : $"{path}/ws/lobby";
        builder.Query = $"playerId={UnityWebRequest.EscapeURL(playerId)}";
        wsUrl = builder.Uri.ToString();
        return true;
    }

    private IEnumerator SendWebSocketMessage(JObject message, Action<bool> onDone = null)
    {
        if (!_wsConnected || _ws == null || _ws.State != WebSocketState.Open || _wsCts == null)
        {
            onDone?.Invoke(false);
            yield break;
        }

        var json = message.ToString(Formatting.None);
        var task = SendWebSocketTextAsync(json, _wsCts.Token);
        while (!task.IsCompleted)
            yield return null;

        var ok = task.Status == TaskStatus.RanToCompletion && task.Result;
        if (!ok)
        {
            _wsConnected = false;
            _wsReady = false;
        }

        onDone?.Invoke(ok);
    }

    private async Task<bool> SendWebSocketTextAsync(string text, CancellationToken token)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            return false;

        await _wsSendLock.WaitAsync(token);
        try
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                return false;

            var bytes = Encoding.UTF8.GetBytes(text);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _wsSendLock.Release();
        }
    }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int ZooWebSocketConnect(string url, string receiverName);

    [DllImport("__Internal")]
    private static extern int ZooWebSocketSend(string message);

    [DllImport("__Internal")]
    private static extern void ZooWebSocketClose();

    private bool TryBuildWebSocketUrl(out string wsUrl)
    {
        wsUrl = null;
        var playerId = GetLocalPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
            return false;

        if (string.IsNullOrWhiteSpace(BaseUrl) || !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
            return false;

        string scheme;
        if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            scheme = "wss";
        else if (string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            scheme = "ws";
        else
            return false;

        var builder = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Port = baseUri.IsDefaultPort ? -1 : baseUri.Port
        };

        var path = (builder.Path ?? string.Empty).TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(path) ? "/ws/lobby" : $"{path}/ws/lobby";
        builder.Query = $"playerId={UnityWebRequest.EscapeURL(playerId)}";
        wsUrl = builder.Uri.ToString();
        return true;
    }

    private IEnumerator ConnectWebSocket()
    {
        if (_wsConnecting || _wsConnected)
            yield break;

        if (!TryBuildWebSocketUrl(out var wsUrl))
            yield break;

        TeardownWebSocketClient();
        _wsConnecting = true;
        _wsReady = false;

        var started = Time.realtimeSinceStartup;
        var startedOk = false;
        try
        {
            startedOk = ZooWebSocketConnect(wsUrl, gameObject.name) != 0;
        }
        catch (Exception ex)
        {
            if (webSocketDebugLogs)
                Debug.LogWarning($"[LobbyWS] Browser connect setup failed: {ex.GetType().Name}");
        }

        if (!startedOk)
        {
            _wsConnecting = false;
            yield break;
        }

        while (_wsConnecting && Time.realtimeSinceStartup - started < 10f)
            yield return null;

        if (_wsConnecting)
        {
            if (webSocketDebugLogs)
                Debug.LogWarning("[LobbyWS] Browser connect timed out.");
            TeardownWebSocketClient();
        }
    }

    private void TeardownWebSocketClient()
    {
        _wsReady = false;
        _wsConnected = false;
        _wsConnecting = false;

        try
        {
            ZooWebSocketClose();
        }
        catch
        {
        }

        lock (_wsInboxLock)
            _wsInbox.Clear();
    }

    private IEnumerator SendWebSocketMessage(JObject message, Action<bool> onDone = null)
    {
        if (!_wsConnected)
        {
            onDone?.Invoke(false);
            yield break;
        }

        var ok = false;
        try
        {
            ok = ZooWebSocketSend(message.ToString(Formatting.None)) != 0;
        }
        catch
        {
            ok = false;
        }

        if (!ok)
        {
            _wsConnected = false;
            _wsReady = false;
        }

        onDone?.Invoke(ok);
        yield break;
    }

    [UnityEngine.Scripting.Preserve]
    public void OnWebSocketOpenedFromJs(string _)
    {
        _wsConnecting = false;
        _wsConnected = true;
        _wsReady = false;
        _nextWsPingAt = Time.unscaledTime + Mathf.Max(3f, webSocketPingIntervalSec);
        _nextWsSyncRequestAt = Time.unscaledTime + 1f;

        if (webSocketDebugLogs)
            Debug.Log("[LobbyWS] Browser WebSocket connected.");
    }

    [UnityEngine.Scripting.Preserve]
    public void OnWebSocketMessageFromJs(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        lock (_wsInboxLock)
            _wsInbox.Enqueue(message);
    }

    [UnityEngine.Scripting.Preserve]
    public void OnWebSocketClosedFromJs(string reason)
    {
        _wsConnecting = false;
        _wsConnected = false;
        _wsReady = false;

        if (webSocketDebugLogs)
            Debug.Log($"[LobbyWS] Browser WebSocket closed: {reason}");
    }

    [UnityEngine.Scripting.Preserve]
    public void OnWebSocketErrorFromJs(string reason)
    {
        if (webSocketDebugLogs)
            Debug.LogWarning($"[LobbyWS] Browser WebSocket error: {reason}");
    }
#endif

    private void ProcessWebSocketInbox()
    {
        var maxMessages = Mathf.Max(1, maxWebSocketMessagesPerFrame);
        for (var processed = 0; processed < maxMessages; processed++)
        {
            string msg = null;
            lock (_wsInboxLock)
            {
                if (_wsInbox.Count > 0)
                    msg = _wsInbox.Dequeue();
            }

            if (string.IsNullOrEmpty(msg))
                break;

            HandleWebSocketMessage(msg);
        }
    }

    private void HandleWebSocketMessage(string msg)
    {
        try
        {
            var obj = JObject.Parse(msg);
            var type = obj["type"]?.ToString();
            switch (type)
            {
                case "ws_ready":
                    _wsReady = true;
                    LobbyId = obj["lobbyId"]?.ToString() ?? LobbyId;
                    if (webSocketDebugLogs)
                        Debug.Log($"[LobbyWS] Ready lobbyId={LobbyId}");
                    break;

                case "lobby_state":
                    LobbyId = obj["lobbyId"]?.ToString() ?? LobbyId;
                    _lastVersion = obj["version"]?.Value<long>() ?? _lastVersion;
                    ParseMembers(obj["members"] as JArray);
                    ResetErrors();
                    _nextWsSyncRequestAt = Time.unscaledTime + Mathf.Max(3f, webSocketSyncRequestIntervalSec);
                    break;

                case "pong":
                    break;

                case "ack":
                    break;

                case "error":
                    var code = obj["code"]?.ToString();
                    if (code == "not_in_lobby")
                    {
                        HandleWsNotInLobby();
                        break;
                    }

                    if (webSocketDebugLogs)
                        Debug.LogWarning($"[LobbyWS] server error: {code} {obj["message"]}");
                    break;

                case "left":
                    _wsReady = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            if (webSocketDebugLogs)
                Debug.LogWarning($"[LobbyWS] Parse failed: {ex.GetType().Name}");
        }
    }

    private void TickWebSocketHeartbeat()
    {
        if (!IsOnline || !IsWebSocketActive())
            return;

        var now = Time.unscaledTime;
        if (now >= _nextWsPingAt)
        {
            _nextWsPingAt = now + Mathf.Max(3f, webSocketPingIntervalSec);
            StartCoroutine(SendWebSocketMessage(new JObject { ["type"] = "ping" }));
        }

        if (now >= _nextWsSyncRequestAt)
        {
            _nextWsSyncRequestAt = now + Mathf.Max(3f, webSocketSyncRequestIntervalSec);
            StartCoroutine(SendWebSocketMessage(new JObject { ["type"] = "sync_request" }));
        }
    }

    public void SetDebugSimulateOffline(bool enabled)
    {
        if (debugSimulateOffline == enabled)
            return;

        debugSimulateOffline = enabled;
        if (debugSimulateOffline)
        {
            if (IsOnline)
                DisableOnline("debug_simulated_offline");
            return;
        }

        if (!IsOnline && _reconnectLoop == null)
            _reconnectLoop = StartCoroutine(ReconnectLoop());
    }

    public IEnumerator JoinLobbyFlow()
    {
        float analyticsJoinStartedAt = Time.realtimeSinceStartup;
        if (debugSimulateOffline)
        {
            IsInitialJoinResolved = true;
            yield break;
        }

        if (IsOnline)
        {
            IsInitialJoinResolved = true;
            yield break;
        }
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
            TrackLobbyJoin("auto", LobbyJoinResult.Failed, analyticsJoinStartedAt, "backend_missing");
            IsInitialJoinResolved = true;
            yield break;
        }

        if (snapshotSync == null)
            snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();

        yield return backend.EnsureGuest();

        var joinResult = LobbyJoinResult.Failed;
        yield return JoinLobby(result => joinResult = result);
        TrackLobbyJoin("auto", joinResult, analyticsJoinStartedAt,
            joinResult == LobbyJoinResult.Success ? string.Empty : joinResult.ToString().ToLowerInvariant());
        if (joinResult != LobbyJoinResult.Success)
        {
            DisableOnline(joinResult == LobbyJoinResult.CapacityDegraded
                ? "capacity_degraded"
                : "join_failed");
            IsInitialJoinResolved = true;
            yield break;
        }

        IsOnline = true;
        SetNetworkMode(LobbyNetworkMode.Online);
        ResetErrors();
        _forceSnapshotUpload = true;
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.SetOfflineLocalOnly(false);
        backend.SetRealtimeLobbyMode();

        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);

        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());
        StartWebSocketLoopIfNeeded();
        EnsureDebugMirrorLoopState();
        IsInitialJoinResolved = true;
    }

    public IEnumerator JoinWithFriend(string friendCode, Action<bool> onDone = null)
    {
        float analyticsJoinStartedAt = Time.realtimeSinceStartup;
        if (debugSimulateOffline)
        {
            onDone?.Invoke(false);
            yield break;
        }

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
            TrackLobbyJoin("friend_code", LobbyJoinResult.Failed, analyticsJoinStartedAt, "backend_missing", friendCode);
            onDone?.Invoke(false);
            yield break;
        }

        if (snapshotSync == null)
            snapshotSync = FindAnyObjectByType<ZooBaseSnapshotSync>();

        yield return backend.EnsureGuest();

        var joinResult = LobbyJoinResult.Failed;
        yield return JoinLobbyWith(friendCode, result => joinResult = result);
        TrackLobbyJoin("friend_code", joinResult, analyticsJoinStartedAt,
            joinResult == LobbyJoinResult.Success ? string.Empty : joinResult.ToString().ToLowerInvariant(),
            friendCode);
        if (joinResult != LobbyJoinResult.Success)
        {
            if (joinResult == LobbyJoinResult.CapacityDegraded)
                DisableOnline("capacity_degraded");
            else
                RegisterError("JoinWithFriend failed");
            onDone?.Invoke(false);
            yield break;
        }

        IsOnline = true;
        SetNetworkMode(LobbyNetworkMode.Online);
        ResetErrors();
        _forceSnapshotUpload = true;
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.SetOfflineLocalOnly(false);
        backend.SetRealtimeLobbyMode();
        if (snapshotSync != null)
            snapshotSync.SetAutoPublish(false);
        if (_updateLoop == null)
            _updateLoop = StartCoroutine(UpdateLoop());
        if (_stateLoop == null)
            _stateLoop = StartCoroutine(StateLoop());
        if (_sampleLoop == null)
            _sampleLoop = StartCoroutine(SampleLoop());
        StartWebSocketLoopIfNeeded();
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

            var useWebSocket = IsWebSocketActive();
            if (useWebSocket)
                yield return UpdateLobbyViaWebSocket();
            else
                yield return UpdateLobby();
            var delay = useWebSocket
                ? Mathf.Max(0.1f, webSocketUpdateIntervalSec)
                : updateIntervalSec;
            yield return new WaitForSeconds(delay);
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

            if (IsWebSocketActive())
            {
                yield return new WaitForSeconds(Mathf.Max(0.2f, stateIntervalSec));
                continue;
            }

            yield return FetchState();
            var pollDelay = stateIntervalSec;
            if (_lastMembers.Count <= 1)
                pollDelay = Mathf.Max(stateIntervalSec, soloStateIntervalSec);
            var now = Time.realtimeSinceStartup;
            if (now < _stateBackoffUntil)
                pollDelay = Mathf.Max(pollDelay, _stateBackoffUntil - now);
            yield return new WaitForSeconds(pollDelay);
        }
    }

    private void DisableOnline(string reason)
    {
        var capacityDegraded = string.Equals(reason, "capacity_degraded", StringComparison.Ordinal);
        IsOnline = false;
        SetNetworkMode(capacityDegraded ? LobbyNetworkMode.CapacitySnapshot : LobbyNetworkMode.OfflineLocal);
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
        _state404Count = 0;
        _stateRecoverInProgress = false;
        _lastStateRecoverAt = 0f;
        _stateBackoffUntil = 0f;
        _lastHeartbeatSentAt = 0f;
        _remoteBaseRawCache.Clear();
        _remoteBaseSnapshotCache.Clear();
        _lastMembers.Clear();
        LobbyStateUpdated?.Invoke(_lastMembers);
        FlushTrafficSummary(force: true);
        StopWebSocketTransport();

        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
        {
            if (ShouldSuppressReconnectTeleport(reason))
                _remoteBases.SuppressNextAutoTeleport();
            if (capacityDegraded)
                _remoteBases.ApplyCachedLocationsFallback();
            else
                _remoteBases.ApplyOfflineLocalOnly();
        }

        if (backend != null)
        {
            if (capacityDegraded)
                backend.RequestCapacitySnapshotOnce();
            else
                backend.SetOfflineLocalOnlyMode();
        }

        if (_reconnectLoop == null && !debugSimulateOffline)
            _reconnectLoop = StartCoroutine(ReconnectLoop());
    }

    private void SetNetworkMode(LobbyNetworkMode mode)
    {
        if (NetworkMode == mode)
            return;

        var previousMode = NetworkMode;
        NetworkMode = mode;
        NetworkModeChanged?.Invoke(mode);
        GameAnalytics.TrackCritical(AnalyticsEventNames.OnlineModeChanged, GameAnalytics.Params(
            "mode_before", previousMode.ToString().ToLowerInvariant(),
            "mode_after", mode.ToString().ToLowerInvariant(),
            "lobby_id_hash", GameAnalytics.HashId(LobbyId),
            "source", "lobby_client",
            "result", "changed"),
            previousMode + ":" + mode);

        if (!_networkAudioReady)
        {
            if (mode == LobbyNetworkMode.Online)
                _networkAudioReady = true;
            return;
        }

        if (previousMode == LobbyNetworkMode.Online && mode != LobbyNetworkMode.Online)
            G.Sound?.Play(GameAudioId.SFX_NETWORK_LOST);
        else if (previousMode != LobbyNetworkMode.Online && mode == LobbyNetworkMode.Online)
            G.Sound?.Play(GameAudioId.SFX_NETWORK_RESTORED);
    }

    private static bool ShouldSuppressReconnectTeleport(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false;

        return string.Equals(reason, "debug_simulated_offline", StringComparison.Ordinal) ||
               string.Equals(reason, "ws_not_in_lobby", StringComparison.Ordinal) ||
               string.Equals(reason, "server_unreachable", StringComparison.Ordinal);
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
            if (debugSimulateOffline)
            {
                firstAttempt = true;
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            var delay = NetworkMode == LobbyNetworkMode.CapacitySnapshot
                ? Mathf.Max(5f, _capacityRetryAfterSec) + UnityEngine.Random.Range(0f, 5f)
                : firstAttempt
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
        StopWebSocketTransport();
        if (IsOnline)
            StartCoroutine(LeaveLobby());
        StartCoroutine(FlushSnapshotOnce());
    }

    private void OnDestroy()
    {
        StopWebSocketTransport();
        if (Instance == this)
            Instance = null;
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
        return BuildPositionSamplesForSend(now, mirror, positionSendWindowSec, positionTargetSamplesPerUpdate);
    }

    private List<LobbyPosSampleDto> BuildPositionSamplesForSend(
        float now,
        bool mirror,
        float sendWindowSec,
        int requestedTargetSamples)
    {
        if (_positionHistory.Count == 0) return null;

        TrimPositionHistory(now);
        if (_positionHistory.Count == 0) return null;

        var last = _positionHistory[_positionHistory.Count - 1];
        var staleSec = now - last.t;
        if (staleSec > sendWindowSec + 0.05f)
            return null;

        var windowStart = now - Mathf.Max(sendWindowSec, 0.1f);
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
        var targetSamples = Mathf.Clamp(requestedTargetSamples, 4, 60);
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

            if (!G.Save.IsReady)
                return _cachedLocalPlayerId;

            var id = G.Save.LoadBackendProfile().playerId;
            if (!string.IsNullOrEmpty(id))
                _cachedLocalPlayerId = id;
        }
        catch
        {
            // Keep the previous id during bootstrap races.
        }

        return _cachedLocalPlayerId;
    }

    public void HandleLocalSaveReset()
    {
        _cachedLocalPlayerId = null;
        _lastVersion = 0;
        _lastMembers.Clear();
        _remoteBaseRawCache.Clear();
        _remoteBaseSnapshotCache.Clear();
        LobbyStateUpdated?.Invoke(_lastMembers);

        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        if (_remoteBases != null)
            _remoteBases.HandleLocalSaveReset();

        if (IsOnline)
            DisableOnline("local_save_reset");
    }

    private void SetPlayerHeader(UnityWebRequest req)
    {
        var pid = GetLocalPlayerId();
        if (!string.IsNullOrEmpty(pid))
            req.SetRequestHeader("X-Player-Id", pid);
    }

    private void SetPlayerHeader(UnityWebRequest req, string playerId)
    {
        var pid = string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
        if (!string.IsNullOrEmpty(pid))
            req.SetRequestHeader("X-Player-Id", pid);
    }

    private IEnumerator JoinLobby(Action<LobbyJoinResult> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/join";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = Mathf.Max(5, initialRequestTimeoutSec);
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(ParseJoinFailure(req));
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
            onDone?.Invoke(LobbyJoinResult.Success);
        }
        catch
        {
            onDone?.Invoke(LobbyJoinResult.Failed);
        }

        if (slotPick >= 0)
        {
            bool claimed = false;
            yield return TryClaimSlotWithRetry(availableSlotsCache, v => claimed = v);
            if (!claimed)
            {
                onDone?.Invoke(LobbyJoinResult.Failed);
                yield break;
            }

            yield return FetchState();
        }
        else if (needAutoClaim)
        {
            bool claimed = false;
            yield return ClaimSlotAuto(v => claimed = v);
            if (!claimed)
            {
                onDone?.Invoke(LobbyJoinResult.Failed);
                yield break;
            }

            yield return FetchState();
        }

        if (IsOnline && _reconnectLoop != null)
        {
            StopCoroutine(_reconnectLoop);
            _reconnectLoop = null;
        }
    }

    private IEnumerator JoinLobbyWith(string friendCode, Action<LobbyJoinResult> onDone = null)
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
        req.timeout = Mathf.Max(5, initialRequestTimeoutSec);
        req.SetRequestHeader("Content-Type", "application/json");
        SetPlayerHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onDone?.Invoke(ParseJoinFailure(req));
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
            onDone?.Invoke(LobbyJoinResult.Success);
        }
        catch
        {
            onDone?.Invoke(LobbyJoinResult.Failed);
        }

        if (slotPick >= 0)
        {
            bool claimed = false;
            yield return TryClaimSlotWithRetry(availableSlotsCache, v => claimed = v);
            if (!claimed)
            {
                onDone?.Invoke(LobbyJoinResult.Failed);
                yield break;
            }

            yield return FetchState();
        }
        else if (needAutoClaim)
        {
            bool claimed = false;
            yield return ClaimSlotAuto(v => claimed = v);
            if (!claimed)
            {
                onDone?.Invoke(LobbyJoinResult.Failed);
                yield break;
            }

            yield return FetchState();
        }
    }

    private LobbyJoinResult ParseJoinFailure(UnityWebRequest request)
    {
        var body = request.downloadHandler != null ? request.downloadHandler.text : null;
        if (request.responseCode != 429 || string.IsNullOrWhiteSpace(body))
            return LobbyJoinResult.Failed;

        try
        {
            var payload = JObject.Parse(body);
            if (!string.Equals(payload["error"]?.ToString(), "capacity_degraded", StringComparison.Ordinal))
                return LobbyJoinResult.Failed;

            _capacityRetryAfterSec = Mathf.Clamp(
                payload["retryAfterSec"]?.Value<float>() ?? reconnectIntervalSec,
                5f,
                3600f);
            return LobbyJoinResult.CapacityDegraded;
        }
        catch
        {
            return LobbyJoinResult.Failed;
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
        req.timeout = Mathf.Max(5, initialRequestTimeoutSec);
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



    private IEnumerator ClaimSlotAuto(Action<bool> onDone = null)
    {
        var url = $"{BaseUrl}/lobby/slot/auto";
        using var req = new UnityWebRequest(url, "POST");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = Mathf.Max(5, initialRequestTimeoutSec);
        SetPlayerHeader(req);

        yield return req.SendWebRequest();
        onDone?.Invoke(req.result == UnityWebRequest.Result.Success);
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
        var autoClaimed = false;
        yield return ClaimSlotAuto(v => autoClaimed = v);
        onDone?.Invoke(autoClaimed);
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

    public IEnumerator SendGift(string toPlayerId, string itemType, string itemId, Action<bool> onDone = null, string toFriendCode = null)
    {
        var cleanPlayerId = string.IsNullOrWhiteSpace(toPlayerId) ? null : toPlayerId.Trim();
        var cleanFriendCode = string.IsNullOrWhiteSpace(toFriendCode) ? null : toFriendCode.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(cleanPlayerId) && string.IsNullOrEmpty(cleanFriendCode))
        {
            onDone?.Invoke(false);
            yield break;
        }

        var url = $"{BaseUrl}/gifts/send";
        var payload = new JObject
        {
            ["itemType"] = itemType,
            ["itemId"] = itemId
        };
        if (!string.IsNullOrEmpty(cleanPlayerId))
            payload["toPlayerId"] = cleanPlayerId;
        if (!string.IsNullOrEmpty(cleanFriendCode))
            payload["toFriendCode"] = cleanFriendCode;
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

    private JObject BuildLobbyUpdatePayload(
        out bool forceSnapshot,
        out bool sentBaseData,
        out int sampleCount,
        bool webSocketTransport = false)
    {
        var payload = new JObject();
        forceSnapshot = _forceSnapshotUpload;
        sentBaseData = false;
        sampleCount = 0;

        var snapshotJson = snapshotSync == null
            ? null
            : (forceSnapshot ? snapshotSync.BuildSnapshotJson() : snapshotSync.ConsumeDirtySnapshot());
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
        List<LobbyPosSampleDto> samplesToSend = null;
        if (_lastMembers.Count > 1)
        {
            samplesToSend = webSocketTransport
                ? BuildPositionSamplesForSend(
                    Time.realtimeSinceStartup,
                    mirror: false,
                    Mathf.Max(0.1f, webSocketPositionWindowSec),
                    webSocketPositionTargetSamples)
                : BuildPositionSamplesForSend(Time.realtimeSinceStartup, mirror: false);
        }

        if (samplesToSend != null)
        {
            payload["positions"] = JToken.FromObject(samplesToSend);
            sampleCount = samplesToSend.Count;
        }

        return payload;
    }

    private IEnumerator UpdateLobbyViaWebSocket()
    {
        var payload = BuildLobbyUpdatePayload(
            out var forceSnapshot,
            out var sentBaseData,
            out var sampleCount,
            webSocketTransport: true);
        if (payload.Count == 0)
            yield break;

        var wsMsg = new JObject
        {
            ["type"] = "lobby_update",
            ["payload"] = payload
        };

        var ok = false;
        yield return SendWebSocketMessage(wsMsg, v => ok = v);
        if (!ok)
        {
            RegisterError("WS /lobby_update send_failed");
            yield break;
        }

        if (webSocketDebugLogs && debugTrafficLogs && debugTrafficVerbose)
            Debug.Log($"[LobbyWS] sent fields={payload.Count} samples={sampleCount}");

        ResetErrors();
        if (forceSnapshot && sentBaseData)
            _forceSnapshotUpload = false;
    }

    private IEnumerator UpdateLobby()
    {
        var url = $"{BaseUrl}/lobby/update";
        var payload = BuildLobbyUpdatePayload(out var forceSnapshot, out var sentBaseData, out var sampleCount);
        var sendHeartbeatOnly = false;
        var now = Time.realtimeSinceStartup;
        if (payload.Count == 0)
        {
            if (now - _lastHeartbeatSentAt < Mathf.Max(0.5f, heartbeatIntervalSec))
            {
                if (debugTrafficLogs && debugTrafficVerbose)
                    Debug.Log("[LobbyTraffic] POST /lobby/update skipped (empty payload)");
                yield break;
            }

            // Keep lobby membership alive even when nothing changed.
            sendHeartbeatOnly = true;
            _lastHeartbeatSentAt = now;
        }

        var json = payload.Count == 0 ? "{}" : payload.ToString(Formatting.None);
        var txBytes = System.Text.Encoding.UTF8.GetByteCount(json);
        var startedAt = now;

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
            $"fields={payload.Count} samples={sampleCount} heartbeatOnly={sendHeartbeatOnly}");

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
        req.timeout = Mathf.Max(5, initialRequestTimeoutSec);
        SetPlayerHeader(req);

        var startedAt = Time.realtimeSinceStartup;
        yield return req.SendWebRequest();
        TrackHttpTraffic("GET /lobby/state", req, 0, startedAt, $"since={sinceVersion}");

        if (req.responseCode == 204)
            yield break;

        if (req.result != UnityWebRequest.Result.Success)
        {
            if (req.responseCode == 404)
            {
                HandleStateNotFound();
                yield break;
            }
            RegisterError(req, "GET /lobby/state");
            yield break;
        }

        _state404Count = 0;
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

    private void HandleStateNotFound()
    {
        if (IsAppBackgrounded())
            return;

        _state404Count++;
        var backoffSec = Mathf.Min(2f, 0.5f + _state404Count * 0.25f);
        _stateBackoffUntil = Time.realtimeSinceStartup + backoffSec;
        Debug.LogWarning($"[Lobby] State 404 ({_state404Count}/{maxState404BeforeRecover}), waiting before recover");

        if (_state404Count < maxState404BeforeRecover)
            return;
        if (_stateRecoverInProgress)
            return;

        var now = Time.realtimeSinceStartup;
        if (now - _lastStateRecoverAt < state404RecoverCooldownSec)
            return;

        _lastStateRecoverAt = now;
        StartCoroutine(RecoverAfterStateNotFound());
    }

    private IEnumerator RecoverAfterStateNotFound()
    {
        float analyticsRecoveryStartedAt = Time.realtimeSinceStartup;
        _stateRecoverInProgress = true;
        var joinResult = LobbyJoinResult.Failed;
        yield return JoinLobby(result => joinResult = result);

        if (joinResult == LobbyJoinResult.Success)
        {
            _state404Count = 0;
            _stateBackoffUntil = 0f;
            ResetErrors();
            Debug.Log("[Lobby] Recovered after state 404 via rejoin");
        }
        else if (joinResult == LobbyJoinResult.CapacityDegraded)
        {
            DisableOnline("capacity_degraded");
        }
        else
        {
            RegisterError("state_recover_join_failed");
        }

        GameAnalytics.TrackCritical(AnalyticsEventNames.LobbyRecoveryResult, GameAnalytics.Params(
            "recovery_type", "state_404_rejoin",
            "attempt_count", _state404Count,
            "latency_ms", Math.Round(Math.Max(0f, Time.realtimeSinceStartup - analyticsRecoveryStartedAt) * 1000d),
            "source", "lobby_state",
            "result", joinResult == LobbyJoinResult.Success ? "success" : "failed",
            "failure_reason", joinResult == LobbyJoinResult.Success
                ? string.Empty
                : joinResult.ToString().ToLowerInvariant()),
            "state_404_rejoin");

        _stateRecoverInProgress = false;
    }

    private static void TrackLobbyJoin(
        string joinType,
        LobbyJoinResult joinResult,
        float startedAt,
        string failureReason,
        string targetCode = null)
    {
        GameAnalytics.TrackCritical(AnalyticsEventNames.LobbyJoinResult, GameAnalytics.Params(
            "join_type", joinType,
            "target_id_hash", GameAnalytics.HashId(targetCode),
            "latency_ms", Math.Round(Math.Max(0f, Time.realtimeSinceStartup - startedAt) * 1000d),
            "source", "lobby_client",
            "result", joinResult == LobbyJoinResult.Success ? "success" : "failed",
            "failure_reason", failureReason ?? string.Empty));
    }

    private void HandleWsNotInLobby()
    {
        if (!IsOnline)
            return;

        Debug.LogWarning("[Lobby] WS reported not_in_lobby. Switching offline to trigger rejoin.");
        DisableOnline("ws_not_in_lobby");
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

        var candidates = BuildMemberCandidates(arr, localId);
        for (int i = 0; i < candidates.Count; i++)
        {
            var obj = candidates[i].raw;
            var item = candidates[i].item;
            if (obj == null || item == null)
                continue;

            var isLocalMember = !string.IsNullOrEmpty(localId) && item.playerId == localId;

            if (!isLocalMember)
            {
                var baseToken = obj["baseData"];
                if (baseToken != null && baseToken.Type != JTokenType.Null)
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
                else if (!string.IsNullOrEmpty(item.playerId))
                {
                    // Some state updates may omit baseData; keep last known snapshot
                    // so remote bases do not appear empty until the next full update.
                    if (_remoteBaseRawCache.TryGetValue(item.playerId, out var cachedRaw))
                        item.baseDataRaw = cachedRaw;
                    if (_remoteBaseSnapshotCache.TryGetValue(item.playerId, out var cachedSnapshot))
                        item.baseData = cachedSnapshot;
                }
            }

            if (!isLocalMember && item.isOnline && !string.IsNullOrEmpty(item.playerId))
                seenRemoteIds.Add(item.playerId);

            _lastMembers.Add(item);
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

    private List<MemberParseCandidate> BuildMemberCandidates(JArray arr, string localId)
    {
        var result = new List<MemberParseCandidate>();
        if (arr == null)
            return result;

        var byPlayerId = new Dictionary<string, MemberParseCandidate>(StringComparer.Ordinal);
        var playerOrder = new List<string>();
        var anonymous = new List<MemberParseCandidate>();

        foreach (var token in arr)
        {
            var obj = token as JObject;
            if (obj == null)
                continue;

            var item = ParseMemberFast(obj);
            if (item == null)
                continue;

            var candidate = new MemberParseCandidate
            {
                item = item,
                raw = obj
            };

            if (string.IsNullOrEmpty(item.playerId))
            {
                anonymous.Add(candidate);
                continue;
            }

            if (byPlayerId.TryGetValue(item.playerId, out var existing))
            {
                if (ShouldReplaceMemberCandidate(existing.item, item, localId))
                    byPlayerId[item.playerId] = candidate;
                continue;
            }

            byPlayerId.Add(item.playerId, candidate);
            playerOrder.Add(item.playerId);
        }

        for (int i = 0; i < playerOrder.Count; i++)
        {
            var pid = playerOrder[i];
            if (byPlayerId.TryGetValue(pid, out var candidate))
                result.Add(candidate);
        }

        if (anonymous.Count > 0)
            result.AddRange(anonymous);

        return result;
    }

    private bool ShouldReplaceMemberCandidate(LobbyMemberStateDto current, LobbyMemberStateDto candidate, string localId)
    {
        if (current == null)
            return true;
        if (candidate == null)
            return false;

        var currentIsLocal = !string.IsNullOrEmpty(localId) &&
                             string.Equals(current.playerId, localId, StringComparison.Ordinal);
        var candidateIsLocal = !string.IsNullOrEmpty(localId) &&
                               string.Equals(candidate.playerId, localId, StringComparison.Ordinal);
        if (currentIsLocal != candidateIsLocal)
            return candidateIsLocal;

        if (current.isOnline != candidate.isOnline)
            return candidate.isOnline;

        var updatedAtCmp = CompareUpdatedAt(candidate.updatedAt, current.updatedAt);
        if (updatedAtCmp != 0)
            return updatedAtCmp > 0;

        var currentPosCount = current.positions != null ? current.positions.Count : 0;
        var candidatePosCount = candidate.positions != null ? candidate.positions.Count : 0;
        if (currentPosCount != candidatePosCount)
            return candidatePosCount > currentPosCount;

        var currentHasHand = HasHandData(current.hand);
        var candidateHasHand = HasHandData(candidate.hand);
        if (currentHasHand != candidateHasHand)
            return candidateHasHand;

        return false;
    }

    private int CompareUpdatedAt(string lhs, string rhs)
    {
        var lhsOk = TryParseServerUtc(lhs, out var lhsUtc);
        var rhsOk = TryParseServerUtc(rhs, out var rhsUtc);

        if (lhsOk && rhsOk)
            return lhsUtc.CompareTo(rhsUtc);
        if (lhsOk)
            return 1;
        if (rhsOk)
            return -1;

        return 0;
    }

    private static bool HasHandData(LobbyHandItemDto hand)
    {
        if (hand == null)
            return false;

        return !string.IsNullOrWhiteSpace(hand.type) ||
               !string.IsNullOrWhiteSpace(hand.id) ||
               hand.element.HasValue ||
               hand.weight.HasValue ||
               hand.income.HasValue;
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

    private static bool TryParseServerUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            utc = dto.UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt))
        {
            utc = dt.ToUniversalTime();
            return true;
        }

        return false;
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
