using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GiftChestUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GiftChestSlotView slotPrefab;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private Button closeButton;

    private readonly List<GiftChestSlotView> _slots = new();
    private Action<int> _onClaim;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        Close();
    }

    public void Open(ChestStateResponse state, Action<int> onClaim)
    {
        _onClaim = onClaim;
        BuildSlots(state);
        if (root != null) root.SetActive(true);
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void BuildSlots(ChestStateResponse state)
    {
        if (slotsParent == null || slotPrefab == null) return;

        for (int i = slotsParent.childCount - 1; i >= 0; i--)
            Destroy(slotsParent.GetChild(i).gameObject);

        _slots.Clear();

        var list = state?.slots ?? new List<ChestSlotDto>();
        bool hasItems = false;

        for (int i = 0; i < 6; i++)
        {
            ChestSlotDto slot = list.Find(s => s.slotIndex == i);
            bool hasItem = slot != null && !string.IsNullOrEmpty(slot.itemType);
            hasItems |= hasItem;

            Sprite icon = null;
            string itemName = "";
            if (hasItem && slot.itemData != null)
            {
                if (slot.itemType == "egg")
                {
                    var prefab = G.Storage != null ? G.Storage.GetEgg(slot.itemData.id) : null;
                    if (prefab != null)
                    {
                        icon = prefab.Icon;
                        itemName = prefab.Name;
                    }
                }
                else if (slot.itemType == "animal")
                {
                    var prefab = G.Storage != null ? G.Storage.GetPet(slot.itemData.id) : null;
                    if (prefab != null)
                    {
                        icon = prefab.Icon;
                        itemName = prefab.Name;
                    }
                }
            }

            var view = Instantiate(slotPrefab, slotsParent);
            view.Bind(i, icon, itemName, hasItem, _onClaim);
            _slots.Add(view);
        }

        if (emptyText != null) emptyText.gameObject.SetActive(!hasItems);
    }
}
