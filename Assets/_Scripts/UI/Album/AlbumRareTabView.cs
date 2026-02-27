using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlbumRareTabView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject mentionBadge;
    [SerializeField] private Image selectedFrame;

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

    public void Bind(string title, bool unlocked, bool selected, bool hasMention, Action onClick)
    {
        _onClick = onClick;

        if (titleText != null)
            titleText.text = title;

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
