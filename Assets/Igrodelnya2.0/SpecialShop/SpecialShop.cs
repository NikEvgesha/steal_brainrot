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
    private Dictionary<string, ShopPackData> _purchaseData;
    private List<ShopRow> _rows;
    private readonly List<string> _pendingRestoredPurchaseIds = new List<string>();
    private bool _isOpen;
    private bool _slotsInitialized;
    //private bool _inAppAvailable;
    public bool Opened => _isOpen;

    private void Awake()
    {
        _purchaseData = new Dictionary<string, ShopPackData>();
        _rows = new List<ShopRow>();

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
        //_inAppAvailable = true; //G.Purchases.PurchasesAvailable();
        InitSlots();
        G.Purchases?.RestorePurchases();
        G.Currency.NoGems.AddListener(ToggleOpen);
    }

    private void OnDisable()
    {
        G.Currency.NoGems.RemoveListener(ToggleOpen);
    }


    public void InitSlots()
    {
        _slotsInitialized = false;
        _purchaseData.Clear();

        foreach (ShopPackData item in _packs)
        {
            Transform row = GetOrCreateAvailableRow(item.SlotType==ShopSlotType.Big);
            SpecialShopSlot slot = Instantiate(_slotPrefab, row); // TODO
            string purchaseId = GetPurchaseId(item);
            PurchaseData data = G.Purchases != null ? G.Purchases.GetPurchaseData(purchaseId) : PurchaseData.Fallback(purchaseId);
            if (!_purchaseData.ContainsKey(data.Id))
                _purchaseData.Add(data.Id, item);
            slot.Init(item, data);
        }

        _slotsInitialized = true;
        FlushPendingRestores();
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
        if (!_slotsInitialized)
        {
            if (!string.IsNullOrWhiteSpace(id) && !_pendingRestoredPurchaseIds.Contains(id))
                _pendingRestoredPurchaseIds.Add(id);
            return;
        }

        GiveReward(id);
    }


    public void TryBuy(PurchaseData purchaseData, ShopPackData packData)
    {
        //GiveReward(purchaseData);
        if (G.Purchases == null || purchaseData == null || string.IsNullOrWhiteSpace(purchaseData.Id))
        {
            Debug.LogWarning("[SpecialShop] Cannot buy pack: purchases are not ready.");
            return;
        }

        G.IsPaused = true;
        G.Purchases.BuyPurchase(
            purchaseData.Id,
            (success) =>
            {
                if (success)
                {
                    GiveReward(purchaseData.Id);
                }
                G.IsPaused = false;
            });
    }

    private void GiveReward(string purchaseId)
    {
        if (string.IsNullOrWhiteSpace(purchaseId) || !_purchaseData.TryGetValue(purchaseId, out var packData) || packData == null)
            return;

        foreach (ShopReward reward in packData.Rewards)
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

    private static string GetPurchaseId(ShopPackData pack)
    {
        return "Pack_" + (pack != null ? pack.Id : "");
    }

    private void FlushPendingRestores()
    {
        if (_pendingRestoredPurchaseIds.Count == 0)
            return;

        for (int i = 0; i < _pendingRestoredPurchaseIds.Count; i++)
            GiveReward(_pendingRestoredPurchaseIds[i]);

        _pendingRestoredPurchaseIds.Clear();
    }
}
