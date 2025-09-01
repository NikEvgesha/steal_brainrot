using MirraGames.SDK;
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
    [SerializeField] private PlaytimeRewardSlot _slotPrefab;
    [SerializeField] private GameObject _panel;
    [SerializeField] private GameObject _button;
    [SerializeField] private GameObject _indicator;

    private bool _isOpen = false;
    private List<PlaytimeRewardSlot> _slots = new();

    private List<PlaytimeReward> _availableRewards;
    private IEnumerator _timerCoroutine;
    private DateTime _lastRewardTime;
    

    private void Start()
    {
        LoadingManager.Instance.LocationChanged += ToggleButtonVisibility;
        foreach (PlaytimeReward reward in _rewards)
        {
            PlaytimeRewardSlot slot = _grid.SpawnObject<PlaytimeRewardSlot>(_slotPrefab.gameObject);
            _slots.Add(slot);
            slot.Init(reward, this);
        }
        _lastRewardTime = MirraSDK.Time.CurrentDate.ToUniversalTime().Add(TimeSpan.FromMinutes(_rewards[_rewards.Count - 1].playtimeMinutes));
        _availableRewards = new();
        _timerCoroutine = RewardTimer();
        StartCoroutine(_timerCoroutine);
    }

    private IEnumerator RewardTimer()
    {
        int i = 0;
        while (MirraSDK.Time.CurrentDate.ToUniversalTime() < _lastRewardTime && i < _rewards.Count)
        {
            yield return new WaitForSecondsRealtime(_rewards[i].playtimeMinutes * 60);
            _availableRewards.Add(_rewards[i]);
            i++;
            _indicator.SetActive(true);
        }
    }


    public void OnRewardCollect(PlaytimeReward reward)
    {
        _availableRewards.Remove(reward);
        if (_availableRewards.Count == 0)
            _indicator.SetActive(false);
    }


    private void OnEnable()
    {
        PlayerInput.Instance.APlaytime += ToggleOpen;
        PlayerInput.Instance.AOpenWindow += Close;
    }
    private void OnDisable()
    {
        PlayerInput.Instance.APlaytime -= ToggleOpen;
        PlayerInput.Instance.AOpenWindow -= Close;
        StopAllCoroutines();
    }
    public void ToggleOpen()
    {
        if (!(LoadingManager.Instance.CurrentLocation == Location.Lobby)) 
        { 
            return;
        } 
        _isOpen = !_isOpen;
        _panel.SetActive(_isOpen);
        ControlManager.Instance.CursorActive = _isOpen;

        if (_isOpen)
            PlayerInput.Instance.AOpenWindow?.Invoke(this);
    }
    public void Close(MonoBehaviour ui)
    {
        if (ui!= this && _isOpen)
            ToggleOpen();
    }
    private void ToggleButtonVisibility(Location location)
    {
        _button.SetActive(location == Location.Lobby);

/*        if (location != Location.Lobby) { 
            foreach (PlaytimeRewardSlot slot in _slots)
            {
                slot.ResetReward();
            }
        }*/

    }
}
