using UnityEngine;
using UnityEngine.UI;

public class RouletteSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Text _amount;


    public void Init(RouletteReward reward)
    {
        if (reward.rewardType == RouletteRewardType.Gems)
        {
            _icon.sprite = G.Currency.GetCurrencyIcon(CurrencyType.Gems);
            _amount.text = reward.amount.ToString();
        }
        else
        {
            _icon.sprite = reward.item.IMG;
            _amount.gameObject.SetActive(false);
        }


    }

}
