using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GiftChestSlotView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button claimButton;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject filledState;

    private int slotIndex;

    public void Bind(int index, Sprite itemIcon, string itemName, bool hasItem, Action<int> onClaim)
    {
        slotIndex = index;
        if (emptyState != null) emptyState.SetActive(!hasItem);
        if (filledState != null) filledState.SetActive(hasItem);

        if (icon != null) icon.sprite = itemIcon;
        if (nameText != null) nameText.text = itemName ?? "";

        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.gameObject.SetActive(hasItem);
            if (hasItem)
                claimButton.onClick.AddListener(() => onClaim?.Invoke(slotIndex));
        }
    }
}
