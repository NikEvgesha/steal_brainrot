using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialShopRewardSlot : MonoBehaviour
{
    [SerializeField] protected Image _icon;
    [SerializeField] protected TextMeshProUGUI _amount;

    public void SetReward(Sprite icon, int amount)
    {
        _icon.sprite = icon;
        _amount.text = "x" +  amount.ToString();
    }
}