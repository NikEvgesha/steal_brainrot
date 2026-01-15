using UnityEngine;
using UnityEngine.UI;

public class FoodShopSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _name;
    [SerializeField] private Text _amountText;
    [SerializeField] private Text _coinPrice;
    [SerializeField] private Text _gemPrice;
    [SerializeField] private Button _coinButton;
    [SerializeField] private Button _gemButton;
    [SerializeField] private GameObject _unavailablePanel;

    private Food _food;
    private int _amount;
    private FoodShopUI _ui;

    public Food Food => _food;

    public void Init(FoodShopUI ui, Food food)
    {
        _food = food;
        _ui = ui;

        _icon.sprite = _food.Icon;
        _name.text = _food.Name;
        //_amountText.text = _food..ToString();
        _coinPrice.text = _food.Data.MoneyPrice.ToString();
        _gemPrice.text = _food.Data.GemPrice.ToString();
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
