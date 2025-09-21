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
    private void Start()
    {
        _touchHandler = GetComponentInChildren<BuyTouchHandler>();
        _cells = _cellsParent.GetComponentsInChildren<FieldCell>().ToList();
        _buyPanel.SetInfo("Разблокировать", _price.ToString());

        if (_unblocked)
        {
            Destroy(_grassObj);
        } else
        {
            foreach (FieldCell cell in _cells)
            {
                cell.gameObject.SetActive(false);
            }
        }
    }


    public void _OnPlayerEnter()
    {
        if (_unblocked) return;

        // TODO: check if hammer active
        _buyPanel.gameObject.SetActive(true);
        _playerOnField = true;

    }

    public void _OnPlayerExit()
    {
        if (_unblocked) return;

        _buyPanel.gameObject.SetActive(false);
        _playerOnField = false;
    }


    public void _TryBuy()
    {
        if (CurrencyManager.Instance.CheckEnoughCurrency(CurrencyType.Coins, _price))
        {
            CurrencyManager.Instance.RemoveCurrency(CurrencyType.Coins, _price);
            Destroy(_grassObj);
            Destroy(_buyPanel.gameObject);
            foreach (FieldCell cell in _cells)
            {
                cell.gameObject.SetActive(true);
            }
        } else
        {
            // Show currency shop
        }
    }

}
