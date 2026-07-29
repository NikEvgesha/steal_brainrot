using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class AnalyticsManager : MonoBehaviour
{
    private const int SchemaVersion = 1;
    private const int MaxQueuedEvents = 100;
    private const int MaxQueuedBytes = 256 * 1024;
    private const float QueueTtlSeconds = 300f;
    private const float CriticalQueueTtlSeconds = 1800f;
    private const float FlushIntervalSeconds = 0.1f;
    private const float RatePerSecond = 1f;
    private const float RateBurst = 15f;
    private const float BreakerDurationSeconds = 60f;
    private const float QualitySummaryIntervalSeconds = 300f;
    private const string MirraSdkVersion = "5.1.20";
    private const string SessionIndexKey = "Analytics.SessionIndex";
    private const string FirstSeenUtcKey = "Analytics.FirstSeenUtc";
    private const string OnceKeyPrefix = "Analytics.Once.";

    private static AnalyticsManager _instance;

    [SerializeField] private List<AnalyticsProvider> analyticsProviders = new List<AnalyticsProvider>();

    private readonly List<PendingEvent> _queue = new List<PendingEvent>(MaxQueuedEvents);
    private readonly Dictionary<string, float> _recentDeduplicationKeys =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Queue<float> _oneSecondTraffic = new Queue<float>();
    private readonly Queue<float> _tenSecondTraffic = new Queue<float>();
    private readonly Dictionary<string, int> _backendFailures = new Dictionary<string, int>(StringComparer.Ordinal);

    private string _sessionId;
    private int _sessionIndex;
    private long _firstSeenUtcTicks;
    private float _sessionStartedRealtime;
    private float _nextFlushAt;
    private float _nextQualitySummaryAt;
    private float _lastTokenUpdate;
    private float _tokens = RateBurst;
    private float _breakerUntil;
    private float _nextBackendFailureSummaryAt;
    private int _queuedBytes;
    private int _droppedByCapacity;
    private int _droppedByRateLimit;
    private int _providerFailures;
    private int _providerFailuresPendingReport;
    private bool _reportingDeliveryRecovery;

    private bool _gameReadyRequested;
    private bool _gameReadySent;
    private bool _sessionEventsQueued;
    private bool _gameplayRequested;
    private bool _nativeGameplayActive;
    private bool _sessionEnded;
    private bool _applicationPaused;
    private float _applicationPausedAt;
    private string _pendingLoadResult = "success";
    private string _pendingLoadFailureReason = string.Empty;

    private int _frameCount;
    private int _slowFrameCount;
    private double _frameTimeTotal;
    private float _worstFrameSeconds;
    private int _eggsHatched;
    private int _itemsAcquired;
    private int _incomeCollections;
    private double _incomeCollected;
    private double _autoClaimCollected;
    private int _autoClaimActions;
    private float _autoClaimWindowStartedAt;

    public static AnalyticsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AnalyticsManager");
                _instance = go.AddComponent<AnalyticsManager>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public bool HasProviders => analyticsProviders != null && analyticsProviders.Count > 0;
    public static AnalyticsManager Current => _instance;
    public bool IsSessionStarted => _sessionEventsQueued;
    public string SessionId => _sessionId;
    public int QueuedEventCount => _queue.Count;
    public int DroppedEventCount => _droppedByCapacity + _droppedByRateLimit;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        BeginSession();
        EnsureProviders();
        InitializeProviders();
    }

    private void Update()
    {
        SampleFrame();
        PumpNativeLifecycle();

        float now = Time.realtimeSinceStartup;
        if (now >= _nextFlushAt)
        {
            _nextFlushAt = now + FlushIntervalSeconds;
            FlushQueue(4);
            PruneDeduplicationKeys(now);
        }

        if (_sessionEventsQueued && now >= _nextQualitySummaryAt)
        {
            QueueQualitySummary("periodic");
            ResetQualityWindow();
            _nextQualitySummaryAt = now + QualitySummaryIntervalSeconds;
        }

        if (_backendFailures.Count > 0 && now >= _nextBackendFailureSummaryAt)
            QueueBackendFailureSummary("periodic");
    }

    private void OnApplicationPause(bool paused)
    {
        if (_applicationPaused == paused)
            return;

        _applicationPaused = paused;
        if (!_sessionEventsQueued)
            return;

        if (paused)
        {
            _applicationPausedAt = Time.realtimeSinceStartup;
            Track(AnalyticsEventNames.AppPause, GameAnalytics.Params("pause_reason", "application_pause"),
                AnalyticsPriority.Critical, "application_pause");
            MarkGameplayStopped("application_pause");
        }
        else
        {
            Track(AnalyticsEventNames.AppResume, GameAnalytics.Params(
                    "pause_duration_sec", Math.Max(0f, Time.realtimeSinceStartup - _applicationPausedAt),
                    "resume_reason", "application_resume"),
                AnalyticsPriority.Critical, "application_resume");
            MarkGameplayStarted("application_resume");
        }
    }

    private void OnApplicationQuit()
    {
        EndSession("application_quit");
    }

    private void OnDestroy()
    {
        if (_instance != this)
            return;

        EndSession("analytics_destroyed");
        _instance = null;
    }

    public void MarkGameReady(string loadResult = "success", string failureReason = "")
    {
        if (_gameReadyRequested)
            return;

        _gameReadyRequested = true;
        _pendingLoadResult = string.IsNullOrWhiteSpace(loadResult) ? "success" : loadResult;
        _pendingLoadFailureReason = failureReason ?? string.Empty;
    }

    public void MarkGameplayStarted(string source = "game")
    {
        _gameplayRequested = true;
        PumpNativeLifecycle();
    }

    public void MarkGameplayStopped(string source = "game")
    {
        _gameplayRequested = false;
        PumpNativeLifecycle();
    }

    public void EndSession(string reason)
    {
        if (_sessionEnded || !_sessionEventsQueued)
            return;

        _sessionEnded = true;
        QueueAutoClaimSummary("session_end");
        QueueBackendFailureSummary("session_end");
        QueueQualitySummary("session_end");
        Track(AnalyticsEventNames.SessionEnded, GameAnalytics.Params(
                "end_reason", reason ?? string.Empty,
                "session_duration_sec", SessionElapsedSeconds,
                "events_dropped", DroppedEventCount,
                "analytics_queue_size", _queue.Count),
            AnalyticsPriority.Critical, "session_end");
        FlushQueue(MaxQueuedEvents);
    }

    public void Track(
        string eventName,
        Dictionary<string, object> parameters = null,
        AnalyticsPriority priority = AnalyticsPriority.Normal,
        string deduplicationKey = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return;

        float now = Time.realtimeSinceStartup;
        RegisterTraffic(now);

        if (!string.IsNullOrWhiteSpace(deduplicationKey))
        {
            string key = eventName + ":" + deduplicationKey;
            if (_recentDeduplicationKeys.TryGetValue(key, out float lastAt) && now - lastAt < 1f)
                return;
            _recentDeduplicationKeys[key] = now;
        }

        if (now < _breakerUntil && priority != AnalyticsPriority.Critical)
        {
            _droppedByRateLimit++;
            return;
        }

        Dictionary<string, object> payload = BuildPayload(parameters);
        ObserveEvent(eventName, payload);
        Enqueue(new PendingEvent
        {
            Name = eventName.Trim(),
            Parameters = payload,
            Priority = priority,
            EnqueuedAt = now,
            EstimatedBytes = EstimateBytes(eventName, payload)
        });
    }

    public bool TrackOnce(
        string profileMilestoneKey,
        string eventName,
        Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrWhiteSpace(profileMilestoneKey))
            return false;

        string key = OnceKeyPrefix + profileMilestoneKey.Trim();
        if (PlayerPrefs.GetInt(key, 0) == 1)
            return false;

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Track(eventName, parameters, AnalyticsPriority.Critical, "once:" + profileMilestoneKey);
        return true;
    }

    public void RecordClaimAll(
        string zoneId,
        string source,
        double amount,
        double multiplier,
        int sourceCount,
        string result = "success",
        string failureReason = "")
    {
        if (string.Equals(source, "auto", StringComparison.Ordinal) && amount > 0d)
        {
            if (_autoClaimActions == 0)
                _autoClaimWindowStartedAt = Time.realtimeSinceStartup;
            _autoClaimActions++;
            _autoClaimCollected += amount;
            if (Time.realtimeSinceStartup - _autoClaimWindowStartedAt >= 60f)
                QueueAutoClaimSummary(zoneId);
            return;
        }

        Track(AnalyticsEventNames.ClaimAllResult, GameAnalytics.Params(
                "zone_id", zoneId ?? string.Empty,
                "collection_mode", source ?? string.Empty,
                "amount", amount,
                "multiplier", multiplier,
                "income_source_count", sourceCount,
                "aggregated_action_count", 1,
                "result", result ?? string.Empty,
                "failure_reason", failureReason ?? string.Empty),
            AnalyticsPriority.Normal,
            (zoneId ?? string.Empty) + ":" + (source ?? string.Empty));
    }

    public void RecordBackendFailure(string endpoint, long statusCode, string failureKind)
    {
        string safeEndpoint = string.IsNullOrWhiteSpace(endpoint) ? "unknown" : endpoint;
        string safeKind = string.IsNullOrWhiteSpace(failureKind) ? "request_failed" : failureKind;
        string key = safeEndpoint + "|" + statusCode + "|" + safeKind;
        _backendFailures.TryGetValue(key, out int count);
        _backendFailures[key] = count + 1;
        if (_nextBackendFailureSummaryAt <= 0f)
            _nextBackendFailureSummaryAt = Time.realtimeSinceStartup + 60f;
    }

    public void LogEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        AnalyticsPriority priority = eventName.StartsWith("tutorial_", StringComparison.Ordinal)
            ? AnalyticsPriority.Critical
            : AnalyticsPriority.Normal;
        Track(eventName, parameters, priority);
    }

    public void LogEvent(string eventName)
    {
        Track(eventName);
    }

    public void LogEvent(string eventName, Dictionary<string, string> parameters)
    {
        var converted = new Dictionary<string, object>(StringComparer.Ordinal);
        if (parameters != null)
        {
            foreach (KeyValuePair<string, string> pair in parameters)
                converted[pair.Key] = pair.Value;
        }
        Track(eventName, converted);
    }

    public void AddProvider(AnalyticsProvider provider)
    {
        if (provider == null || analyticsProviders.Contains(provider))
            return;

        analyticsProviders.Add(provider);
        provider.Initialize();
    }

    private void BeginSession()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        _sessionStartedRealtime = Time.realtimeSinceStartup;
        _sessionIndex = PlayerPrefs.GetInt(SessionIndexKey, 0) + 1;
        PlayerPrefs.SetInt(SessionIndexKey, _sessionIndex);

        string firstSeen = PlayerPrefs.GetString(FirstSeenUtcKey, string.Empty);
        if (!long.TryParse(firstSeen, NumberStyles.Integer, CultureInfo.InvariantCulture, out _firstSeenUtcTicks))
        {
            _firstSeenUtcTicks = DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(FirstSeenUtcKey, _firstSeenUtcTicks.ToString(CultureInfo.InvariantCulture));
        }

        PlayerPrefs.Save();
        _lastTokenUpdate = Time.realtimeSinceStartup;
        _nextQualitySummaryAt = _sessionStartedRealtime + QualitySummaryIntervalSeconds;
    }

    private void EnsureProviders()
    {
        if (analyticsProviders == null)
            analyticsProviders = new List<AnalyticsProvider>();

        analyticsProviders.RemoveAll(provider => provider == null);
        bool hasMirra = false;
        for (int i = 0; i < analyticsProviders.Count; i++)
        {
            if (analyticsProviders[i] is MirraSDKAnalyticsProvider)
            {
                hasMirra = true;
                break;
            }
        }

        if (!hasMirra)
            analyticsProviders.Add(gameObject.AddComponent<MirraSDKAnalyticsProvider>());
    }

    private void InitializeProviders()
    {
        for (int i = 0; i < analyticsProviders.Count; i++)
        {
            try
            {
                analyticsProviders[i]?.Initialize();
            }
            catch (Exception exception)
            {
                RegisterProviderFailure("initialize", exception);
            }
        }
    }

    private MirraSDKAnalyticsProvider GetMirraProvider()
    {
        for (int i = 0; i < analyticsProviders.Count; i++)
        {
            if (analyticsProviders[i] is MirraSDKAnalyticsProvider provider)
                return provider;
        }
        return null;
    }

    private void PumpNativeLifecycle()
    {
        MirraSDKAnalyticsProvider mirra = GetMirraProvider();
        if (mirra == null || !mirra.IsReady)
            return;

        if (_gameReadyRequested && !_gameReadySent)
        {
            if (!mirra.TryGameIsReady())
                return;

            _gameReadySent = true;
            QueueSessionStartEvents();
        }

        if (!_gameReadySent)
            return;

        if (_gameplayRequested && !_nativeGameplayActive)
        {
            if (mirra.TryGameplayStart())
                _nativeGameplayActive = true;
        }
        else if (!_gameplayRequested && _nativeGameplayActive)
        {
            if (mirra.TryGameplayStop())
                _nativeGameplayActive = false;
        }
    }

    private void QueueSessionStartEvents()
    {
        if (_sessionEventsQueued)
            return;

        _sessionEventsQueued = true;
        Track(AnalyticsEventNames.GameLoaded, GameAnalytics.Params(
                "load_duration_sec", SessionElapsedSeconds,
                "result", _pendingLoadResult,
                "failure_reason", _pendingLoadFailureReason),
            AnalyticsPriority.Critical, "game_loaded");
        Track(AnalyticsEventNames.SessionStarted, GameAnalytics.Params(
                "entry_point", "game_boot",
                "returning_player", _sessionIndex > 1),
            AnalyticsPriority.Critical, "session_started");
    }

    private void FlushQueue(int maxEvents)
    {
        if (_queue.Count == 0 || !HasProviders)
            return;

        RefillTokens();
        int processed = 0;
        while (processed < maxEvents && _queue.Count > 0)
        {
            int index = FindNextEventIndex();
            if (index < 0)
                break;

            PendingEvent pending = _queue[index];
            float ttl = pending.Priority == AnalyticsPriority.Critical
                ? CriticalQueueTtlSeconds
                : QueueTtlSeconds;
            if (Time.realtimeSinceStartup - pending.EnqueuedAt > ttl)
            {
                RemoveAt(index);
                _droppedByCapacity++;
                continue;
            }

            if (!pending.RateSlotConsumed)
            {
                if (_tokens < 1f)
                    break;
                _tokens -= 1f;
                pending.RateSlotConsumed = true;
            }

            bool hasReadyProvider = false;
            bool allDelivered = true;
            for (int providerIndex = 0; providerIndex < analyticsProviders.Count; providerIndex++)
            {
                AnalyticsProvider provider = analyticsProviders[providerIndex];
                if (provider == null)
                    continue;

                int providerId = provider.GetInstanceID();
                if (pending.DeliveredProviderIds.Contains(providerId))
                    continue;

                if (!provider.IsReady)
                {
                    allDelivered = false;
                    continue;
                }

                hasReadyProvider = true;
                try
                {
                    if (provider.TrySendEvent(pending.Name, pending.Parameters))
                        pending.DeliveredProviderIds.Add(providerId);
                    else
                        allDelivered = false;
                }
                catch (Exception exception)
                {
                    allDelivered = false;
                    RegisterProviderFailure(pending.Name, exception);
                }
            }

            if (!hasReadyProvider)
                break;

            if (allDelivered)
            {
                RemoveAt(index);
                QueueDeliveryRecoveryIfNeeded();
            }

            processed++;
            if (!allDelivered)
                break;
        }
    }

    private int FindNextEventIndex()
    {
        int bestIndex = -1;
        AnalyticsPriority bestPriority = AnalyticsPriority.Diagnostic;
        for (int i = 0; i < _queue.Count; i++)
        {
            if (bestIndex < 0 || _queue[i].Priority < bestPriority)
            {
                bestIndex = i;
                bestPriority = _queue[i].Priority;
            }
        }
        return bestIndex;
    }

    private void Enqueue(PendingEvent pending)
    {
        while (_queue.Count >= MaxQueuedEvents || _queuedBytes + pending.EstimatedBytes > MaxQueuedBytes)
        {
            int removable = FindOldestNonCriticalIndex();
            if (removable < 0)
            {
                _droppedByCapacity++;
                return;
            }

            RemoveAt(removable);
            _droppedByCapacity++;
        }

        _queue.Add(pending);
        _queuedBytes += pending.EstimatedBytes;
    }

    private int FindOldestNonCriticalIndex()
    {
        int candidate = -1;
        AnalyticsPriority lowestValue = AnalyticsPriority.Critical;
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].Priority == AnalyticsPriority.Critical)
                continue;
            if (candidate < 0 || _queue[i].Priority > lowestValue)
            {
                candidate = i;
                lowestValue = _queue[i].Priority;
            }
        }
        return candidate;
    }

    private void RemoveAt(int index)
    {
        _queuedBytes = Mathf.Max(0, _queuedBytes - _queue[index].EstimatedBytes);
        _queue.RemoveAt(index);
    }

    private void RefillTokens()
    {
        float now = Time.realtimeSinceStartup;
        _tokens = Mathf.Min(RateBurst, _tokens + (now - _lastTokenUpdate) * RatePerSecond);
        _lastTokenUpdate = now;
    }

    private void RegisterTraffic(float now)
    {
        _oneSecondTraffic.Enqueue(now);
        _tenSecondTraffic.Enqueue(now);
        while (_oneSecondTraffic.Count > 0 && now - _oneSecondTraffic.Peek() > 1f)
            _oneSecondTraffic.Dequeue();
        while (_tenSecondTraffic.Count > 0 && now - _tenSecondTraffic.Peek() > 10f)
            _tenSecondTraffic.Dequeue();

        if (_oneSecondTraffic.Count > 10 || _tenSecondTraffic.Count > 30)
            _breakerUntil = Mathf.Max(_breakerUntil, now + BreakerDurationSeconds);
    }

    private void PruneDeduplicationKeys(float now)
    {
        if (_recentDeduplicationKeys.Count < 128)
            return;

        var expired = new List<string>();
        foreach (KeyValuePair<string, float> pair in _recentDeduplicationKeys)
        {
            if (now - pair.Value > 10f)
                expired.Add(pair.Key);
        }
        for (int i = 0; i < expired.Count; i++)
            _recentDeduplicationKeys.Remove(expired[i]);
    }

    private Dictionary<string, object> BuildPayload(Dictionary<string, object> eventParameters)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (eventParameters != null)
        {
            foreach (KeyValuePair<string, object> pair in eventParameters)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && result.Count < 48)
                    result[pair.Key] = SanitizeValue(pair.Value);
            }
        }

        result["schema_version"] = SchemaVersion;
        result["event_id"] = Guid.NewGuid().ToString("N");
        result["session_id"] = _sessionId;
        result["session_index"] = _sessionIndex;
        result["client_version"] = Application.version ?? string.Empty;
        result["mirra_sdk_version"] = MirraSdkVersion;
        result["platform"] = Application.platform.ToString().ToLowerInvariant();
        result["language"] = G.Localization != null
            ? G.Localization.CurrentLanguage ?? string.Empty
            : Application.systemLanguage.ToString().ToLowerInvariant();
        result["input_mode"] = G.Control != null && G.Control.UseTouchControl ? "touch" : "keyboard_mouse";
        result["online_mode"] = LobbyClient.Instance != null
            ? LobbyClient.Instance.NetworkMode.ToString().ToLowerInvariant()
            : "unknown";
        result["scene"] = SceneManager.GetActiveScene().name ?? string.Empty;
        result["session_elapsed_sec"] = SessionElapsedSeconds;
        result["profile_age_days"] = ProfileAgeDays;
        result["is_first_session"] = _sessionIndex == 1;
        result["tutorial_state"] = TutorialManager.Instance == null
            ? "unknown"
            : TutorialManager.Instance.IsTutorialActive ? "active" : "inactive";
        result["tutorial_task_id"] = TutorialManager.Instance != null
            ? TutorialManager.Instance.CurrentStableId ?? string.Empty
            : string.Empty;
        result["coins_balance"] = G.Currency != null ? G.Currency.Coins : 0d;
        result["gems_balance"] = G.Currency != null ? G.Currency.Gems : 0d;
        SetDefault(result, "currency_type", string.Empty);
        SetDefault(result, "price", 0d);
        SetDefault(result, "result", string.Empty);
        SetDefault(result, "failure_reason", string.Empty);
        SetDefault(result, "source", string.Empty);
        SetDefault(result, "target_id_hash", string.Empty);
        result["build_channel"] = Debug.isDebugBuild ? "development" : "production";
        return result;
    }

    private static void SetDefault(Dictionary<string, object> target, string key, object value)
    {
        if (!target.ContainsKey(key))
            target[key] = value;
    }

    private static object SanitizeValue(object value)
    {
        if (value == null)
            return string.Empty;
        if (value is string text)
            return text.Length <= 256 ? text : text.Substring(0, 256);
        if (value is bool || value is int || value is long || value is float || value is double)
            return value;
        if (value is Enum)
            return value.ToString().ToLowerInvariant();
        if (value is decimal decimalValue)
            return Convert.ToDouble(decimalValue, CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int EstimateBytes(string eventName, Dictionary<string, object> parameters)
    {
        int bytes = (eventName?.Length ?? 0) * 2 + 32;
        foreach (KeyValuePair<string, object> pair in parameters)
        {
            bytes += pair.Key.Length * 2;
            bytes += (Convert.ToString(pair.Value, CultureInfo.InvariantCulture)?.Length ?? 0) * 2;
            bytes += 8;
        }
        return bytes;
    }

    private void ObserveEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (eventName == AnalyticsEventNames.AnimalHatched)
            _eggsHatched++;
        else if (eventName == AnalyticsEventNames.ItemAcquired)
            _itemsAcquired++;
        else if (eventName == AnalyticsEventNames.IncomeCollected)
        {
            _incomeCollections++;
            if (parameters.TryGetValue("amount", out object amount))
            {
                try
                {
                    _incomeCollected += Convert.ToDouble(amount, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }
        }
    }

    private void SampleFrame()
    {
        float delta = Time.unscaledDeltaTime;
        if (delta <= 0f || delta > 5f)
            return;

        _frameCount++;
        _frameTimeTotal += delta;
        _worstFrameSeconds = Mathf.Max(_worstFrameSeconds, delta);
        if (delta > 0.05f)
            _slowFrameCount++;
    }

    private void QueueQualitySummary(string window)
    {
        if (_frameCount <= 0)
            return;

        double averageFps = _frameTimeTotal > 0d ? _frameCount / _frameTimeTotal : 0d;
        GameAnalytics.Track(AnalyticsEventNames.SessionQualitySummary, GameAnalytics.Params(
                "window", window,
                "average_fps", Math.Round(averageFps, 1),
                "worst_frame_ms", Math.Round(_worstFrameSeconds * 1000d, 1),
                "slow_frame_ratio", Math.Round((double)_slowFrameCount / _frameCount, 4),
                "eggs_hatched", _eggsHatched,
                "items_acquired", _itemsAcquired,
                "income_collections", _incomeCollections,
                "income_collected", _incomeCollected,
                "events_dropped", DroppedEventCount,
                "analytics_queue_size", _queue.Count),
            AnalyticsPriority.Diagnostic,
            "quality:" + window);
    }

    private void QueueAutoClaimSummary(string zoneId)
    {
        if (_autoClaimActions <= 0)
            return;

        int actions = _autoClaimActions;
        double amount = _autoClaimCollected;
        _autoClaimActions = 0;
        _autoClaimCollected = 0d;
        _autoClaimWindowStartedAt = 0f;
        Track(AnalyticsEventNames.ClaimAllResult, GameAnalytics.Params(
                "zone_id", zoneId ?? string.Empty,
                "collection_mode", "auto",
                "amount", amount,
                "multiplier", 1d,
                "income_source_count", 0,
                "aggregated_action_count", actions,
                "result", "success"),
            AnalyticsPriority.Normal,
            "auto_claim_window");
    }

    private void QueueBackendFailureSummary(string window)
    {
        if (_backendFailures.Count == 0)
            return;

        string topKey = string.Empty;
        int topCount = 0;
        int total = 0;
        foreach (KeyValuePair<string, int> pair in _backendFailures)
        {
            total += pair.Value;
            if (pair.Value > topCount)
            {
                topKey = pair.Key;
                topCount = pair.Value;
            }
        }

        int uniqueGroups = _backendFailures.Count;
        string[] parts = topKey.Split('|');
        _backendFailures.Clear();
        _nextBackendFailureSummaryAt = 0f;
        Track(AnalyticsEventNames.BackendFailureSummary, GameAnalytics.Params(
                "window", window,
                "failure_count", total,
                "unique_failure_groups", uniqueGroups,
                "top_endpoint", parts.Length > 0 ? parts[0] : "unknown",
                "top_status_code", parts.Length > 1 ? parts[1] : string.Empty,
                "top_failure_kind", parts.Length > 2 ? parts[2] : string.Empty,
                "top_failure_count", topCount,
                "source", "backend_client",
                "result", "failed"),
            AnalyticsPriority.Diagnostic,
            "backend_failures:" + window);
    }

    private void ResetQualityWindow()
    {
        _frameCount = 0;
        _slowFrameCount = 0;
        _frameTimeTotal = 0d;
        _worstFrameSeconds = 0f;
    }

    private void RegisterProviderFailure(string operation, Exception exception)
    {
        _providerFailures++;
        _providerFailuresPendingReport++;
        if (_providerFailures == 1 || _providerFailures % 25 == 0)
            Debug.LogWarning($"Analytics provider failure during '{operation}': {exception.GetType().Name}");
    }

    private void QueueDeliveryRecoveryIfNeeded()
    {
        if (_providerFailuresPendingReport <= 0 || _reportingDeliveryRecovery)
            return;

        int failures = _providerFailuresPendingReport;
        _providerFailuresPendingReport = 0;
        _reportingDeliveryRecovery = true;
        Track(AnalyticsEventNames.AnalyticsDeliveryError, GameAnalytics.Params(
                "failure_count", failures,
                "queue_size", _queue.Count,
                "result", "recovered"),
            AnalyticsPriority.Diagnostic,
            "delivery_recovery");
        _reportingDeliveryRecovery = false;
    }

    private double SessionElapsedSeconds =>
        Math.Round(Math.Max(0f, Time.realtimeSinceStartup - _sessionStartedRealtime), 2);

    private int ProfileAgeDays
    {
        get
        {
            if (_firstSeenUtcTicks <= 0)
                return 0;
            try
            {
                return Math.Max(0, (int)(DateTime.UtcNow - new DateTime(_firstSeenUtcTicks, DateTimeKind.Utc)).TotalDays);
            }
            catch
            {
                return 0;
            }
        }
    }

    private sealed class PendingEvent
    {
        public string Name;
        public Dictionary<string, object> Parameters;
        public AnalyticsPriority Priority;
        public float EnqueuedAt;
        public int EstimatedBytes;
        public bool RateSlotConsumed;
        public readonly HashSet<int> DeliveredProviderIds = new HashSet<int>();
    }
}
