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
        EnsureMentionBadgeLayout();
        EnsureButtonPointerPassThrough();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    public void Bind(string title, bool unlocked, bool selected, bool hasMention, Action onClick)
    {
        _onClick = onClick;

        if (button != null)
            button.interactable = true;

        if (titleText != null)
        {
            titleText.text = title;
            titleText.alpha = unlocked ? 1f : 0.9f;
        }

        if (lockOverlay != null)
        {
            // Rare tabs should stay visible/clickable even when this rare type
            // has not been discovered yet.
            lockOverlay.SetActive(false);
            if (lockOverlay.TryGetComponent<Graphic>(out var lockGraphic))
                lockGraphic.raycastTarget = false;
        }

        if (mentionBadge != null)
        {
            mentionBadge.SetActive(hasMention);
            if (mentionBadge.TryGetComponent<Graphic>(out var mentionGraphic))
                mentionGraphic.raycastTarget = false;
            if (hasMention)
                mentionBadge.transform.SetAsLastSibling();
        }
        EnsureMentionBadgeLayout();

        if (selectedFrame != null)
        {
            selectedFrame.enabled = selected;
            selectedFrame.raycastTarget = false;
        }

        EnsureButtonPointerPassThrough();
    }

    private void OnClicked()
    {
        _onClick?.Invoke();
    }

    private void EnsureButtonPointerPassThrough()
    {
        if (button == null)
            return;

        var target = button.targetGraphic;
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null || graphic == target)
                continue;
            graphic.raycastTarget = false;
        }
    }

    private void EnsureMentionBadgeLayout()
    {
        if (mentionBadge == null)
            return;

        var rt = mentionBadge.transform as RectTransform;
        if (rt == null)
            return;

        rt.anchorMin = Vector2.one;
        rt.anchorMax = Vector2.one;
        rt.pivot = Vector2.one;
        rt.anchoredPosition = new Vector2(-4f, -4f);

        var width = rt.sizeDelta.x;
        var height = rt.sizeDelta.y;
        if (width <= 0f || height <= 0f || width > 64f || height > 64f)
            rt.sizeDelta = new Vector2(18f, 18f);
    }
}
