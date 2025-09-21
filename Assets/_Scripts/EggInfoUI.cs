using UnityEngine;
using UnityEngine.UI;

public class EggInfoUI : MonoBehaviour
{
    [SerializeField] private Text _name;
    [SerializeField] private Text _rarity;
    [SerializeField] private Text _price;


    public void SetInfo(EggData data)
    {
        _name.text = data.Name;
        //_rarity.text = rarity.Name;
        _price.text = data.Price.ToString();
    }
}
