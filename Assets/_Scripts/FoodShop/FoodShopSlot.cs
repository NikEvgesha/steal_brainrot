using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FoodShopSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _name;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private TMP_Text _coinPrice;
    [SerializeField] private TMP_Text _gemPrice;
    [SerializeField] private Button _coinButton;
    [SerializeField] private Button _gemButton;
    [SerializeField] private GameObject _unavailablePanel;

    private Food _food;
    private int _amount;
    private bool _available;
    private FoodShopUI _ui;
    private CurrencyManager _subscribedCurrencyManager;

    public Food Food => _food;
    public Transform CoinButtonTarget => _coinButton != null ? _coinButton.transform : transform;

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        SubscribeToCurrency();
        RefreshName();
        RefreshPurchaseButtons();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        UnsubscribeFromCurrency();
    }

    public void Init(FoodShopUI ui, Food food)
    {
        _food = food;
        _ui = ui;

        _icon.sprite = _food.Icon;
        RefreshName();
        //_amountText.text = _food..ToString();
        _coinPrice.text = G.Currency.ToString(_food.Data.MoneyPrice);
        _gemPrice.text = G.Currency.ToString(_food.Data.GemPrice);
        SubscribeToCurrency();
        RefreshPurchaseButtons();
    }

    private void OnLanguageChanged(string _)
    {
        RefreshName();
    }

    private void RefreshName()
    {
        if (_name == null || _food == null)
            return;

        _name.text = ItemDisplayNameResolver.ResolveItemName(_food.Name, _food.gameObject.name);
    }

    public void SetAvailability(bool available)
    {
        _available = available;
        _coinButton.gameObject.SetActive(available);
        _gemButton.gameObject.SetActive(available);
        _unavailablePanel.SetActive(!available);
        RefreshPurchaseButtons();
    }

    public void SetAmount(int amount)
    {
        _amount = amount;
        _amountText.text = "x" + amount.ToString();
        RefreshPurchaseButtons();
    }

    private void SubscribeToCurrency()
    {
        if (_subscribedCurrencyManager != null || G.Currency == null)
            return;

        _subscribedCurrencyManager = G.Currency;
        _subscribedCurrencyManager.CurrencyChanged?.AddListener(OnCurrencyChanged);
    }

    private void UnsubscribeFromCurrency()
    {
        if (_subscribedCurrencyManager == null)
            return;

        _subscribedCurrencyManager.CurrencyChanged?.RemoveListener(OnCurrencyChanged);
        _subscribedCurrencyManager = null;
    }

    private void OnCurrencyChanged(CurrencyType _, double __)
    {
        RefreshPurchaseButtons();
    }

    private void RefreshPurchaseButtons()
    {
        if (_food == null)
            return;

        SubscribeToCurrency();
        bool canPurchase = _available && _amount > 0 && G.Currency != null;
        BlockyUITheme.StylePurchaseButton(
            _coinButton,
            CurrencyType.Coins,
            canPurchase && G.Currency.Coins >= _food.Data.MoneyPrice);
        BlockyUITheme.StylePurchaseButton(
            _gemButton,
            CurrencyType.Gems,
            canPurchase && G.Currency.Gems >= _food.Data.GemPrice);
    }

    public void OnGemsButtonClick()
    {
        _ui.OnBuyButtonClicked(this, true);
    }

    public void OnCoinsButtonClick()
    {
        _ui.OnBuyButtonClicked(this, false);
    }
}
