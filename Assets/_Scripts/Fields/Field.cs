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

    public int ID => _id;
    
   

    public void Init()
    {
        if (_initialized) return;
        CaptureDefaultStateIfNeeded();
        G.QuickAccess.SwitchActiveItem.AddListener(CheckBuy);
        _touchHandler = GetComponentInChildren<BuyTouchHandler>();
        _cells = _cellsParent.GetComponentsInChildren<FieldCell>().ToList();
        _buyPanel.SetInfo("Разблокировать", _price.ToString());

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

        _playerOnField = true;
        CheckBuy(G.QuickAccess.CurrentActive);
        

    }

    public void _OnPlayerExit()
    {
        if (_remoteMode) return;
        if (_unblocked) return;

        _buyPanel.gameObject.SetActive(false);
        _playerOnField = false;
    }


    private void CheckBuy(InventoryItem currentActive)
    {
        if (_remoteMode) return;
        if (_unblocked || !_playerOnField) return;
        _buyPanel.gameObject.SetActive(currentActive != null && currentActive.Type == Item.Hamer);
    }

    public void _TryBuy()
    {
        if (_remoteMode) return;
        if (G.Currency.RemoveCurrency(CurrencyType.Coins, _price))
        {
            Unblock();
        }
    }

    public void Unblock()
    {
        ApplyUnblockedVisual(true);
        G.Save.SaveFieldUnblockStatus(_id, true);
    }


    public void SetID(int id)
    {
        _id = id;
    }


    public void LoadData()
    {
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
            _cells = _cellsParent.GetComponentsInChildren<FieldCell>(true).ToList();
        }
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
        if (_touchHandler == null)
            _touchHandler = GetComponentInChildren<BuyTouchHandler>(true);
        if (_touchHandler != null)
            _touchHandler.enabled = !_remoteMode;
        if (_remoteMode)
            _playerOnField = false;
    }

}
