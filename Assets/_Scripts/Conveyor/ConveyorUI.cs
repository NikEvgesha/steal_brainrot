using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ConveyorUI : MonoBehaviour
{
    [SerializeField] private Text _levelName;
    [SerializeField] private Image _icon;
    [SerializeField] private Image _newEggIcon;
    [SerializeField] private Text _priceCoins;
    [SerializeField] private Text _priceGems;
    [SerializeField] private Text _incomeMultiplier;
    [SerializeField] private Button _buttonBuyGems;
    [SerializeField] private Button _buttonBuyCoins;
    [SerializeField] private Button _buttonActivate;
    [SerializeField] private GameObject _activeText;
    [SerializeField] private GameObject _notAvailableText;
    [SerializeField] private ConveyorLevelTab _tabPrefab;
    [SerializeField] private Transform _tansParent;

    private GameObject _panel;
    private bool _isOpen;
    private ConveyorLevel _currentLevelInfo;

    [HideInInspector]
    public UnityEvent<ConveyorLevel> LevelActivated = new();



    public void Init(List<ConveyorLevel> levels)
    {
        foreach (ConveyorLevel level in levels) {
            ConveyorLevelTab tab = Instantiate(_tabPrefab, _tansParent);
            tab.Init(level);
            tab.OnClick.AddListener(SetInfo);
        }
        //_currentLevelInfo = levels[0];
        SetInfo(levels[0]);
    }

    private void Awake()
    {
        _panel = transform.GetChild(0).gameObject;
    }

    public void ToggleOpen(bool open)
    {
        _isOpen = open;
        _panel.SetActive(open);
    }


    public void SetInfo(ConveyorLevel level)
    {
        if (_currentLevelInfo == level) return;

        _currentLevelInfo = level;
        _levelName.text = level.Name;
        _icon.sprite = level.Icon;
        _newEggIcon.sprite = level.NewEgg.Icon;
        _incomeMultiplier.text = "+" + ((level.IncomeMultiplier - 1) * 100).ToString() + "%";

        SetButtons();
    }

    private void SetButtons()
    {
        _activeText.gameObject.SetActive(_currentLevelInfo.IsActive);
        _notAvailableText.gameObject.SetActive(!_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsAvailable);
        _buttonActivate.gameObject.SetActive(_currentLevelInfo.IsPurchased && !_currentLevelInfo.IsActive);
        _buttonBuyCoins.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        _buttonBuyGems.gameObject.SetActive(!_currentLevelInfo.IsPurchased && _currentLevelInfo.IsAvailable);
        

        if (!_currentLevelInfo.IsPurchased)
        {
            _priceCoins.text = _currentLevelInfo.PriceCoin.ToString();
            _priceGems.text = _currentLevelInfo.PriceGems.ToString();
        }
    }


    public void _OnBuyCoinsClick()
    {
        _currentLevelInfo.TryBuy(forGems:false);

        if (_currentLevelInfo.IsPurchased)
        {
            SetButtons();
        }
    }

    public void _OnBuyGemsClick()
    {
        _currentLevelInfo.TryBuy(forGems: true);

        if (_currentLevelInfo.IsPurchased)
        {
            SetButtons();
        }
    }

    public void _OnBuyActivateClick()
    {
        LevelActivated?.Invoke(_currentLevelInfo);
        SetButtons();
    }


}
