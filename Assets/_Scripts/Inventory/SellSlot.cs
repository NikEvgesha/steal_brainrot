using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SellSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _price;
    [SerializeField] private Image _background;
    [SerializeField] private Button _sellButton;
    [SerializeField] private GameObject _lockSellIndicator;

    private InventoryItem _item;
    public InventoryItem Item => _item;

    public void Init(InventoryItem item)
    {
        _item = item;
        _icon.sprite = _item.Icon;
        _price.text = CurrencyText.Coins(G.Currency.ToString(_item.BaseSellPrice)); // TODO: use multipliers
        _sellButton.enabled = _item.SellAllowed;
        _lockSellIndicator.SetActive(!_item.SellAllowed);

        if (_background == null)
            Debug.LogWarning("[SellSlot] Background is not assigned.", this);
    }

    public void _OnSellButtonClick()
    {
        if (!_item.SellAllowed) return;
        string itemId = _item.Name;
        string itemType = _item.Type.ToString().ToLowerInvariant();
        string rarity = _item.RareType.ToString().ToLowerInvariant();
        double price = _item.BaseSellPrice;
        G.Sound?.Play(GameAudioId.SFX_ITEM_SELL);
        G.Currency.AddCurrency(CurrencyType.Coins, price);
        G.Inventory.Remove(_item);
        GameAnalytics.Track(AnalyticsEventNames.ItemSold, GameAnalytics.Params(
            "item_type", itemType,
            "item_id", itemId,
            "rarity", rarity,
            "currency_type", "coins",
            "price", price,
            "source", "sell_point",
            "result", "success"));
        Destroy(gameObject);
    }
}
