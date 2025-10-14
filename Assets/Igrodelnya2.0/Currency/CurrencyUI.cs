using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private Text _currencyAmount;
    [SerializeField] private CurrencyType _type;
    [SerializeField] private UIMoneyChangeAnimation _diffObj;

    private double _currentAmount = 0;

    private void Start()
    {
        G.Currency.CurrencyChanged += OnCurrencyChanged;
        OnCurrencyChanged(_type, G.Currency.GetBalance(_type));
    }

    private void OnEnable()
    {
        if (G.Currency)
        {
            G.Currency.CurrencyChanged += OnCurrencyChanged;

            OnCurrencyChanged(_type, G.Currency.GetBalance(_type));
        }
    }

    private void OnDisable()
    {
        G.Currency.CurrencyChanged -= OnCurrencyChanged;
    }


    private void OnCurrencyChanged(CurrencyType type, float newAmount)
    {
        if (type == _type)
        {
            double difference = newAmount - _currentAmount;
            if (difference != 0)
            {
                ShowDifference(difference);
            }

            _currentAmount = newAmount;
            _currencyAmount.text = newAmount.ToString();
        }
    }

    private void ShowDifference(double diff)
    {
        if (!gameObject.activeInHierarchy) return;
        UIMoneyChangeAnimation animation = Instantiate(_diffObj, transform);
        animation.Config(CurrencyConverter.convertNumToString(diff), diff > 0);
    }
}
