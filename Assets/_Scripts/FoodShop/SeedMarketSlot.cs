/*using UnityEngine;
using UnityEngine.UI;

public class SeedMarketSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _name;
    [SerializeField] private Text _amountText;
    [SerializeField] private Text _coinPrice;
    [SerializeField] private Text _gemPrice;
    [SerializeField] private Button _coinButton;

    private SeedData _seedData;
    private int _amount;
    private SeedMarket _market;

    public void Init(SeedMarket market, SeedData seedData)
    {
        _seedData = seedData;
        _market = market;

        _icon.sprite = _seedData.Icon;
        _name.text = _seedData.Plant.Name;
        _amountText.text = _seedData.MarketStack.ToString();
        _coinPrice.text = _seedData.CoinPrice.ToString();
        _gemPrice.text = _seedData.GemPrice.ToString();
    }

    public void SetAvailability(bool available)
    {
        _coinButton.interactable = available;
        // сделать слот серым если недоступен
    }

    public void SetAmount(int amount)
    {
        _amountText.text = amount.ToString();
    }

    public void OnGemsButtonClick()
    {
        _market.TryBuy(_seedData, true);
    }

    public void OnCoinsButtonClick()
    {
        _market.TryBuy(_seedData, false);
    }
}
*/