using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;

public sealed class ShopEffectsService : MonoBehaviour, IElementLuckBonusSource
{
    private const string SavePrefix = "ShopEffectsV1";
    private const float TimerRefreshInterval = 0.5f;

    public event UnityAction Changed;

    public float PermanentIncomeBonus01 { get; private set; }
    public float PermanentElementLuckBonus01 { get; private set; }
    public float PermanentElementChanceMultiplier { get; private set; } = 1f;
    public float PermanentOfflineIncomeMultiplier { get; private set; } = 1f;
    public float PermanentHatchSpeedBonus01 { get; private set; }
    public float TimedIncomeMultiplier { get; private set; } = 1f;
    public float TimedElementLuckBonus01 { get; private set; }
    public float TimedHatchSpeedBonus01 { get; private set; }
    public long TimedIncomeUntilUnix { get; private set; }
    public long TimedLuckUntilUnix { get; private set; }
    public long TimedHatchUntilUnix { get; private set; }
    public long DailyGemsPassUntilUnix { get; private set; }
    public int DailyGemsPerDay { get; private set; }
    public double TimedHatchProgressSeconds { get; private set; }

    private ShopPermanentIncomeModifier _permanentIncomeModifier;
    private ShopTimedIncomeModifier _timedIncomeModifier;
    private readonly HashSet<string> _pendingPermanentSaveKeys = new();
    private bool _registeredIncome;
    private bool _registeredLuck;
    private float _nextTimerRefresh;
    private long _timedHatchProgressUpdatedUnix;

