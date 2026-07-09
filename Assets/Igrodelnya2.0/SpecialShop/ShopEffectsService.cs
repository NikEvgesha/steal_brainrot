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
    public float TimedIncomeMultiplier { get; private set; } = 1f;
    public float TimedElementLuckBonus01 { get; private set; }
    public long TimedIncomeUntilUnix { get; private set; }
    public long TimedLuckUntilUnix { get; private set; }

    private ShopPermanentIncomeModifier _permanentIncomeModifier;
    private ShopTimedIncomeModifier _timedIncomeModifier;
    private readonly HashSet<string> _pendingPermanentSaveKeys = new();
    private bool _registeredIncome;
    private bool _registeredLuck;
    private float _nextTimerRefresh;

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

        if (Time.unscaledTime < _nextTimerRefresh)
            return;

        _nextTimerRefresh = Time.unscaledTime + TimerRefreshInterval;
        if (ExpireFinishedEffects())
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

    public void RestoreOwnedPermanentEffects(IReadOnlyList<ShopPackData> packs)
    {
        PermanentIncomeBonus01 = 0f;
        PermanentElementLuckBonus01 = 0f;

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
                return GrantPermanentReward(packId, reward);

            case ShopRewardType.ConsumableIncomeBoost:
            case ShopRewardType.ConsumableElementLuckBoost:
            case ShopRewardType.ConsumableHatchSkip:
                AddConsumable(reward.Type, Mathf.Max(1, reward.Amount));
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

    public int GetRemainingSeconds(ShopRewardType type)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long until = type == ShopRewardType.ConsumableIncomeBoost ? TimedIncomeUntilUnix : TimedLuckUntilUnix;
        return (int)Math.Min(int.MaxValue, Math.Max(0L, until - now));
    }

    private bool GrantPermanentReward(string packId, ShopReward reward)
    {
        if (string.IsNullOrWhiteSpace(packId))
            return false;

        if (IsPermanentRewardOwned(packId, reward.Type))
            return true;

        SetPermanentRewardOwned(packId, reward.Type);
        AddPermanentValue(reward);
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
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TimedIncomeUntilUnix = Math.Max(now, TimedIncomeUntilUnix) + Mathf.Max(1, reward.DurationMinutes) * 60L;
        TimedIncomeMultiplier = Mathf.Max(TimedIncomeMultiplier, Mathf.Max(1f, reward.EffectValue));
        SaveTimedEffects();
    }

    private void ApplyTimedLuck(ShopReward reward)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TimedLuckUntilUnix = Math.Max(now, TimedLuckUntilUnix) + Mathf.Max(1, reward.DurationMinutes) * 60L;
        TimedElementLuckBonus01 = Mathf.Max(TimedElementLuckBonus01, Mathf.Max(0f, reward.EffectValue) / 100f);
        SaveTimedEffects();
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
        TimedIncomeMultiplier = PlayerPrefs.GetFloat(BuildKey("Timed", "IncomeValue"), 1f);
        TimedElementLuckBonus01 = PlayerPrefs.GetFloat(BuildKey("Timed", "LuckValue"), 0f);
        ExpireFinishedEffects();
    }

    private void SaveTimedEffects()
    {
        SaveLong("IncomeUntil", TimedIncomeUntilUnix);
        SaveLong("LuckUntil", TimedLuckUntilUnix);
        PlayerPrefs.SetFloat(BuildKey("Timed", "IncomeValue"), TimedIncomeMultiplier);
        PlayerPrefs.SetFloat(BuildKey("Timed", "LuckValue"), TimedElementLuckBonus01);
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

        if (changed)
            SaveTimedEffects();

        return changed;
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
