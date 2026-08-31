using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpecialShopEternalRewardCellView : MonoBehaviour
{
    [Header("Visual references")]
    [SerializeField] private Image _background;
    [SerializeField] private Image _rewardIcon;
    [SerializeField] private TextMeshProUGUI _amountText;
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private Image _priceCurrencyIcon;
    [SerializeField] private TextMeshProUGUI _nextText;
    [SerializeField] private Button _button;

    [Header("State colors")]
    [SerializeField] private Color _currentFreeColor = new(0.08f, 0.72f, 0.95f, 1f);
    [SerializeField] private Color _currentPaidColor = new(0.54f, 0.16f, 0.86f, 1f);
    [SerializeField] private Color _lockedColor = new(0.14f, 0.25f, 0.38f, 0.92f);
    [SerializeField] private Color _freePriceColor = new(0.92f, 1f, 0.28f, 1f);
    [SerializeField] private Color _lockedTextColor = new(0.72f, 0.78f, 0.86f, 1f);

    public void Configure(
        ShopTrackStep step,
        bool current,
        Sprite rewardSprite,
        Sprite currencySprite,
        Action onClick)
    {
        if (_background != null)
            _background.color = current
                ? step.Free ? _currentFreeColor : _currentPaidColor
                : _lockedColor;

        if (_rewardIcon != null)
        {
            _rewardIcon.sprite = rewardSprite;
            _rewardIcon.enabled = rewardSprite != null;
            _rewardIcon.preserveAspect = true;
        }

        if (_amountText != null)
            _amountText.text = "x" + Mathf.Max(1, step.Reward.Amount);

        if (_priceText != null)
        {
            _priceText.text = step.Free
                ? LocalizationUtils.T("UI/Shop/Free", "FREE")
                : step.Price.ToString();
            _priceText.color = current
                ? step.Free ? _freePriceColor : Color.white
                : _lockedTextColor;
        }

        bool showCurrency = !step.Free && currencySprite != null;
        if (_priceCurrencyIcon != null)
        {
            _priceCurrencyIcon.sprite = currencySprite;
            _priceCurrencyIcon.gameObject.SetActive(showCurrency);
            _priceCurrencyIcon.preserveAspect = true;
        }

        if (_nextText != null)
        {
            _nextText.gameObject.SetActive(!current);
            _nextText.text = LocalizationUtils.T("UI/Shop/Next", "NEXT");
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.interactable = current;
            if (current && onClick != null)
                _button.onClick.AddListener(() => onClick());
        }
    }
}
