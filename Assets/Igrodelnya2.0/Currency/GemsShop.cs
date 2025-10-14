using MirraGames.SDK;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GemsShop : MonoBehaviour
{
    [SerializeField] private List<CurrencyPackData> _items;
    [SerializeField] private GameObject _shopCanvas;
    [SerializeField] private DynamicGridSpawner _grid;
    [SerializeField] private GemsShopSlot _slotPrefab;
    [SerializeField] private GameObject _rewardCanvas;
    [SerializeField] private int _adReward;
    [SerializeField] private Text _rewardAmount;


    private Dictionary<PurchaseData, CurrencyPackData> _purchaseData;
    private bool _isOpen;
    private bool _inAppAvailable;
    //private bool _rewardEarned;
    public bool Opened => _isOpen;

    private static GemsShop _instance;
    public static GemsShop Instance => _instance;


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
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
        _purchaseData = new Dictionary<PurchaseData, CurrencyPackData>();
        _inAppAvailable = PurchasesManager.Instance.PurchasesAvailable();
        if (_inAppAvailable)
        {
            InitSlots();
            PurchasesManager.Instance.RestorePurchases();
        }
            
        G.Currency.NoGems += ToggleOpen;

   
        

        _rewardAmount.text = _adReward.ToString();
    }

    private void OnDisable()
    {
        G.Currency.NoGems -= ToggleOpen;
    }


    public void InitSlots()
    {
        foreach (CurrencyPackData item in _items)
        {
            GemsShopSlot slot = _grid.SpawnObject<GemsShopSlot>(_slotPrefab.gameObject);
            PurchaseData data = PurchasesManager.Instance.GetPurchaseData(item.CurrencyType.ToString() + "_" + item.Amount);
            _purchaseData.Add(data, item);
            slot.Init(item, data, this);
            //if (data.CurrencyImageURL != null && data.CurrencyImageURL != "")
            //    StartCoroutine(DownloadImage(data.CurrencyImageURL, slot));
        }
    }

    public void ToggleOpen()
    {
        _isOpen = !_isOpen;

        if (!_inAppAvailable)
        {
            _rewardCanvas.gameObject.SetActive(_isOpen);
        } else
        {
            _shopCanvas.gameObject.SetActive(_isOpen);
        }

            
        G.Control.CursorActive = _isOpen;
        if (_isOpen)
        {
            //_rewardEarned = false;
            G.Currency.ShowGems?.Invoke(true);
            G.Input.AOpenWindow?.Invoke(this);
        }
    }


    public void TryBuy(PurchaseData purchaseData, CurrencyPackData packData)
    {

        PauseManager.Instance.SetPause(true, true);
        PurchasesManager.Instance.BuyPurchase(
            purchaseData.Id,
            (success) =>
            {
                if (success)
                {
                    G.Currency.AddCurrency(packData.CurrencyType, packData.Amount);
                }
                PauseManager.Instance.SetPause(false, true);
            });

        
    }

    public void OnPurchaseRestore(string id)
    {
        foreach (PurchaseData purchase in _purchaseData.Keys)
        {
            if (purchase.Id == id)
            {
                G.Currency.AddCurrency(_purchaseData[purchase].CurrencyType, _purchaseData[purchase].Amount);
                break;
            }
        }
    }

    public void OnRewardButtonCLick()
    {
        AdsManager.Instance.ShowRewardedAd(
                "WatchAdToEarn",
                (success) =>
                {
                    if (success)
                    {
                        G.Currency.AddCurrency(CurrencyType.Gems, _adReward);
                        //_rewardEarned = true;
                    }
                    ToggleOpen();
                });
    }


/*    IEnumerator DownloadImage(string imageUrl, GemsShopSlot slot)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            slot.InitImage(sprite);
        }
        else
        {
            Debug.LogError("Ошибка загрузки: " + request.error);
        }
    }*/
}
