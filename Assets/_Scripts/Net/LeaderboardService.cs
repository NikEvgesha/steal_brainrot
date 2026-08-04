using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MirraGames.SDK;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public sealed class LeaderboardEntryDto
{
    public int rank;
    public string playerId;
    public string displayName;
    public double score;
    public string petId;
    public int? petElement;
}

[Serializable]
public sealed class LeaderboardBoardDto
{
    public string boardId;
    public string periodStart;
    public List<LeaderboardEntryDto> entries = new();
    public LeaderboardEntryDto currentPlayer;
}

[Serializable]
public sealed class LeaderboardsResponse
{
    public string generatedAtUtc;
    public string weekStartUtc;
    public string monthStartUtc;
    public List<LeaderboardBoardDto> boards = new();
}

/// <summary>
/// Keeps the five game leaderboards in Mirra Games Achievements.
/// Weekly/monthly resets are configured in the Mirra Games dashboard.
/// </summary>
public sealed class LeaderboardService : MonoBehaviour
{
    public const string DonationsBoardId = "donations_all_time";
    public const string WeeklyIncomeBoardId = "income_weekly";
    public const string WeeklyHatchesBoardId = "hatches_weekly";
    public const string MonthlyBestPetBoardId = "best_pet_monthly";
    public const string AllTimeHatchesBoardId = "hatches_all_time";

    private const string CacheKeyPrefix = "mirra.leaderboards.snapshot.v1";
    private const string ScoreKeyPrefix = "mirra.leaderboards.score.v1";
    private const double IncomeLogScale = 7_000_000d;
    private const float ProviderTimeoutSeconds = 20f;
    private const float RequestTimeoutSeconds = 10f;

    private static readonly string[] BoardIds =
    {
        DonationsBoardId,
        WeeklyIncomeBoardId,
        WeeklyHatchesBoardId,
        MonthlyBestPetBoardId,
        AllTimeHatchesBoardId
    };

    [SerializeField, Min(3)] private int topEntries = 10;
    [SerializeField, Min(2f)] private float flushIntervalSeconds = 10f;
    [SerializeField, Min(15f)] private float refreshIntervalSeconds = 60f;

    private readonly Dictionary<string, int> _scores = new();
    private IncomeModifiersHub _subscribedIncome;
    private string _playerId = "local";
    private string _displayName = "Player";
    private bool _initialized;
    private bool _flushRequested;
    private bool _refreshRequested;
    private int _legacyHatchesAtStartup;
    private double _pendingIncome;
    private long _pendingDonations;
    private int _pendingWeeklyHatches;
    private int _pendingAllTimeHatches;
    private double _pendingBestPetIncome;

    public LeaderboardsResponse Snapshot { get; private set; }
    public bool HasSnapshot => Snapshot?.boards != null;
    public bool IsUsingCachedSnapshot { get; private set; }
    public event Action<LeaderboardsResponse> SnapshotUpdated;

    private void Awake()
    {
        if (G.Leaderboards != null && G.Leaderboards != this)
        {
            Destroy(this);
            return;
        }

        G.Leaderboards = this;
        _legacyHatchesAtStartup = Math.Max(0, LocalPlayerStatsStore.GetTotalHatched());
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        EnsureIncomeSubscription();
        bool providersReady = false;
        MirraSDK.WaitForProviders(() => providersReady = true);

        float providerWait = 0f;
        while (!providersReady && providerWait < ProviderTimeoutSeconds)
        {
            EnsureIncomeSubscription();
            providerWait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!providersReady)
        {
            Debug.LogWarning("[Leaderboards] Mirra Games providers did not initialize in time.");
            yield break;
        }

        ResolvePlayerIdentity();
        LoadCachedSnapshot();
        yield return SynchronizeScores();

        EnsureAllTimeHatchesMigration();
        _initialized = true;
        EnsureIncomeSubscription();
        _refreshRequested = true;

        float flushElapsed = 0f;
        float refreshElapsed = 0f;
        while (enabled)
        {
            EnsureIncomeSubscription();
            flushElapsed += Time.unscaledDeltaTime;
            refreshElapsed += Time.unscaledDeltaTime;

            if (_flushRequested || flushElapsed >= flushIntervalSeconds)
            {
                ApplyPendingScores();
                flushElapsed = 0f;
            }

            if (_refreshRequested || !HasSnapshot || refreshElapsed >= refreshIntervalSeconds)
            {
                _refreshRequested = false;
                refreshElapsed = 0f;
                yield return RefreshSnapshot();
            }

            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (_subscribedIncome != null)
            _subscribedIncome.CoinsEarned -= RecordIncome;
        if (G.Leaderboards == this)
            G.Leaderboards = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && _initialized)
            ApplyPendingScores();
    }

