using UnityEngine;
using UnityEngine.UI;

public class BrainrotInfoUI : MonoBehaviour
{
    [SerializeField] private Text _income;
    [SerializeField] private Text _accumulationIncome;
    [SerializeField] private Text _offlineIncome;


    public void SetInfo(BrainrotData data, Rarity rarity)
    {
        //_name.text = data.Name;
        _accumulationIncome.text = "$0";
        _income.text = string.Format("${0}/s", data.Income * rarity.IncomeMultiplier);
    }
    public void UpdateIncome(float income)
    {
        _accumulationIncome.text = "$"+ income;
    }
    public void UpdateOfflineIncome(float income)
    {
        _offlineIncome.text = "ќфлайн инком = $" + income;
    }
}
