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
    [SerializeField] private Transform _content;
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
        //LoadingManager.Instance.LocationChanged += ToggleButtonVisibility;
        if (_slotPrefab == null || (_content == null && _grid == null))
        {
            Debug.LogError("[PlaytimeRewardPanel] Reward content or slot prefab is missing.");
            return;
        }

        foreach (PlaytimeReward reward in _rewards)
        {
            PlaytimeRewardSlot slot = _content != null
                ? Instantiate(_slotPrefab, _content)
                : _grid.SpawnObject<PlaytimeRewardSlot>(_slotPrefab.gameObject);
            _slots.Add(slot);
            slot.Init(reward, this);
        }

        if (_rewards == null || _rewards.Count == 0)
            return;

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
            if (_indicator != null)
                _indicator.SetActive(true);
        }
    }


    public void OnRewardCollect(PlaytimeReward reward)
    {
        _availableRewards.Remove(reward);
        if (_availableRewards.Count == 0)
            _indicator?.SetActive(false);
    }


    private void OnEnable()
    {
        if (G.Input == null)
            return;

        G.Input.APlaytime += ToggleOpen;
        G.Input.AOpenWindow += Close;
    }
    private void OnDisable()
    {
        if (G.Input != null)
        {
            G.Input.APlaytime -= ToggleOpen;
            G.Input.AOpenWindow -= Close;
        }
        StopAllCoroutines();
    }
    public void ToggleOpen()
    {
        //if (!(LoadingManager.Instance.CurrentLocation == Location.Lobby)) 
        //{ 
        //    return;
        //} 
        _isOpen = !_isOpen;
        _panel?.SetActive(_isOpen);
        if (G.Control != null)
            G.Control.CursorActive = _isOpen;

        if (_isOpen)
            G.Input.AOpenWindow?.Invoke(this);
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
