using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct PlaytimeReward
{
    public int amount;
    public int playtimeMinutes;
}

public class PlaytimeRewardPanel : MonoBehaviour
{
    [SerializeField] private List<PlaytimeReward> _rewards;
    [SerializeField] private DynamicGridSpawner _grid;
    [SerializeField] private Transform _content;
    [SerializeField] private PlaytimeRewardSlot _slotPrefab;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _button;
    [SerializeField] private GameObject _indicator;

    private bool _isOpen;
    private readonly List<PlaytimeRewardSlot> _slots = new();
    private Coroutine _rewardSequence;
    private int _activeRewardIndex;
    private float _activeSecondsRemaining;
    private int _readyRewardCount;
    private bool _initialized;
    private bool _inputSubscribed;

    private void Start()
    {
        if (_slotPrefab == null || (_content == null && _grid == null) || _rewards == null)
        {
            Debug.LogError("[PlaytimeRewardPanel] Reward content or slot prefab is missing.");
            return;
        }

        int previousMilestoneMinutes = 0;
        for (int i = 0; i < _rewards.Count; i++)
        {
            PlaytimeReward reward = _rewards[i];
            PlaytimeRewardSlot slot = _content != null
                ? Instantiate(_slotPrefab, _content)
                : _grid.SpawnObject<PlaytimeRewardSlot>(_slotPrefab.gameObject);

            // Serialized values are cumulative milestones (1, 5, 15, ...).
            // Convert them to one-at-a-time stage durations (1, 4, 10, ...).
            int milestoneMinutes = Mathf.Max(previousMilestoneMinutes, reward.playtimeMinutes);
            int stageDurationSeconds = (milestoneMinutes - previousMilestoneMinutes) * 60;
            previousMilestoneMinutes = milestoneMinutes;

            _slots.Add(slot);
            slot.Init(reward, this, i, stageDurationSeconds);
        }

        _initialized = true;
        TrySubscribeInput();
        StartRewardSequence();
    }

    private void OnEnable()
    {
        TrySubscribeInput();
        StartRewardSequence();
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        StopRewardSequence();
    }

    private void StartRewardSequence()
    {
        if (!_initialized || _rewardSequence != null || !isActiveAndEnabled || _activeRewardIndex >= _slots.Count)
            return;

        if (_activeSecondsRemaining <= 0f)
            _activeSecondsRemaining = _slots[_activeRewardIndex].StageDurationSeconds;

        _rewardSequence = StartCoroutine(RewardSequence());
    }

    private IEnumerator RewardSequence()
    {
        while (_activeRewardIndex < _slots.Count)
        {
            PlaytimeRewardSlot slot = _slots[_activeRewardIndex];
            slot.BeginCountdown(_activeSecondsRemaining);

            while (_activeSecondsRemaining > 0f)
            {
                slot.UpdateCountdown(_activeSecondsRemaining);
                yield return null;
                _activeSecondsRemaining = Mathf.Max(0f, _activeSecondsRemaining - Time.unscaledDeltaTime);
            }

            slot.UpdateCountdown(0f);
            slot.SetReady();
            _readyRewardCount++;
            if (_indicator != null)
                _indicator.SetActive(true);

            _activeRewardIndex++;
            _activeSecondsRemaining = _activeRewardIndex < _slots.Count
                ? _slots[_activeRewardIndex].StageDurationSeconds
                : 0f;

            // Keep even zero-duration stages ordered and visually deterministic.
            yield return null;
        }

        _rewardSequence = null;
    }

    public void OnRewardCollect(PlaytimeRewardSlot slot)
    {
        if (slot == null)
            return;

        _readyRewardCount = Mathf.Max(0, _readyRewardCount - 1);
        if (_readyRewardCount == 0)
            _indicator?.SetActive(false);
    }

    private void TrySubscribeInput()
    {
        if (_inputSubscribed || G.Input == null)
            return;

        G.Input.APlaytime += ToggleOpen;
        G.Input.AOpenWindow += Close;
        _inputSubscribed = true;
    }

    private void UnsubscribeInput()
    {
        if (!_inputSubscribed || G.Input == null)
            return;

        G.Input.APlaytime -= ToggleOpen;
        G.Input.AOpenWindow -= Close;
        _inputSubscribed = false;
    }

    private void StopRewardSequence()
    {
        if (_rewardSequence == null)
            return;

        StopCoroutine(_rewardSequence);
        _rewardSequence = null;
    }

    public void ToggleOpen()
    {
        _isOpen = !_isOpen;
        _panel?.SetActive(_isOpen);
        if (G.Control != null)
            G.Control.CursorActive = _isOpen;

        if (_isOpen)
            G.Input?.AOpenWindow?.Invoke(this);
    }

    public void Close(MonoBehaviour ui)
    {
        if (ui != this && _isOpen)
            ToggleOpen();
    }

    private void ToggleButtonVisibility(Location location)
    {
        if (_button != null)
            _button.SetActive(location == Location.Lobby);
    }
}
