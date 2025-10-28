using UnityEngine;
using UnityEngine.UI;

public class RouletteSlot : MonoBehaviour
{
    private Image _icon;
    private Text _amount;

    public void Init(RouletteReward reward)
    {
        _icon = GetComponent<Image>();
        _amount = GetComponentInChildren<Text>();
        if (reward.rewardType == RouletteRewardType.Gems)
        {
            _icon.sprite = G.Currency.GetCurrencyIcon(CurrencyType.Gems);
            _amount.text = reward.amount.ToString();
        }
        else
        {
            _icon.sprite = reward.item.Icon;
            _amount.gameObject.SetActive(false);
        }


    }

}
