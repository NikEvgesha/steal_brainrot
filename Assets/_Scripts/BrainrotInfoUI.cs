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
        _income.text = string.Format("${0}/s", dinamicData.ResultIncome);
    }
    public void SetInfo(float income)
    {
        //_name.text = data.Name;
        _accumulationIncome.text = "$0";
        _income.text = string.Format("${0}/s", income);
    }


    public void UpdateIncome(float income)
    {
        _accumulationIncome.text = "$"+ income;
    }
    public void UpdateOfflineIncome(float income)
    {
        _offlineIncome.gameObject.SetActive(true);
        _offlineIncome.text = "ќфлайн инком = $" + income;
    }
}
