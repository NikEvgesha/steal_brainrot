using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AlbumEntryView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject mentionBadge;
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private Image selectedFrame;
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0f, 0f, 0f, 0.92f);
    [SerializeField] private bool enforcePreferredSize = true;
    [SerializeField] private Vector2 preferredSize = new Vector2(156f, 156f);
    [SerializeField] private bool applyRuntimeBlockyStyle = false;

    private Action _onClick;
    private int _lastClickFrame = -1;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button == null)
            button = GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
        }
        EnsureLayoutElement();
        EnsureMentionBadgeLayout();
        EnsureButtonPointerPassThrough();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    public void Bind(Sprite icon, string title, bool unlocked, bool selected, bool hasMention, Action onClick, RareType rareType = RareType.Common)
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
        {
            lockOverlay.SetActive(!unlocked);
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

        if (applyRuntimeBlockyStyle)
            BlockyUITheme.StyleCard(gameObject, rareType, selected);
        EnsureButtonPointerPassThrough();
    }

    private void OnClicked()
    {
        if (_lastClickFrame == Time.frameCount)
            return;

        _lastClickFrame = Time.frameCount;
        _onClick?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        // Fallback for prefab layouts where raycast hits child graphics
        // that are not in the Button component hierarchy.
        OnClicked();
    }

    private void EnsureButtonPointerPassThrough()
    {
        if (button == null)
            return;

        var target = button.targetGraphic;
        if (target == null)
        {
            target = GetComponentInChildren<Graphic>(true);
            if (target != null)
                button.targetGraphic = target;
        }
        var graphics = GetComponentsInChildren<Graphic>(true);
        for (var i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null)
                continue;

            if (target != null && graphic == target)
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

        if (rt.sizeDelta.x <= 0f || rt.sizeDelta.y <= 0f || rt.sizeDelta.x > 64f || rt.sizeDelta.y > 64f)
            rt.sizeDelta = new Vector2(18f, 18f);
    }

    private void EnsureLayoutElement()
    {
        if (!enforcePreferredSize)
            return;

        var layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        var width = Mathf.Max(1f, preferredSize.x);
        var height = Mathf.Max(1f, preferredSize.y);
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }
}
