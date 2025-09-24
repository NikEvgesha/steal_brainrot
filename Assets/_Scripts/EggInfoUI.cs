using UnityEngine;
using UnityEngine.UI;

public class EggInfoUI : MonoBehaviour
{
    [SerializeField] private Text _name;
    [SerializeField] private Text _luck; 
    [SerializeField] private Text _price;


    public void SetInfo(EggData data)
    {
        _name.text = data.Name;
        _luck.text = data.Luck+"X"+ " Удача" ;
        _price.text = (data.Price * ElementTypeMultiplaer.Init.GetMultiplaer(data.DinamicData.Type)).ToString();
    }
}
