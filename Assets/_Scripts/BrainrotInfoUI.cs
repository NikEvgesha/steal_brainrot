using UnityEngine;
using UnityEngine.UI;

public class BrainrotInfoUI : MonoBehaviour
{
    [SerializeField] private Text _name;
    [SerializeField] private Text _rarity;
    [SerializeField] private Text _income;
    [SerializeField] private Text _price;


    public void SetInfo(BrainrotData data, Rarity rarity)
    {
        _name.text = data.Name;
        _rarity.text = rarity.Name;
        _income.text = string.Format("{0}/s", data.Income * rarity.IncomeMultiplier);
        _price.text = data.BuyPrice.ToString();
    }
}