    private void OnApplicationQuit()
    {
        if (_initialized)
            ApplyPendingScores();
    }

    public void RecordIncome(double amount)
    {
        if (!IsPositiveFinite(amount))
            return;

        _pendingIncome += amount;
        if (_pendingIncome >= 1000d)
            _flushRequested = true;
    }

    public void RecordHatch(string petId, ElementType element, double intrinsicIncome)
    {
        _pendingWeeklyHatches++;
        _pendingAllTimeHatches++;
        if (IsPositiveFinite(intrinsicIncome))
            _pendingBestPetIncome = Math.Max(_pendingBestPetIncome, intrinsicIncome);
        _flushRequested = true;
    }

    public void SubmitDonation(string productId, long score, Action<bool> onAccepted = null)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(productId) || score <= 0)
        {
            onAccepted?.Invoke(false);
            return;
        }

        _pendingDonations = Math.Min(int.MaxValue, _pendingDonations + score);
        ApplyPendingScores();
        RequestRefresh();
        onAccepted?.Invoke(true);
    }

    public LeaderboardBoardDto GetBoard(string boardId)
    {
        if (Snapshot?.boards == null || string.IsNullOrWhiteSpace(boardId))
            return null;
        return Snapshot.boards.FirstOrDefault(board => board != null && board.boardId == boardId);
    }

    public void RequestRefresh()
    {
        _refreshRequested = true;
    }

    public void RequestFlush()
    {
        _flushRequested = true;
    }

    private void EnsureIncomeSubscription()
    {
        if (_subscribedIncome == G.Income)
            return;
        if (_subscribedIncome != null)
            _subscribedIncome.CoinsEarned -= RecordIncome;

        _subscribedIncome = G.Income;
        if (_subscribedIncome != null)
            _subscribedIncome.CoinsEarned += RecordIncome;
    }

    private void ResolvePlayerIdentity()
    {
        try
        {
            string mirraId = MirraSDK.Player.UniqueId;
            string mirraName = MirraSDK.Player.DisplayName;
            if (!string.IsNullOrWhiteSpace(mirraId))
                _playerId = mirraId.Trim();
            else if (Application.isEditor)
                _playerId = "unity_editor";
            if (!string.IsNullOrWhiteSpace(mirraName))
                _displayName = mirraName.Trim();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Leaderboards] Cannot read Mirra player identity: {ex.Message}");
        }
    }

    private IEnumerator SynchronizeScores()
    {
        if (Application.isEditor)
        {
            foreach (string boardId in BoardIds)
                _scores[boardId] = LoadStoredScore(boardId);
            yield break;
        }

        var completed = new HashSet<string>();
        foreach (string id in BoardIds)
        {
            string boardId = id;
            MirraSDK.Achievements.GetScore(boardId, score =>
            {
                _scores[boardId] = Math.Max(0, score);
                SaveStoredScore(boardId, _scores[boardId]);
                completed.Add(boardId);
            });
        }

        float elapsed = 0f;
        while (completed.Count < BoardIds.Length && elapsed < RequestTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        foreach (string boardId in BoardIds)
        {
            if (!_scores.ContainsKey(boardId))
                _scores[boardId] = LoadStoredScore(boardId);
        }

        if (completed.Count < BoardIds.Length)
            Debug.LogWarning($"[Leaderboards] Mirra score sync timed out ({completed.Count}/{BoardIds.Length}).");
    }

    private void EnsureAllTimeHatchesMigration()
    {
        int current = GetRawScore(AllTimeHatchesBoardId);
        if (_legacyHatchesAtStartup > current)
            SetRawScore(AllTimeHatchesBoardId, _legacyHatchesAtStartup);
    }

    private void ApplyPendingScores()
    {
        if (!_initialized && _scores.Count == 0)
            return;

        _flushRequested = false;
        bool changed = false;

        if (_pendingIncome > 0d)
        {
            double total = DecodeIncome(GetRawScore(WeeklyIncomeBoardId)) + _pendingIncome;
            _pendingIncome = 0d;
            SetRawScore(WeeklyIncomeBoardId, EncodeIncome(total));
            changed = true;
        }

        if (_pendingWeeklyHatches > 0)
        {
            SetRawScore(
                WeeklyHatchesBoardId,
                SaturatingAdd(GetRawScore(WeeklyHatchesBoardId), _pendingWeeklyHatches));
            _pendingWeeklyHatches = 0;
            changed = true;
        }

        if (_pendingAllTimeHatches > 0)
        {
            SetRawScore(
                AllTimeHatchesBoardId,
                SaturatingAdd(GetRawScore(AllTimeHatchesBoardId), _pendingAllTimeHatches));
            _pendingAllTimeHatches = 0;
            changed = true;
        }

        if (_pendingBestPetIncome > 0d)
        {
            int best = EncodeIncome(_pendingBestPetIncome);
            _pendingBestPetIncome = 0d;
            if (best > GetRawScore(MonthlyBestPetBoardId))
            {
                SetRawScore(MonthlyBestPetBoardId, best);
                changed = true;
            }
        }

        if (_pendingDonations > 0)
        {
            SetRawScore(
                DonationsBoardId,
                SaturatingAdd(GetRawScore(DonationsBoardId), _pendingDonations));
            _pendingDonations = 0;
            changed = true;
        }

        if (changed)
            _refreshRequested = true;
    }

    private void SetRawScore(string boardId, int score)
    {
        score = Math.Max(0, score);
        _scores[boardId] = score;
        SaveStoredScore(boardId, score);
        if (!Application.isEditor)
            MirraSDK.Achievements.SetScore(boardId, score);
    }

    private int GetRawScore(string boardId)
    {
        return _scores.TryGetValue(boardId, out int score) ? Math.Max(0, score) : 0;
    }

    private IEnumerator RefreshSnapshot()
    {
        if (Application.isEditor)
        {
            Snapshot = BuildLocalSnapshot();
            IsUsingCachedSnapshot = false;
            SaveSnapshot();
            SnapshotUpdated?.Invoke(Snapshot);
            yield break;
        }

        var loaded = new Dictionary<string, MirraGames.SDK.Common.Leaderboard>();
        var completed = new HashSet<string>();
        foreach (string id in BoardIds)
        {
            string boardId = id;
            MirraSDK.Achievements.GetLeaderboard(boardId, leaderboard =>
            {
                loaded[boardId] = leaderboard;
                completed.Add(boardId);
            });
        }

        float elapsed = 0f;
        while (completed.Count < BoardIds.Length && elapsed < RequestTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        var response = CreateEmptySnapshot();
        bool usedCache = false;
        foreach (string boardId in BoardIds)
        {
            if (loaded.TryGetValue(boardId, out var leaderboard) && leaderboard != null)
            {
                response.boards.Add(ConvertBoard(boardId, leaderboard));
                continue;
            }

            var cached = GetBoard(boardId);
            response.boards.Add(cached ?? BuildLocalBoard(boardId, false));
            usedCache = true;
        }

        Snapshot = response;
        IsUsingCachedSnapshot = usedCache;
        SaveSnapshot();
        SnapshotUpdated?.Invoke(Snapshot);
    }

    private LeaderboardsResponse BuildLocalSnapshot()
    {
        var response = CreateEmptySnapshot();
        foreach (string boardId in BoardIds)
            response.boards.Add(BuildLocalBoard(boardId, true));
        return response;
    }

    private LeaderboardsResponse CreateEmptySnapshot()
    {
        DateTime now = DateTime.UtcNow;
        return new LeaderboardsResponse
        {
            generatedAtUtc = now.ToString("O", CultureInfo.InvariantCulture),
            weekStartUtc = GetWeekStart(now).ToString("O", CultureInfo.InvariantCulture),
            monthStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .ToString("O", CultureInfo.InvariantCulture),
            boards = new List<LeaderboardBoardDto>()
        };
    }

    private LeaderboardBoardDto BuildLocalBoard(string boardId, bool includeEntry)
    {
        int rawScore = GetRawScore(boardId);
        var current = rawScore > 0 ? CreateEntry(boardId, 1, _playerId, _displayName, rawScore) : null;
        return new LeaderboardBoardDto
        {
            boardId = boardId,
            periodStart = GetPeriodStart(boardId),
            entries = includeEntry && current != null
                ? new List<LeaderboardEntryDto> { current }
                : new List<LeaderboardEntryDto>(),
            currentPlayer = current
        };
    }

    private LeaderboardBoardDto ConvertBoard(
        string boardId,
        MirraGames.SDK.Common.Leaderboard leaderboard)
    {
        var players = leaderboard.players ?? Array.Empty<MirraGames.SDK.Common.PlayerScore>();
        var entries = players
            .Where(player => player != null)
            .OrderBy(player => player.position)
            .Take(Mathf.Clamp(topEntries, 3, 50))
            .Select(player => CreateEntry(
                boardId,
                player.position,
                string.Empty,
                player.displayName,
                player.score))
            .ToList();

        int ownRawScore = GetRawScore(boardId);
        LeaderboardEntryDto current = null;
        if (ownRawScore > 0)
        {
            var ownRow = players.FirstOrDefault(player =>
                player != null &&
                player.score == ownRawScore &&
                !string.IsNullOrWhiteSpace(_displayName) &&
                string.Equals(player.displayName, _displayName, StringComparison.OrdinalIgnoreCase));
            current = CreateEntry(
                boardId,
                ownRow != null ? ownRow.position : 0,
                _playerId,
                _displayName,
                ownRawScore);
        }

        return new LeaderboardBoardDto
        {
            boardId = boardId,
            periodStart = GetPeriodStart(boardId),
            entries = entries,
            currentPlayer = current
        };
    }

    private static LeaderboardEntryDto CreateEntry(
        string boardId,
        int rank,
        string playerId,
        string displayName,
        int rawScore)
    {
        return new LeaderboardEntryDto
        {
            rank = Math.Max(0, rank),
            playerId = playerId ?? string.Empty,
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
            score = DecodeBoardScore(boardId, rawScore)
        };
    }

    private void LoadCachedSnapshot()
    {
        try
        {
            string json = PlayerPrefs.GetString(SnapshotKey(), string.Empty);
            Snapshot = JsonConvert.DeserializeObject<LeaderboardsResponse>(json);
            if (Snapshot != null)
            {
                Snapshot.boards ??= new List<LeaderboardBoardDto>();
                IsUsingCachedSnapshot = true;
                SnapshotUpdated?.Invoke(Snapshot);
            }
        }
        catch (Exception ex)
        {
            Snapshot = null;
            Debug.LogWarning($"[Leaderboards] Cannot load cached Mirra leaderboard: {ex.Message}");
        }
    }

    private void SaveSnapshot()
    {
        try
        {
            PlayerPrefs.SetString(SnapshotKey(), JsonConvert.SerializeObject(Snapshot));
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Leaderboards] Cannot cache Mirra leaderboard: {ex.Message}");
        }
    }

    private int LoadStoredScore(string boardId)
    {
        return Math.Max(0, PlayerPrefs.GetInt(ScoreKey(boardId), 0));
    }

    private void SaveStoredScore(string boardId, int score)
    {
        PlayerPrefs.SetInt(ScoreKey(boardId), Math.Max(0, score));
        PlayerPrefs.Save();
    }

    private string SnapshotKey()
    {
        DateTime now = DateTime.UtcNow;
        return $"{CacheKeyPrefix}.{SanitizeKey(_playerId)}.{WeekToken(now)}.{now:yyyyMM}";
    }

    private string ScoreKey(string boardId)
    {
        return $"{ScoreKeyPrefix}.{SanitizeKey(_playerId)}.{boardId}.{PeriodToken(boardId)}";
    }

    private static string PeriodToken(string boardId)
    {
        DateTime now = DateTime.UtcNow;
        if (boardId == WeeklyIncomeBoardId || boardId == WeeklyHatchesBoardId)
            return WeekToken(now);
        if (boardId == MonthlyBestPetBoardId)
            return now.ToString("yyyyMM", CultureInfo.InvariantCulture);
        return "all";
    }

    private static string GetPeriodStart(string boardId)
    {
        DateTime now = DateTime.UtcNow;
        if (boardId == WeeklyIncomeBoardId || boardId == WeeklyHatchesBoardId)
            return GetWeekStart(now).ToString("O", CultureInfo.InvariantCulture);
        if (boardId == MonthlyBestPetBoardId)
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .ToString("O", CultureInfo.InvariantCulture);
        return string.Empty;
    }

    private static string WeekToken(DateTime utc)
    {
        DateTime monday = GetWeekStart(utc);
        return monday.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    private static DateTime GetWeekStart(DateTime utc)
    {
        int daysSinceMonday = ((int)utc.DayOfWeek + 6) % 7;
        return utc.Date.AddDays(-daysSinceMonday);
    }

    private static string SanitizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "local";
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static int EncodeIncome(double value)
    {
        if (!IsPositiveFinite(value))
            return 0;
        double encoded = Math.Round(Math.Log10(value + 1d) * IncomeLogScale);
        return encoded >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)encoded);
    }

    private static double DecodeIncome(int score)
    {
        if (score <= 0)
            return 0d;
        return Math.Pow(10d, score / IncomeLogScale) - 1d;
    }

    private static double DecodeBoardScore(string boardId, int rawScore)
    {
        return boardId == WeeklyIncomeBoardId || boardId == MonthlyBestPetBoardId
            ? DecodeIncome(rawScore)
            : Math.Max(0, rawScore);
    }

    private static int SaturatingAdd(int current, long addition)
    {
        long result = Math.Max(0L, current) + Math.Max(0L, addition);
        return result >= int.MaxValue ? int.MaxValue : (int)result;
    }

    private static bool IsPositiveFinite(double value)
    {
        return value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
