using UnityEngine;
using UnityEngine.UI;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private Text _currencyAmount;
    [SerializeField] private CurrencyType _type;
    [SerializeField] private UIMoneyChangeAnimation _diffObj;

    private int _currentAmount = 0;

    private void Start()
    {
        CurrencyManager.Instance.CurrencyChanged += OnCurrencyChanged;
        OnCurrencyChanged(_type, CurrencyManager.Instance.GetBalance(_type));
    }

    private void OnEnable()
    {
        if (CurrencyManager.Instance)
        {
            CurrencyManager.Instance.CurrencyChanged += OnCurrencyChanged;

            OnCurrencyChanged(_type, CurrencyManager.Instance.GetBalance(_type));
        }
    }

    private void OnDisable()
    {
        CurrencyManager.Instance.CurrencyChanged -= OnCurrencyChanged;
    }


    private void OnCurrencyChanged(CurrencyType type, int newAmount)
    {
        if (type == _type)
        {
            int difference = newAmount - _currentAmount;
            if (difference != 0)
            {
                ShowDifference(difference);
            }

            _currentAmount = newAmount;
            _currencyAmount.text = newAmount.ToString();
        }
    }

    private void ShowDifference(int diff)
    {
        if (!gameObject.activeInHierarchy) return;
        UIMoneyChangeAnimation animation = Instantiate(_diffObj, transform);
        animation.Config(diff.ToString(), diff > 0);
    }
}
