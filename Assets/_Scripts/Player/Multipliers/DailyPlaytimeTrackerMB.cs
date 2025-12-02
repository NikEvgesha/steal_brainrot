using System;
using UnityEngine;
using UnityEngine.Events;

public sealed class DailyPlaytimeTrackerMB : MonoBehaviour
{
    [ContextMenu("TEST/Reset Today Time")]
    private void CtxReset() => ResetTodayTime();

    [ContextMenu("TEST/Add 10 Minutes")]
    private void CtxAdd10() => AddMinutes(10f);

    [ContextMenu("TEST/Set 49 Minutes")]
    private void CtxSet49() => SetMinutes(49f);

    [Header("Time Source")]
    [SerializeField] private bool _useUtc = false;

    [Header("Persistence")]
    [SerializeField] private float _saveEverySeconds = 10f;

    [Header("Testing")]
    [SerializeField] private bool _isTest = true;
    [SerializeField] private bool _disableAutoTickInTest = false;

    private const string KeyDate = "daily_playtime_date";
    private const string KeySeconds = "daily_playtime_seconds";

    private float _todaySeconds;
    private double _lastSaveTime;
    private DateTime _currentDate;

    public UnityAction Changed;

    public float TodayMinutes => _todaySeconds / 60f;
    public bool IsTest => _isTest;

    private void Awake()
    {
        LoadOrResetIfNewDay();
        _lastSaveTime = Time.realtimeSinceStartupAsDouble;
    }

    private void Update()
    {
        bool canTick = !_isTest || !_disableAutoTickInTest;
        if (canTick)
        {
            _todaySeconds += Time.unscaledDeltaTime;
        }

        if (IsNewDay())
        {
            ResetForNewDay();
        }

        var now = Time.realtimeSinceStartupAsDouble;
        if (now - _lastSaveTime >= _saveEverySeconds)
        {
            _lastSaveTime = now;
            Save();
            Changed?.Invoke();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    // ====== Public test/debug API ======

    public void SetTestMode(bool enabled)
    {
        if (_isTest == enabled) return;
        _isTest = enabled;
        Changed?.Invoke();
    }

    public void ResetTodayTime()
    {
        _todaySeconds = 0f;
        _currentDate = GetNow().Date;
        Save();
        Changed?.Invoke();
    }

    public void AddMinutes(float minutes)
    {
        if (minutes <= 0f) return;
        _todaySeconds += minutes * 60f;
        Save();
        Changed?.Invoke();
    }

    public void AddSeconds(float seconds)
    {
        if (seconds <= 0f) return;
        _todaySeconds += seconds;
        Save();
        Changed?.Invoke();
    }

    public void SetMinutes(float minutes)
    {
        _todaySeconds = Mathf.Max(0f, minutes) * 60f;
        Save();
        Changed?.Invoke();
    }

    // ====== Internal ======

    private bool IsNewDay()
    {
        var dateNow = GetNow().Date;
        return dateNow != _currentDate;
    }

    private void LoadOrResetIfNewDay()
    {
        _currentDate = GetNow().Date;

        var savedDateString = PlayerPrefs.GetString(KeyDate, "");
        if (!DateTime.TryParse(savedDateString, out var savedDate))
        {
            _todaySeconds = 0f;
            Save();
            return;
        }

        if (savedDate.Date != _currentDate)
        {
            ResetForNewDay();
            return;
        }

        _todaySeconds = PlayerPrefs.GetFloat(KeySeconds, 0f);
    }

    private void ResetForNewDay()
    {
        _currentDate = GetNow().Date;
        _todaySeconds = 0f;
        Save();
        Changed?.Invoke();
    }

    private void Save()
    {
        PlayerPrefs.SetString(KeyDate, _currentDate.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetFloat(KeySeconds, _todaySeconds);
        PlayerPrefs.Save();
    }

    private DateTime GetNow()
    {
        return _useUtc ? DateTime.UtcNow : DateTime.Now;
    }
}
