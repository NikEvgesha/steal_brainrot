using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlbumEntryView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject mentionBadge;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image selectedFrame;
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0f, 0f, 0f, 0.92f);

    private Action _onClick;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    public void Bind(Sprite icon, string title, bool unlocked, bool selected, bool hasMention, Action onClick)
    {
        _onClick = onClick;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.color = unlocked ? unlockedColor : lockedColor;
        }

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(title) ? "???" : title;

        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        if (mentionBadge != null)
            mentionBadge.SetActive(hasMention);

        if (selectedFrame != null)
            selectedFrame.enabled = selected;
    }

    private void OnClicked()
    {
        _onClick?.Invoke();
    }
}
