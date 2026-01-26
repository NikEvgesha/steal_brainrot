using UnityEngine;
using UnityEngine.UI;

public class BrainrotInfoUI : MonoBehaviour
{
    [SerializeField] private Text _income;
    [SerializeField] private Text _accumulationIncome;
    [SerializeField] private Text _offlineIncome;
    private void Start()
    {
        _offlineIncome.gameObject.SetActive(false);
    }

    public void SetInfo(BrainrotTypeData data, BrainrotDinamicData dinamicData)
    {
        //_name.text = data.Name;
        _accumulationIncome.text = "$0";
        _income.text = string.Format("${0}/s", G.Currency.ToString(dinamicData.ResultIncome));
    }
    public void SetInfo(double income)
    {
        //_name.text = data.Name;
        _accumulationIncome.text = "$0";
        _income.text = string.Format("${0}/s", G.Currency.ToString(income));
    }


    public void UpdateIncome(double income)
    {
        _accumulationIncome.text = "$"+ G.Currency.ToString(income);
    }
    public void UpdateOfflineIncome(double income)
    {
        _offlineIncome.gameObject.SetActive(true);
        _offlineIncome.text = "ќфлайн инком = $" + G.Currency.ToString(income);
    }
}
