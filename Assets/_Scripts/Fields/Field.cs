using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Field : MonoBehaviour
{
    [SerializeField] private float _price;
    [SerializeField] private GameObject _grassObj;
    [SerializeField] private Transform _cellsParent;
    [SerializeField] private bool _unblocked;
    [SerializeField] private InteractionPanel _buyPanel;

    private List<FieldCell> _cells;
    private bool _playerOnField;
    private BuyTouchHandler _touchHandler;
    private int _id;

    public int ID => _id;
    
   
    private void Awake()
    {
        G.Initialized.AddListener(Init);     
    }

    private void Init()
    {
        G.QuickAccess.SwitchActiveItem.AddListener(CheckBuy);
        _touchHandler = GetComponentInChildren<BuyTouchHandler>();
        _cells = _cellsParent.GetComponentsInChildren<FieldCell>().ToList();
        _buyPanel.SetInfo("Разблокировать", _price.ToString());

        if (_unblocked)
        {
            Unblock();
        }
    }

    public void _OnPlayerEnter()
    {
        if (_unblocked) return;

        _playerOnField = true;
        CheckBuy(G.QuickAccess.CurrentActive);
        

    }

    public void _OnPlayerExit()
    {
        if (_unblocked) return;

        _buyPanel.gameObject.SetActive(false);
        _playerOnField = false;
    }


    private void CheckBuy(InventoryItem currentActive)
    {
        if (_unblocked || !_playerOnField) return;
        _buyPanel.gameObject.SetActive(currentActive != null && currentActive.Type == Item.Hamer);
    }

    public void _TryBuy()
    {
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

}