    public static ShopEffectsService EnsureExists()
    {
        if (G.ShopEffects != null)
            return G.ShopEffects;

        var existing = FindFirstObjectByType<ShopEffectsService>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(ShopEffectsService));
        return go.AddComponent<ShopEffectsService>();
    }

    private void Awake()
    {
        if (G.ShopEffects != null && G.ShopEffects != this)
        {
            Destroy(gameObject);
            return;
        }

        G.ShopEffects = this;
        DontDestroyOnLoad(gameObject);

        _permanentIncomeModifier = gameObject.AddComponent<ShopPermanentIncomeModifier>();
        _permanentIncomeModifier.Initialize(this);
        _timedIncomeModifier = gameObject.AddComponent<ShopTimedIncomeModifier>();
        _timedIncomeModifier.Initialize(this);

        LoadTimedEffects();
        TryRegisterRuntimeSources();
    }

    private void Update()
    {
        TryRegisterRuntimeSources();
        FlushPendingPermanentSaves();
        TryGrantDailyPassReward();

        if (Time.unscaledTime < _nextTimerRefresh)
            return;

        _nextTimerRefresh = Time.unscaledTime + TimerRefreshInterval;
        bool hatchProgressChanged = IntegrateTimedHatchProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        bool expired = ExpireFinishedEffects();
        if (hatchProgressChanged && !expired)
            SaveTimedEffects();
        if (expired)
            NotifyChanged();
    }

    private void OnDestroy()
    {
        if (_registeredIncome && G.Income != null)
        {
            G.Income.Unregister(_permanentIncomeModifier);
            G.Income.Unregister(_timedIncomeModifier);
        }

        if (_registeredLuck && G.Luck != null)
            G.Luck.Unregister(this);

        if (G.ShopEffects == this)
            G.ShopEffects = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            return;

        IntegrateTimedHatchProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        SaveTimedEffects();
        SaveDailyPass();
    }

    private void OnApplicationQuit()
    {
        IntegrateTimedHatchProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        SaveTimedEffects();
        SaveDailyPass();
    }

    public void RestoreOwnedPermanentEffects(IReadOnlyList<ShopPackData> packs)
    {
        PermanentIncomeBonus01 = 0f;
        PermanentElementLuckBonus01 = 0f;
        PermanentElementChanceMultiplier = 1f;
        PermanentOfflineIncomeMultiplier = 1f;
        PermanentHatchSpeedBonus01 = 0f;

        if (packs != null)
        {
            for (int i = 0; i < packs.Count; i++)
            {
                var pack = packs[i];
                if (pack == null || pack.Rewards == null)
                    continue;

                for (int j = 0; j < pack.Rewards.Count; j++)
                {
                    var reward = pack.Rewards[j];
                    if (ShopRewardUtility.IsPermanent(reward.Type) && IsPermanentRewardOwned(pack.Id, reward.Type))
                        AddPermanentValue(reward);
                }
            }
        }

        NotifyChanged();
    }

    public bool GrantReward(string packId, ShopReward reward)
    {
        switch (reward.Type)
        {
            case ShopRewardType.PermanentIncomePercent:
            case ShopRewardType.PermanentElementLuckPercent:
            case ShopRewardType.PermanentElementChanceMultiplier:
            case ShopRewardType.PermanentOfflineIncomeMultiplier:
            case ShopRewardType.PermanentHatchSpeedPercent:
                return GrantPermanentReward(packId, reward);

            case ShopRewardType.ConsumableIncomeBoost:
            case ShopRewardType.ConsumableElementLuckBoost:
            case ShopRewardType.ConsumableHatchSkip:
            case ShopRewardType.ConsumableHatchSpeedBoost:
            case ShopRewardType.ConsumableOmniBoost:
            case ShopRewardType.InstantHatchAll:
                AddConsumable(reward.Type, Mathf.Max(1, reward.Amount));
                return true;

            case ShopRewardType.TimedIncomeBoost:
                ApplyTimedIncome(reward);
                NotifyChanged();
                return true;

            case ShopRewardType.TimedElementLuckBoost:
                ApplyTimedLuck(reward);
                NotifyChanged();
                return true;

            case ShopRewardType.DailyGemsPass:
                GrantDailyGemsPass(reward);
                return true;

            default:
                return false;
        }
    }

    public bool TryUseFirstConsumable(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null)
            return false;

        for (int i = 0; i < pack.Rewards.Count; i++)
        {
            if (ShopRewardUtility.IsConsumable(pack.Rewards[i].Type))
                return TryUse(pack.Rewards[i]);
        }

        return false;
    }

    public int GetOwnedCount(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null)
            return 0;

        for (int i = 0; i < pack.Rewards.Count; i++)
        {
            if (ShopRewardUtility.IsConsumable(pack.Rewards[i].Type))
                return GetConsumableCount(pack.Rewards[i].Type);
        }

        return 0;
    }

    public int GetConsumableCount(ShopRewardType type)
    {
        return Mathf.Max(0, PlayerPrefs.GetInt(BuildKey("Count", type.ToString()), 0));
    }

    public bool IsPermanentPackOwned(ShopPackData pack)
    {
        if (pack == null || pack.Rewards == null)
            return false;

        bool foundPermanent = false;
        for (int i = 0; i < pack.Rewards.Count; i++)
        {
            var reward = pack.Rewards[i];
            if (!ShopRewardUtility.IsPermanent(reward.Type))
                continue;

            foundPermanent = true;
            if (!IsPermanentRewardOwned(pack.Id, reward.Type))
                return false;
        }

        return foundPermanent;
    }

    public float GetElementChanceBonus01()
    {
        var timed = IsTimedLuckActive ? TimedElementLuckBonus01 : 0f;
        return Mathf.Clamp01(PermanentElementLuckBonus01 + timed);
    }

    public bool IsTimedIncomeActive => TimedIncomeUntilUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool IsTimedLuckActive => TimedLuckUntilUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool IsTimedHatchSpeedActive => TimedHatchUntilUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public bool IsDailyGemsPassActive => DailyGemsPassUntilUnix > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public int GetRemainingSeconds(ShopRewardType type)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long until;
        switch (type)
        {
            case ShopRewardType.ConsumableIncomeBoost:
            case ShopRewardType.TimedIncomeBoost:
                until = TimedIncomeUntilUnix;
                break;
            case ShopRewardType.ConsumableElementLuckBoost:
            case ShopRewardType.TimedElementLuckBoost:
                until = TimedLuckUntilUnix;
                break;
            case ShopRewardType.ConsumableHatchSpeedBoost:
                until = TimedHatchUntilUnix;
                break;
            case ShopRewardType.ConsumableOmniBoost:
                until = Math.Max(TimedIncomeUntilUnix, Math.Max(TimedLuckUntilUnix, TimedHatchUntilUnix));
                break;
            default:
                until = 0L;
                break;
        }
        return (int)Math.Min(int.MaxValue, Math.Max(0L, until - now));
    }

    public double ApplyOfflineIncome(double amount)
    {
        return Math.Max(0d, amount) * Math.Max(1f, PermanentOfflineIncomeMultiplier);
    }

    public float GetHatchDurationMultiplier()
    {
        return 1f / Mathf.Max(1f, 1f + PermanentHatchSpeedBonus01);
    }

    public double GetTimedHatchProgressSeconds()
    {
        IntegrateTimedHatchProgress(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return Math.Max(0d, TimedHatchProgressSeconds);
    }

    private bool GrantPermanentReward(string packId, ShopReward reward)
    {
        if (string.IsNullOrWhiteSpace(packId))
            return false;

        if (IsPermanentRewardOwned(packId, reward.Type))
            return true;

        SetPermanentRewardOwned(packId, reward.Type);
        AddPermanentValue(reward);
        if (reward.Type == ShopRewardType.PermanentHatchSpeedPercent)
            ApplyHatchSpeedToExistingEggs(Mathf.Max(0f, reward.EffectValue) / 100f);
        NotifyChanged();
        return true;
    }

    private void AddPermanentValue(ShopReward reward)
    {
        var value01 = Mathf.Max(0f, reward.EffectValue) / 100f;
        if (reward.Type == ShopRewardType.PermanentIncomePercent)
            PermanentIncomeBonus01 += value01;
        else if (reward.Type == ShopRewardType.PermanentElementLuckPercent)
            PermanentElementLuckBonus01 += value01;
        else if (reward.Type == ShopRewardType.PermanentElementChanceMultiplier)
            PermanentElementChanceMultiplier = Mathf.Max(
                PermanentElementChanceMultiplier,
                Mathf.Max(1f, reward.EffectValue));
        else if (reward.Type == ShopRewardType.PermanentOfflineIncomeMultiplier)
            PermanentOfflineIncomeMultiplier = Mathf.Max(
                PermanentOfflineIncomeMultiplier,
                Mathf.Max(1f, reward.EffectValue));
        else if (reward.Type == ShopRewardType.PermanentHatchSpeedPercent)
            PermanentHatchSpeedBonus01 += value01;
    }

    private bool TryUse(ShopReward reward)
    {
        int count = GetConsumableCount(reward.Type);
        if (count <= 0)
            return false;

        bool applied;
        switch (reward.Type)
        {
            case ShopRewardType.ConsumableIncomeBoost:
                ApplyTimedIncome(reward);
                applied = true;
                break;
            case ShopRewardType.ConsumableElementLuckBoost:
                ApplyTimedLuck(reward);
                applied = true;
                break;
            case ShopRewardType.ConsumableHatchSkip:
                applied = ApplyHatchSkip(reward);
                break;
            case ShopRewardType.ConsumableHatchSpeedBoost:
                ApplyTimedHatchSpeed(reward);
                applied = true;
                break;
            case ShopRewardType.ConsumableOmniBoost:
                ApplyOmniBoost(reward);
                applied = true;
                break;
            case ShopRewardType.InstantHatchAll:
                applied = ApplyInstantHatchAll();
                break;
            default:
                return false;
        }

        if (!applied)
            return false;

        SetConsumableCount(reward.Type, count - 1);
        NotifyChanged();
        return true;
    }

    private void ApplyTimedIncome(ShopReward reward)
    {
        bool wasActive = IsTimedIncomeActive;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TimedIncomeUntilUnix = Math.Max(now, TimedIncomeUntilUnix) + Mathf.Max(1, reward.DurationMinutes) * 60L;
        float newMultiplier = Mathf.Max(1f, reward.EffectValue);
        TimedIncomeMultiplier = wasActive ? Mathf.Max(TimedIncomeMultiplier, newMultiplier) : newMultiplier;
        SaveTimedEffects();
    }

    private void ApplyTimedLuck(ShopReward reward)
    {
        bool wasActive = IsTimedLuckActive;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TimedLuckUntilUnix = Math.Max(now, TimedLuckUntilUnix) + Mathf.Max(1, reward.DurationMinutes) * 60L;
        float newBonus = Mathf.Max(0f, reward.EffectValue) / 100f;
        TimedElementLuckBonus01 = wasActive ? Mathf.Max(TimedElementLuckBonus01, newBonus) : newBonus;
        SaveTimedEffects();
    }

    private void ApplyTimedHatchSpeed(ShopReward reward)
    {
        bool wasActive = IsTimedHatchSpeedActive;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        IntegrateTimedHatchProgress(now);
        TimedHatchUntilUnix = Math.Max(now, TimedHatchUntilUnix) + Mathf.Max(1, reward.DurationMinutes) * 60L;
        float newBonus = Mathf.Max(0f, reward.EffectValue) / 100f;
        TimedHatchSpeedBonus01 = wasActive ? Mathf.Max(TimedHatchSpeedBonus01, newBonus) : newBonus;
        _timedHatchProgressUpdatedUnix = now;
        SaveTimedEffects();
    }

    private void ApplyOmniBoost(ShopReward reward)
    {
        var duration = Mathf.Max(1, reward.DurationMinutes);
        var income = reward;
        income.EffectValue = reward.EffectValue > 1f ? reward.EffectValue : 2f;
        income.DurationMinutes = duration;
        ApplyTimedIncome(income);

        var luck = reward;
        luck.EffectValue = 100f;
        luck.DurationMinutes = duration;
        ApplyTimedLuck(luck);

        var hatch = reward;
        hatch.EffectValue = 25f;
        hatch.DurationMinutes = duration;
        ApplyTimedHatchSpeed(hatch);
    }

    private static bool ApplyHatchSkip(ShopReward reward)
    {
        int seconds = Mathf.Max(1, reward.DurationMinutes) * 60;
        bool changed = false;
        var eggs = FindObjectsByType<Egg>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < eggs.Length; i++)
        {
            if (eggs[i] != null && eggs[i].TryReduceHatchingTime(seconds))
                changed = true;
        }

        return changed;
    }

    private static bool ApplyInstantHatchAll()
    {
        bool changed = false;
        var eggs = FindObjectsByType<Egg>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < eggs.Length; i++)
        {
            if (eggs[i] != null && eggs[i].TryReduceHatchingTime(int.MaxValue))
                changed = true;
        }

        return changed;
    }

    private static void ApplyHatchSpeedToExistingEggs(float bonus01)
    {
        if (bonus01 <= 0f)
            return;

        var eggs = FindObjectsByType<Egg>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < eggs.Length; i++)
            eggs[i]?.ApplyHatchSpeedBonus(bonus01);
    }

    private void GrantDailyGemsPass(ShopReward reward)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int days = Mathf.Max(1, reward.DurationDays);
        bool wasActive = IsDailyGemsPassActive;
        DailyGemsPassUntilUnix = Math.Max(now, DailyGemsPassUntilUnix) + days * 86400L;
        int newDailyAmount = Mathf.Max(1, reward.Amount);
        DailyGemsPerDay = wasActive ? Mathf.Max(DailyGemsPerDay, newDailyAmount) : newDailyAmount;
        SaveDailyPass();
        TryGrantDailyPassReward();
        NotifyChanged();
    }

    private void TryGrantDailyPassReward()
    {
        if (!IsDailyGemsPassActive || DailyGemsPerDay <= 0 || G.Currency == null)
            return;

        string today = DateTimeOffset.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string key = BuildKey("Pass", "LastClaimDate");
        if (string.Equals(PlayerPrefs.GetString(key, string.Empty), today, StringComparison.Ordinal))
            return;

        G.Currency.AddCurrency(CurrencyType.Gems, DailyGemsPerDay);
        PlayerPrefs.SetString(key, today);
        PlayerPrefs.Save();
        NotifyChanged();
    }

    private void AddConsumable(ShopRewardType type, int amount)
    {
        SetConsumableCount(type, GetConsumableCount(type) + Mathf.Max(1, amount));
        NotifyChanged();
    }

    private static void SetConsumableCount(ShopRewardType type, int amount)
    {
        PlayerPrefs.SetInt(BuildKey("Count", type.ToString()), Mathf.Max(0, amount));
        PlayerPrefs.Save();
    }

    private bool IsPermanentRewardOwned(string packId, ShopRewardType type)
    {
        string key = BuildPermanentKey(packId, type);
        if (G.Save != null && G.Save.IsReady && G.Save.GetLevelStatus(key))
            return true;

        bool ownedLocally = PlayerPrefs.GetInt(BuildKey("Permanent", key), 0) == 1;
        if (ownedLocally)
            _pendingPermanentSaveKeys.Add(key);
        return ownedLocally;
    }

    private void SetPermanentRewardOwned(string packId, ShopRewardType type)
    {
        string key = BuildPermanentKey(packId, type);
        if (G.Save != null && G.Save.IsReady)
            G.Save.SaveLevelStatus(key, true);
        else
            _pendingPermanentSaveKeys.Add(key);
        PlayerPrefs.SetInt(BuildKey("Permanent", key), 1);
        PlayerPrefs.Save();
    }

    private void FlushPendingPermanentSaves()
    {
        if (_pendingPermanentSaveKeys.Count == 0 || G.Save == null || !G.Save.IsReady)
            return;

        foreach (string key in _pendingPermanentSaveKeys)
            G.Save.SaveLevelStatus(key, true);
        _pendingPermanentSaveKeys.Clear();
    }

    private void LoadTimedEffects()
    {
        TimedIncomeUntilUnix = LoadLong("IncomeUntil");
        TimedLuckUntilUnix = LoadLong("LuckUntil");
        TimedHatchUntilUnix = LoadLong("HatchUntil");
        TimedIncomeMultiplier = PlayerPrefs.GetFloat(BuildKey("Timed", "IncomeValue"), 1f);
        TimedElementLuckBonus01 = PlayerPrefs.GetFloat(BuildKey("Timed", "LuckValue"), 0f);
        TimedHatchSpeedBonus01 = PlayerPrefs.GetFloat(BuildKey("Timed", "HatchValue"), 0f);
        TimedHatchProgressSeconds = LoadDouble("HatchProgressSeconds");
        _timedHatchProgressUpdatedUnix = LoadLong("HatchProgressUpdated");
        DailyGemsPassUntilUnix = LoadLongFromGroup("Pass", "Until");
        DailyGemsPerDay = Mathf.Max(0, PlayerPrefs.GetInt(BuildKey("Pass", "DailyGems"), 0));
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_timedHatchProgressUpdatedUnix <= 0)
            _timedHatchProgressUpdatedUnix = now;
        IntegrateTimedHatchProgress(now);
        ExpireFinishedEffects();
    }

    private void SaveTimedEffects()
    {
        SaveLong("IncomeUntil", TimedIncomeUntilUnix);
        SaveLong("LuckUntil", TimedLuckUntilUnix);
        SaveLong("HatchUntil", TimedHatchUntilUnix);
        PlayerPrefs.SetFloat(BuildKey("Timed", "IncomeValue"), TimedIncomeMultiplier);
        PlayerPrefs.SetFloat(BuildKey("Timed", "LuckValue"), TimedElementLuckBonus01);
        PlayerPrefs.SetFloat(BuildKey("Timed", "HatchValue"), TimedHatchSpeedBonus01);
        PlayerPrefs.SetString(
            BuildKey("Timed", "HatchProgressSeconds"),
            TimedHatchProgressSeconds.ToString("R", CultureInfo.InvariantCulture));
        SaveLong("HatchProgressUpdated", _timedHatchProgressUpdatedUnix);
        PlayerPrefs.Save();
    }

    private void SaveDailyPass()
    {
        PlayerPrefs.SetString(
            BuildKey("Pass", "Until"),
            DailyGemsPassUntilUnix.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetInt(BuildKey("Pass", "DailyGems"), Mathf.Max(0, DailyGemsPerDay));
        PlayerPrefs.Save();
    }

    private bool ExpireFinishedEffects()
    {
        bool changed = false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (TimedIncomeUntilUnix > 0 && TimedIncomeUntilUnix <= now)
        {
            TimedIncomeUntilUnix = 0;
            TimedIncomeMultiplier = 1f;
            changed = true;
        }

        if (TimedLuckUntilUnix > 0 && TimedLuckUntilUnix <= now)
        {
            TimedLuckUntilUnix = 0;
            TimedElementLuckBonus01 = 0f;
            changed = true;
        }

        if (TimedHatchUntilUnix > 0 && TimedHatchUntilUnix <= now)
        {
            TimedHatchUntilUnix = 0;
            TimedHatchSpeedBonus01 = 0f;
            changed = true;
        }

        if (changed)
            SaveTimedEffects();

        return changed;
    }

    private bool IntegrateTimedHatchProgress(long now)
    {
        if (_timedHatchProgressUpdatedUnix <= 0)
        {
            _timedHatchProgressUpdatedUnix = now;
            return false;
        }

        if (now <= _timedHatchProgressUpdatedUnix)
            return false;

        long activeUntil = Math.Min(now, TimedHatchUntilUnix);
        long activeSeconds = Math.Max(0L, activeUntil - _timedHatchProgressUpdatedUnix);
        _timedHatchProgressUpdatedUnix = now;
        if (activeSeconds <= 0 || TimedHatchSpeedBonus01 <= 0f)
            return false;

        TimedHatchProgressSeconds += activeSeconds * (double)TimedHatchSpeedBonus01;
        return true;
    }

    private void TryRegisterRuntimeSources()
    {
        if (!_registeredIncome && G.Income != null)
        {
            G.Income.Register(_permanentIncomeModifier);
            G.Income.Register(_timedIncomeModifier);
            _registeredIncome = true;
        }

        if (!_registeredLuck && G.Luck != null)
        {
            G.Luck.Register(this);
            _registeredLuck = true;
        }
    }

    private void NotifyChanged()
    {
        _permanentIncomeModifier?.Refresh();
        _timedIncomeModifier?.Refresh();
        G.Luck?.NotifyChanged();
        Changed?.Invoke();
    }

    private static string BuildPermanentKey(string packId, ShopRewardType type)
    {
        return $"{SavePrefix}.Owned.{packId}.{type}";
    }

    private static string BuildKey(string group, string id)
    {
        return $"{SavePrefix}.{group}.{id}";
    }

    private static void SaveLong(string id, long value)
    {
        PlayerPrefs.SetString(BuildKey("Timed", id), value.ToString(CultureInfo.InvariantCulture));
    }

    private static long LoadLong(string id)
    {
        string raw = PlayerPrefs.GetString(BuildKey("Timed", id), "0");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0L;
    }

    private static long LoadLongFromGroup(string group, string id)
    {
        string raw = PlayerPrefs.GetString(BuildKey(group, id), "0");
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0L;
    }

    private static double LoadDouble(string id)
    {
        string raw = PlayerPrefs.GetString(BuildKey("Timed", id), "0");
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0d, value)
            : 0d;
    }
}

public sealed class ShopPermanentIncomeModifier : IncomeModifierBehaviour
{
    private ShopEffectsService _source;

    public override string Id => "shop_permanent_income";
    public override ModifierKind Kind => ModifierKind.PercentAdd;
    public override float Value => _source != null ? _source.PermanentIncomeBonus01 : 0f;
    public override bool IsActive => base.IsActive && Value > 0f;
    public override string Description => "Permanent shop income bonus";

    public void Initialize(ShopEffectsService source) => _source = source;
    public void Refresh() => NotifyChanged();
}

public sealed class ShopTimedIncomeModifier : IncomeModifierBehaviour
{
    private ShopEffectsService _source;

    public override string Id => "shop_timed_income";
    public override ModifierKind Kind => ModifierKind.Multiplier;
    public override float Value => _source != null && _source.IsTimedIncomeActive ? _source.TimedIncomeMultiplier : 1f;
    public override bool IsActive => base.IsActive && Value > 1f;
    public override string Description => "Timed shop income boost";

    public void Initialize(ShopEffectsService source) => _source = source;
    public void Refresh() => NotifyChanged();
}
