using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlbumRareTabView : MonoBehaviour
{
    [Serializable]
    private struct ElementIconBinding
    {
        public ElementType type;
        public Sprite sprite;
    }

    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;
    [SerializeField] private List<ElementIconBinding> elementIcons = new();
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject mentionBadge;
    [SerializeField] private Image selectedFrame;
    [SerializeField] private bool tintRuntimeButton = false;
    [SerializeField] private bool styleRuntimeTextState = false;
    [SerializeField] private bool applyRuntimeBlockyStyle = false;

    private Action _onClick;
    private FontStyles _defaultTitleStyle = FontStyles.Normal;
    private bool _hasTitleStyleCache;
    private Color _baseButtonColor = Color.white;
    private bool _hasBaseButtonColor;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClicked);
            if (button.targetGraphic != null)
            {
                _baseButtonColor = button.targetGraphic.color;
                _hasBaseButtonColor = true;
            }
        }

        if (titleText != null)
        {
            _defaultTitleStyle = titleText.fontStyle;
            _hasTitleStyleCache = true;
        }

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
        Bind(ElementType.ElementType, title, unlocked, selected, hasMention, onClick);
    }

    public void Bind(ElementType elementType, string title, bool unlocked, bool selected, bool hasMention, Action onClick)
    {
        _onClick = onClick;

        if (button != null)
            button.interactable = true;

        if (tintRuntimeButton && button != null && button.targetGraphic != null)
        {
            if (!_hasBaseButtonColor)
            {
                _baseButtonColor = button.targetGraphic.color;
                _hasBaseButtonColor = true;
            }

            var targetColor = _baseButtonColor;
            if (!unlocked)
                targetColor = Color.Lerp(targetColor, Color.black, 0.2f);
            if (selected)
                targetColor = Color.Lerp(targetColor, Color.white, 0.35f);
            targetColor.a = _baseButtonColor.a;
            button.targetGraphic.color = targetColor;
        }

        if (titleText != null)
        {
            if (!_hasTitleStyleCache)
            {
                _defaultTitleStyle = titleText.fontStyle;
                _hasTitleStyleCache = true;
            }

            titleText.text = title;
            titleText.gameObject.SetActive(!ApplyIcon(elementType, unlocked));
            if (styleRuntimeTextState)
            {
                titleText.alpha = unlocked ? 1f : 0.9f;
                titleText.fontStyle = selected ? (_defaultTitleStyle | FontStyles.Bold) : _defaultTitleStyle;
            }
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
        }
        EnsureMentionBadgeLayout();

        if (selectedFrame != null)
        {
            selectedFrame.enabled = selected;
            selectedFrame.raycastTarget = false;
        }

        if (applyRuntimeBlockyStyle && button != null)
            BlockyUITheme.ApplyButton(button, selected ? BlockyUITheme.YellowAccent : BlockyUITheme.BlueHeader);

        EnsureButtonPointerPassThrough();
    }

    private bool ApplyIcon(ElementType elementType, bool unlocked)
    {
        if (iconImage == null)
            return false;

        Sprite icon = GetIcon(elementType);
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        iconImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        return icon != null;
    }

    private Sprite GetIcon(ElementType elementType)
    {
        if (elementIcons == null)
            return null;

        for (var i = 0; i < elementIcons.Count; i++)
        {
            if (elementIcons[i].type == elementType)
                return elementIcons[i].sprite;
        }

        return null;
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
