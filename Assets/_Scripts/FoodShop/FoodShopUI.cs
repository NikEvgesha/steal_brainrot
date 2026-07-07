using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FoodShopUI : MonoBehaviour
{
    [SerializeField] private FoodShopSlot _slotPrefab;
    [SerializeField] private Transform _slotParent;
    [SerializeField] private Text _resupplyPriceText;

    private List<FoodShopSlot> _slots = new();

    [HideInInspector]
    public UnityEvent<Food, bool> BuyButtonClicked;
    [HideInInspector]
    public UnityEvent ResupplyButtonClicked;

    public void InitUI(List<Food> foodList, int resupplyPrice)
    {
        _resupplyPriceText.text = resupplyPrice.ToString();

        foreach (var food in foodList)
        {
            FoodShopSlot slot = Instantiate(_slotPrefab, _slotParent);
            slot.Init(this, food);
            _slots.Add(slot);
        }

        ApplyBlockyStyle();
    }


    public void UpdateUI(Dictionary<Food,int> stock)
    {
        foreach (FoodShopSlot slot in _slots)
        {
            if (stock.ContainsKey(slot.Food))
            {
                int amount = stock[slot.Food];
                slot.SetAmount(amount);
                slot.SetAvailability(amount > 0);
            }
        }
    }


    public void OnBuyButtonClicked(FoodShopSlot slot, bool forGems)
    {
        BuyButtonClicked?.Invoke(slot.Food, forGems);
    }


    public void OnResupplyButtonClicked()
    {
        ResupplyButtonClicked.Invoke();
    }

    private void ApplyBlockyStyle()
    {
        BlockyUITheme.StyleWindow(gameObject, BlockyUITheme.Tone.Orange);
    }
}
