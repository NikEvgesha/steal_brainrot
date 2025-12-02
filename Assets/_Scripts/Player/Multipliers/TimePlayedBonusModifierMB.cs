using System;
using UnityEngine;

public sealed class TimePlayedBonusModifierMB : IncomeModifierBehaviour
{
    [SerializeField] private DailyPlaytimeTrackerMB _tracker;

    [Header("Tuning")]
    [SerializeField] private float _percentPerMinute = 0.01f; // +1% / minute
    [SerializeField] private float _maxPercent = 0.50f;       // cap +50%

    [Header("Availability")]
    [SerializeField] private bool _testModeEnabled = true;
    [SerializeField] private bool _useUtcForWeekendCheck = false;

    private bool _isAllowedNow;

    public override string Id => "time_played_bonus";
    public override ModifierKind Kind => ModifierKind.PercentAdd;

    public override bool IsActive => base.IsActive && _isAllowedNow;

    public void Initialize(DailyPlaytimeTrackerMB tracker)
    {
        _tracker = tracker;
    }

    private void Awake()
    {
        Initialize(FindAnyObjectByType<DailyPlaytimeTrackerMB>());
        RecalculateAllowed();
        SubscribeTracker();
    }

    private void OnDestroy()
    {
        UnsubscribeTracker();
    }

    private void Update()
    {
        RecalculateAllowed();
    }

    public override float Value
    {
        get
        {
            if (!IsActive) return 0f;
            if (_tracker == null) return 0f;

            float minutes = Mathf.Max(0f, _tracker.TodayMinutes);
            float percent = minutes * _percentPerMinute;
            return Mathf.Min(_maxPercent, percent);
        }
    }

    public override float Progress01 => Mathf.Approximately(_maxPercent, 0f) ? 0f : Mathf.Clamp01(Value / _maxPercent);

    public override string Description =>
        "Работает в тестовом режиме или по выходным. +1% каждую минуту (до +50%) за время сегодня.";

    public void SetTestMode(bool enabled)
    {
        _testModeEnabled = enabled;
        RecalculateAllowed();
        NotifyChanged();
    }

    private void SubscribeTracker()
    {
        if (_tracker == null) return;
        _tracker.Changed += OnTrackerChanged;
    }

    private void UnsubscribeTracker()
    {
        if (_tracker == null) return;
        _tracker.Changed -= OnTrackerChanged;
    }

    private void OnTrackerChanged()
    {
        NotifyChanged();
    }

    private void RecalculateAllowed()
    {
        bool isTest = _tracker != null && _tracker.IsTest;
        bool allowed = isTest || IsWeekendNow();

        if (allowed == _isAllowedNow) return;

        _isAllowedNow = allowed;
        NotifyChanged();
    }

    private bool IsWeekendNow()
    {
        DateTime now = _useUtcForWeekendCheck ? DateTime.UtcNow : DateTime.Now;
        return now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
    }
}
