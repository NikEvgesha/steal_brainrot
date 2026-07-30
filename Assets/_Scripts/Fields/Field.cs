using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Field : MonoBehaviour
{
    [SerializeField] private bool _remoteMode;
    [SerializeField] private float _price;
    [SerializeField] private GameObject _grassObj;
    [SerializeField] private Transform _cellsParent;
    [SerializeField] private bool _unblocked;
    [SerializeField] private InteractionPanel _buyPanel;

    private List<FieldCell> _cells;
    private bool _playerOnField;
    private BuyTouchHandler _touchHandler;
    private int _id;
    private bool _initialized;
    private bool _defaultCaptured;
    private bool _defaultUnblocked;
    private bool _quickAccessBound;

    public int ID => _id;
    public bool IsRemoteMode => _remoteMode;
    public bool IsUnblocked => _unblocked;
    public float UnlockPrice => Mathf.Max(0f, _price);
    public Transform BuyActionTarget => _buyPanel != null ? _buyPanel.transform : transform;
    
    private void Awake()
    {
        CaptureDefaultStateIfNeeded();
    }

    private void OnEnable()
    {
        if (_initialized)
            BindQuickAccess();
    }

    private void OnDisable()
    {
        UnbindQuickAccess();
    }
   

    public void Init()
    {
        if (_initialized) return;
        CaptureDefaultStateIfNeeded();
        BindQuickAccess();
        _touchHandler = GetComponentInChildren<BuyTouchHandler>(true);
        EnsureCells();
        if (_buyPanel != null)
            _buyPanel.SetInfoLocalized("UnlockLevel", "Unlock", _price.ToString());

        if (_unblocked)
        {
            ApplyUnblockedVisual(true);
        }
        else
        {
            ApplyUnblockedVisual(false);
        }
        _initialized = true;
    }

    public bool DefaultUnblocked
    {
        get
        {
            CaptureDefaultStateIfNeeded();
            return _defaultUnblocked;
        }
    }

    public void _OnPlayerEnter()
    {
        if (_remoteMode) return;
        if (_unblocked) return;

        BindQuickAccess();
        _playerOnField = true;
        CheckBuy(G.QuickAccess != null ? G.QuickAccess.CurrentActive : null);
        

    }

    public void _OnPlayerExit()
    {
        if (_remoteMode) return;
        if (_unblocked) return;

        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(false);
        _playerOnField = false;
    }


    private void CheckBuy(InventoryItem currentActive)
    {
        if (_remoteMode) return;
        if (_unblocked || !_playerOnField) return;
        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(currentActive != null && currentActive.Type == Item.Hamer);
    }

    public void _TryBuy()
    {
        if (_remoteMode) return;
        bool success = G.Currency.RemoveCurrency(CurrencyType.Coins, _price);
        if (success)
        {
            StartCoroutine(PlayUnlockAudio());
            Unblock();
        }

        GameAnalytics.Track(AnalyticsEventNames.TerritoryUnlockResult, GameAnalytics.Params(
            "field_id", _id,
            "currency_type", "coins",
            "price", _price,
            "source", "field_buy_panel",
            "result", success ? "success" : "failed",
            "failure_reason", success ? string.Empty : "insufficient_currency"));
    }

    public void Unblock()
    {
        ApplyUnblockedVisual(true);
        G.Save.SaveFieldUnblockStatus(_id, true);
        if (!_remoteMode)
            TutorialSignals.Raise(TutorialSignalType.FieldUnlocked, this);
    }

    private IEnumerator PlayUnlockAudio()
    {
        G.Sound?.PlayAt(GameAudioId.SFX_HAMMER_SWING, transform.position);
        yield return new WaitForSecondsRealtime(0.12f);
        G.Sound?.PlayAt(GameAudioId.SFX_HAMMER_IMPACT, transform.position);
        yield return new WaitForSecondsRealtime(0.06f);
        G.Sound?.PlayAt(GameAudioId.SFX_TERRITORY_UNLOCK, transform.position);
    }


    public void SetID(int id)
    {
        _id = id;
    }

    public void SetUnlockPrice(float price)
    {
        _price = Mathf.Max(0f, price);
        if (_buyPanel != null)
            _buyPanel.SetInfoLocalized("UnlockLevel", "Unlock", _price.ToString("0"));
    }


    public void LoadData()
    {
        EnsureCells();
        int id = 0;
        _cells.ForEach(cell => cell.SetLoadedData(SaveKey.Field.ToString() + _id + " " + id++));
    }

    public void ReloadFromSaveState()
    {
        EnsureCells();
        int id = 0;
        foreach (var cell in _cells)
        {
            if (cell == null)
            {
                id++;
                continue;
            }

            cell.ClearLoadedActors();
            cell.SetLoadedData(SaveKey.Field.ToString() + _id + " " + id++);
        }
    }

    public void ClearLoadedActors()
    {
        EnsureCells();
        foreach (var cell in _cells)
        {
            if (cell == null)
                continue;
            cell.ClearLoadedActors();
        }
    }

    public void AssignIdsForRemote(int id)
    {
        _id = id;
        EnsureCells();
        int cellId = 0;
        foreach (var cell in _cells)
        {
            cell.SetId(SaveKey.Field.ToString() + _id + " " + cellId++);
        }
    }

    public void SetUnblockedVisual(bool unblocked)
    {
        ApplyUnblockedVisual(unblocked);
    }

    private void EnsureCells()
    {
        if (_cells == null || _cells.Count == 0)
        {
            _cells = _cellsParent != null
                ? _cellsParent.GetComponentsInChildren<FieldCell>(true).ToList()
                : new List<FieldCell>();
        }
    }

    private void BindQuickAccess()
    {
        if (_quickAccessBound || G.QuickAccess == null)
            return;

        G.QuickAccess.SwitchActiveItem.AddListener(CheckBuy);
        _quickAccessBound = true;
    }

    private void UnbindQuickAccess()
    {
        if (!_quickAccessBound)
            return;

        if (G.QuickAccess != null)
            G.QuickAccess.SwitchActiveItem.RemoveListener(CheckBuy);
        _quickAccessBound = false;
    }

    private void CaptureDefaultStateIfNeeded()
    {
        if (_defaultCaptured)
            return;
        _defaultUnblocked = _unblocked;
        _defaultCaptured = true;
    }

    private void ApplyUnblockedVisual(bool unblocked)
    {
        EnsureCells();
        _unblocked = unblocked;

        if (_grassObj != null) _grassObj.SetActive(!unblocked);
        if (_buyPanel != null) _buyPanel.gameObject.SetActive(false);

        foreach (FieldCell cell in _cells)
        {
            cell.gameObject.SetActive(unblocked);
        }
    }

    public void SetRemoteMode(bool remote)
    {
        _remoteMode = remote;
        if (_buyPanel != null)
            _buyPanel.gameObject.SetActive(false);
        EnsureCells();
        foreach (var cell in _cells)
        {
            if (cell != null)
                cell.SetRemoteMode(remote);
        }
        if (_touchHandler == null)
            _touchHandler = GetComponentInChildren<BuyTouchHandler>(true);
        if (_touchHandler != null)
            _touchHandler.enabled = !_remoteMode;
        if (_remoteMode)
            _playerOnField = false;
    }

}
