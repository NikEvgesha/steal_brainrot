using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms;
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
    private FoodShopUI _ui;

    public Food Food => _food;
    public Transform CoinButtonTarget => _coinButton != null ? _coinButton.transform : transform;

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        RefreshName();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
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
        _coinButton.gameObject.SetActive(available);
        _gemButton.gameObject.SetActive(available);
        _unavailablePanel.SetActive(!available);
    }

    public void SetAmount(int amount)
    {
        _amountText.text = "x" + amount.ToString();
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
