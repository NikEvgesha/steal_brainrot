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
    private int _rewardIndex;
    private int _stageDurationSeconds;
    private bool _claimed;
    private bool _ready;
    private PlaytimeRewardPanel _panel;
    private GameObject _timerPanel;
    private Image _claimButtonImage;

    public int StageDurationSeconds => _stageDurationSeconds;

    public void Init(
        PlaytimeReward reward,
        PlaytimeRewardPanel panel,
        int rewardIndex,
        int stageDurationSeconds)
    {
        _panel = panel;
        _reward = reward;
        _rewardIndex = rewardIndex;
        _stageDurationSeconds = Mathf.Max(0, stageDurationSeconds);

        if (_amountText != null)
            _amountText.text = "+" + _reward.amount;

        _timerPanel = _timer != null && _timer.transform.parent != null
            ? _timer.transform.parent.gameObject
            : null;
        _claimButtonImage = _claimButton != null
            ? (_claimButton.targetGraphic as Image ?? _claimButton.GetComponent<Image>())
            : null;

        ConfigureVisuals();
        ShowWaiting();
    }

    public void ResetReward()
    {
        _claimed = false;
        _ready = false;
        ShowWaiting();
    }

    public void BeginCountdown(float secondsRemaining)
    {
        ShowTimer(true);
        UpdateCountdown(secondsRemaining);
    }

    public void UpdateCountdown(float secondsRemaining)
    {
        if (_timer != null)
            _timer.text = FormatDuration(secondsRemaining);
    }

    public void SetReady()
    {
        _ready = true;
        _claimed = false;

        if (_timerPanel != null)
            _timerPanel.SetActive(false);
        if (_claimButton != null)
        {
            _claimButton.gameObject.SetActive(true);
            _claimButton.interactable = true;
        }
        if (_claimText != null)
            _claimText.SetActive(true);
        if (_claimCompleteText != null)
            _claimCompleteText.SetActive(false);
        if (_claimButtonImage != null)
            _claimButtonImage.color = BlockyUITheme.GreenHeader;

        G.Sound?.Play(GameAudioId.SFX_REWARD_READY);
    }

    public void OnClaimButtonClick()
    {
        if (_claimed || !_ready || _claimButton == null || !_claimButton.interactable || G.Currency == null)
            return;

        G.Currency.AddCurrency(CurrencyType.Gems, _reward.amount);
        _claimed = true;
        _ready = false;
        _claimButton.interactable = false;
        if (_claimText != null)
            _claimText.SetActive(false);
        if (_claimCompleteText != null)
            _claimCompleteText.SetActive(true);
        if (_claimButtonImage != null)
            _claimButtonImage.color = new Color(0.34f, 0.42f, 0.34f, 1f);

        _panel?.OnRewardCollect(this);
        GameAnalytics.Track(AnalyticsEventNames.PlaytimeRewardClaimed, GameAnalytics.Params(
            "reward_index", _rewardIndex,
            "required_playtime_minutes", _reward.playtimeMinutes,
            "currency_type", "gems",
            "reward_amount", _reward.amount,
            "source", "playtime_reward_panel",
            "result", "success"));
    }

    private void ShowWaiting()
    {
        ShowTimer(false);
        UpdateCountdown(_stageDurationSeconds);
    }

    private void ShowTimer(bool activeCountdown)
    {
        if (_timerPanel != null)
            _timerPanel.SetActive(true);
        if (_timer != null)
        {
            _timer.gameObject.SetActive(true);
            _timer.color = activeCountdown
                ? BlockyUITheme.YellowAccent
                : new Color(0.78f, 0.78f, 0.78f, 1f);
        }
        if (_claimButton != null)
        {
            _claimButton.interactable = false;
            _claimButton.gameObject.SetActive(false);
        }
        if (_claimText != null)
            _claimText.SetActive(false);
        if (_claimCompleteText != null)
            _claimCompleteText.SetActive(false);
    }

    private void ConfigureVisuals()
    {
        var gemRect = transform.Find("GemIcon") as RectTransform;
        if (gemRect != null)
        {
            gemRect.anchorMin = new Vector2(0.5f, 0.67f);
            gemRect.anchorMax = gemRect.anchorMin;
            gemRect.anchoredPosition = new Vector2(-42f, 0f);
            gemRect.sizeDelta = new Vector2(70f, 70f);
        }

        if (_amountText != null)
        {
            var amountRect = _amountText.rectTransform;
            amountRect.anchorMin = new Vector2(0.5f, 0.67f);
            amountRect.anchorMax = amountRect.anchorMin;
            amountRect.anchoredPosition = new Vector2(42f, 0f);
            amountRect.sizeDelta = new Vector2(110f, 70f);
            _amountText.alignment = TextAlignmentOptions.MidlineLeft;
            _amountText.enableAutoSizing = true;
            _amountText.fontSizeMin = 24f;
            _amountText.fontSizeMax = 46f;
            _amountText.outlineColor = BlockyUITheme.BlackStroke;
            _amountText.outlineWidth = Mathf.Max(_amountText.outlineWidth, 0.16f);
        }

        ConfigureActionRect(_timerPanel != null ? _timerPanel.transform as RectTransform : null);
        ConfigureActionRect(_claimButton != null ? _claimButton.transform as RectTransform : null);

        if (_timer != null)
        {
            _timer.alignment = TextAlignmentOptions.Center;
            _timer.enableAutoSizing = true;
            _timer.fontSizeMin = 20f;
            _timer.fontSizeMax = 34f;
            _timer.outlineColor = BlockyUITheme.BlackStroke;
            _timer.outlineWidth = Mathf.Max(_timer.outlineWidth, 0.14f);
        }

        var cardImage = GetComponent<Image>();
        if (cardImage != null)
            cardImage.color = new Color(0.08f, 0.49f, 0.92f, 1f);
    }

    private static void ConfigureActionRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.08f, 0.08f);
        rect.anchorMax = new Vector2(0.92f, 0.34f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }
}
