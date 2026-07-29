using MirraGames.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlaytimeRewardSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private GameObject _claimText;
    [SerializeField] private GameObject _claimCompleteText;
    [SerializeField] private TMP_Text _timer;
    [SerializeField] private Button _claimButton;

    private PlaytimeReward _reward;
    private TimeSpan _timeTillReward;
    private int _secondsLeft;
    private TimeSpan _rewardTime;
    private bool _claimed = false;
    private bool _initialized;
    private PlaytimeRewardPanel _panel;

    public void Init(PlaytimeReward reward, PlaytimeRewardPanel panel)
    {
        _panel = panel;
        _reward = reward;
        _amountText.text = "+" + _reward.amount;
        _rewardTime = MirraSDK.Time.CurrentDate.ToUniversalTime().TimeOfDay + TimeSpan.FromMinutes(_reward.playtimeMinutes);
        _initialized = true;
        SwitchButtonElements(false);
        if (isActiveAndEnabled)
            StartRewardTimer();
    }

    private void OnEnable()
    {
        if (!_initialized)
            return;

        StartRewardTimer();
    }

    private void StartRewardTimer()
    {
        StopAllCoroutines();
        _timeTillReward = _rewardTime - MirraSDK.Time.CurrentDate.ToUniversalTime().TimeOfDay;
        bool timerComplete = _timeTillReward <= TimeSpan.Zero;
        if (!timerComplete)
            StartCoroutine(Timer());
        else
            SwitchButtonElements(timerComplete);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void ResetReward()
    {
        _rewardTime = MirraSDK.Time.CurrentDate.ToUniversalTime().TimeOfDay + TimeSpan.FromMinutes(_reward.playtimeMinutes);
        SwitchButtonElements(false);
        _claimed = false;
    }


    private IEnumerator Timer()
    {
        while (_timeTillReward > TimeSpan.FromSeconds(1))
        {
            _timeTillReward = _rewardTime - MirraSDK.Time.CurrentDate.ToUniversalTime().TimeOfDay;
            if (_timeTillReward.TotalSeconds >= 1)
                _timer.text = String.Format(
                    "{0}:{1}",
                    (_timeTillReward.Minutes).ToString("D2"),
                    (_timeTillReward.Seconds).ToString("D2")
                );
            else
                continue;

            yield return new WaitForSecondsRealtime(1);
        }
        SwitchButtonElements(true);
        G.Sound?.Play(GameAudioId.SFX_REWARD_READY);
            

    }


    private void SwitchButtonElements(bool timerComplete)
    {
        _claimButton.interactable = timerComplete && !_claimed;
        _claimCompleteText.SetActive(timerComplete && _claimed);
        _claimText.SetActive(timerComplete && !_claimed);
        _timer.gameObject.SetActive(!timerComplete);
    }

    public void OnClaimButtonClick()
    {
        if (_claimed || !_claimButton.interactable)
            return;

        G.Currency.AddCurrency(CurrencyType.Gems, _reward.amount);
        _claimed = true;
        SwitchButtonElements(true);
        _panel.OnRewardCollect(_reward);
        GameAnalytics.Track(AnalyticsEventNames.PlaytimeRewardClaimed, GameAnalytics.Params(
            "reward_index", transform.GetSiblingIndex(),
            "required_playtime_minutes", _reward.playtimeMinutes,
            "currency_type", "gems",
            "reward_amount", _reward.amount,
            "source", "playtime_reward_panel",
            "result", "success"));
    }
}
