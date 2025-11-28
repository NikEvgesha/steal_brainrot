using MirraGames.SDK.Common;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShop : MonoBehaviour
{
    [SerializeField] private List<ShopPackData> _packs;
    [SerializeField] private GameObject _shopCanvas;
    [SerializeField] private Transform _content;
    [SerializeField] private SpecialShopSlot _slotPrefab;
    [SerializeField] private ShopRow _rowPrefab;
    [SerializeField] private int _maxItemsPerRow = 3;
    private Dictionary<PurchaseData, ShopPackData> _purchaseData;
    private List<ShopRow> _rows;
    private bool _isOpen;
    private bool _inAppAvailable;
    public bool Opened => _isOpen;

    private void Awake()
    {
        if (G.SpecialShop == null)
        {
            G.SpecialShop = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("GemsShop уже существует! Удаляем дубликат.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        _purchaseData = new Dictionary<PurchaseData, ShopPackData>();
        _inAppAvailable = true; //G.Purchases.PurchasesAvailable();
        _rows = new List<ShopRow>();
        InitSlots();
        G.Purchases.RestorePurchases();
        G.Currency.NoGems.AddListener(ToggleOpen);
    }

    private void OnDisable()
    {
        G.Currency.NoGems.RemoveListener(ToggleOpen);
    }


    public void InitSlots()
    {
        foreach (ShopPackData item in _packs)
        {
            Transform row = GetOrCreateAvailableRow(item.SlotType==ShopSlotType.Big);
            SpecialShopSlot slot = Instantiate(_slotPrefab, row); // TODO
            PurchaseData data = G.Purchases.GetPurchaseData("Pack_" + item.Id);
            _purchaseData.Add(data, item);
            slot.Init(item, data);
        }
    }

    private Transform GetOrCreateAvailableRow(bool big)
    {
        if (!big)
        {
            // Проверяем существующие строки
            foreach (ShopRow row in _rows)
            {
                if (row.ItemsCount < row.MaxItems)
                {
                    return row.transform;
                }
            }
        }
        // Если нет свободной строки, создаём новую
        ShopRow newRow = Instantiate(_rowPrefab, _content);
        newRow.setMaxItems(big ? 1 : _maxItemsPerRow);
        _rows.Add(newRow);
        return newRow.transform;
    }

    public void ToggleOpen()
    {
        _isOpen = !_isOpen;
        _shopCanvas.gameObject.SetActive(_isOpen);
        G.Control.CursorActive = _isOpen;
        if (_isOpen)
        {
            //_rewardEarned = false;
            G.Currency.ShowGems?.Invoke(true);
            G.Input.AOpenWindow?.Invoke(this);
        }


    }


    public void OnPurchaseRestore(string id)
    {
        foreach (PurchaseData purchase in _purchaseData.Keys)
        {
            if (purchase.Id == id)
            {
                GiveReward(purchase);
                break;
            }
        }
    }


    public void TryBuy(PurchaseData purchaseData, ShopPackData packData)
    {
        //GiveReward(purchaseData);

        G.IsPaused = true;
        G.Purchases.BuyPurchase(
            purchaseData.Id,
            (success) =>
            {
                if (success)
                {
                    GiveReward(purchaseData);
                }
                G.IsPaused = false;
            });
    }

    private void GiveReward(PurchaseData purchaseData)
    {
        ShopPackData packData = null;
        _purchaseData.TryGetValue(purchaseData, out packData);
        if (packData == null) return;

        foreach (ShopReward reward in _purchaseData[purchaseData].Rewards)
        {
            if (reward.Type == ShopRewardType.Item)
            {
                for (int i = 0; i < reward.Amount; i++)
                {
                    InventoryItem item = Instantiate(reward.Item);
                    G.Inventory.Add(item);
                }
            }
            else
            {
                G.Currency.AddCurrency(reward.RewardCurrencyType, reward.Amount);
            }
        }
    }
}