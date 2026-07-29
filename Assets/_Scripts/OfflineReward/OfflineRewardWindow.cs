using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OfflineRewardWindow : MonoBehaviour
{
    private OfflineRewardManager _owner;
    private TMP_Text _title;
    private TMP_Text _info;
    private TMP_Text _rewardAmount;
    private TMP_Text _elapsedText;
    private TMP_Text _maxText;
    private TMP_Text _claimText;
    private TMP_Text _boostText;
    private Button _closeButton;
    private Button _claimButton;
    private Button _boostButton;
    private Slider _timeSlider;
    private CanvasGroup _canvasGroup;

    public bool IsVisible => gameObject.activeInHierarchy &&
                             (_canvasGroup == null || _canvasGroup.alpha > 0.5f);

    public void Initialize(OfflineRewardManager owner, OfflineRewardSnapshot snapshot)
    {
        _owner = owner;
        CacheReferences();
        ConfigureRewardItems();
        BindButtons();
        Refresh(snapshot);
        SetVisible(true);
    }

    public void Refresh(OfflineRewardSnapshot snapshot)
    {
        if (_title != null)
            _title.text = LocalizationUtils.T("UI/OfflineReward/Title", "Получить офлайн-награды");
        if (_info != null)
        {
            _info.text = LocalizationUtils.Format(
                "UI/OfflineReward/Info",
                "Вы отсутствовали {0}. Накопление работает максимум {1}.",
                OfflineRewardRules.FormatDuration(snapshot.ElapsedSeconds),
                OfflineRewardRules.FormatDuration(OfflineRewardRules.MaxAccrualSeconds));
        }

        string reward = FormatMoney(snapshot.DisplayedReward);
        string boosted = FormatMoney(snapshot.BoostedReward);
        if (_rewardAmount != null)
            _rewardAmount.text = reward;
        if (_elapsedText != null)
            _elapsedText.text = OfflineRewardRules.FormatDuration(snapshot.CappedElapsedSeconds);
        if (_maxText != null)
            _maxText.text = $"MAX {OfflineRewardRules.FormatDuration(OfflineRewardRules.MaxAccrualSeconds)}";
        if (_claimText != null)
            _claimText.text = $"Забрать\n${reward}";
        if (_boostText != null)
        {
            _boostText.enableAutoSizing = true;
            _boostText.fontSizeMin = 22f;
            _boostText.fontSizeMax = 48f;
            _boostText.text = $"X{OfflineRewardRules.BoostMultiplier} за {OfflineRewardRules.BoostPriceGems} крист.\n${boosted}";
        }
        if (_timeSlider != null)
        {
            _timeSlider.minValue = 0f;
            _timeSlider.maxValue = 1f;
            _timeSlider.value = Mathf.Clamp01(
                snapshot.CappedElapsedSeconds / (float)OfflineRewardRules.MaxAccrualSeconds);
            _timeSlider.interactable = false;
        }
    }

    public void SetVisible(bool visible)
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.interactable = visible;
        _canvasGroup.blocksRaycasts = visible;
        if (G.Control != null && visible)
            G.Control.CursorActive = true;
    }

    private void CacheReferences()
    {
        _title = FindText("Text_Title");
        _info = FindText("Text_Info");
        _closeButton = FindOrCreateButton("Button_Close");
        _claimButton = FindOrCreateButton("Button_Claim");
        _boostButton = FindOrCreateButton("Button_x2Claim");

        Transform sliderRoot = FindDeepChild(transform, "Slider_Basic01_IconType");
        if (sliderRoot != null)
        {
            _timeSlider = sliderRoot.GetComponent<Slider>();
            TMP_Text[] texts = sliderRoot.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
                _elapsedText = texts[0];
            if (texts.Length > 1)
                _maxText = texts[1];
        }

        _claimText = FindButtonText(_claimButton);
        _boostText = FindButtonText(_boostButton);
    }

    private void ConfigureRewardItems()
    {
        Transform group = FindDeepChild(transform, "Group_RewardItem");
        if (group == null)
            return;

        var rewardLists = new List<Transform>();
        for (int i = 0; i < group.childCount; i++)
        {
            Transform child = group.GetChild(i);
            if (child.name.StartsWith("List", StringComparison.Ordinal))
                rewardLists.Add(child);
        }

        for (int i = 0; i < rewardLists.Count; i++)
            rewardLists[i].gameObject.SetActive(i == 0);
        if (rewardLists.Count == 0)
            return;

        Transform first = rewardLists[0];
        Image[] images = first.GetComponentsInChildren<Image>(true);
        Sprite coin = G.Currency != null ? G.Currency.GetCurrencyIcon(CurrencyType.Coins) : null;
        if (coin != null)
        {
            for (int i = images.Length - 1; i >= 0; i--)
            {
                if (images[i] == null || images[i].gameObject == first.gameObject)
                    continue;
                images[i].sprite = coin;
                images[i].preserveAspect = true;
                break;
            }
        }

        TMP_Text[] texts = first.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length > 0)
            _rewardAmount = texts[texts.Length - 1];
    }

    private void BindButtons()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => _owner?.ClaimNormal("close_button"));
        }

        if (_claimButton != null)
        {
            _claimButton.onClick.RemoveAllListeners();
            _claimButton.onClick.AddListener(() => _owner?.ClaimNormal("claim_button"));
        }

        if (_boostButton != null)
        {
            _boostButton.onClick.RemoveAllListeners();
            _boostButton.onClick.AddListener(() => _owner?.ClaimBoosted());
        }
    }

    private TMP_Text FindText(string objectName)
    {
        Transform child = FindDeepChild(transform, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Button FindOrCreateButton(string objectName)
    {
        Transform child = FindDeepChild(transform, objectName);
        if (child == null)
            return null;

        Button button = child.GetComponent<Button>();
        if (button == null)
            button = child.gameObject.AddComponent<Button>();
        if (button.targetGraphic == null)
            button.targetGraphic = child.GetComponent<Graphic>() ?? child.GetComponentInChildren<Graphic>(true);
        return button;
    }

    private static TMP_Text FindButtonText(Button button)
    {
        return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
            return null;
        if (string.Equals(parent.name, name, StringComparison.Ordinal))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string FormatMoney(double amount)
    {
        if (G.Currency != null)
            return G.Currency.ToString(Math.Max(0d, amount));
        return Math.Round(Math.Max(0d, amount)).ToString("0");
    }
}
