using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class IncomeCollector : MonoBehaviour
{
    [SerializeField] private int _gemPrice;
    [SerializeField] private TMP_Text _priceText;

    [SerializeField] private GameObject _ui;
    private bool _collectorPurchased;
    private bool _purchaseInProgress;

    [HideInInspector] public UnityEvent CollectIncome;

    private void Start()
    {
        _priceText.text = _gemPrice.ToString();
    }

    public void _onPlayerEnter() {
        if (_collectorPurchased)
        {
            CollectIncome?.Invoke();
        }
        else
        {
            Open();
        }
    }

    public void _onPlayerExit() {
        if (!_collectorPurchased)
            Close();
    }

    public void _TryBuy() {
        if (G.Currency.RemoveCurrency(CurrencyType.Gems, _gemPrice)) {
            _collectorPurchased = true;
            Destroy(_ui);
        }
    }

    public void _ShowAd() {
        if (_purchaseInProgress || G.Ad == null) return;

        _purchaseInProgress = true;
        G.Ad.ShowRewardedAd("IncomeCollect", success =>
        {
            _purchaseInProgress = false;
            if (!success) return;
            CollectIncome?.Invoke();
        });
        return;
    }


    private void Open()
    {
        _ui.gameObject.SetActive(true);
    }


    private void Close()
    {
        _ui.gameObject.SetActive(false);
    }
}
