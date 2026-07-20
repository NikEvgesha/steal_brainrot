using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BigPetSetSlot : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private Image _background;
    [SerializeField] private GameObject _activeIndicator;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private GameObject _selectedBadge;

    private BigPetSetUI _ui;
    private Brainrot _pet;
    private bool _active;
    private Button _button;

    public void Init(BigPetSetUI ui, Brainrot pet)
    {
        _ui = ui;
        _pet = pet;
        EnsureClickTarget();
        ConfigureVisuals();

        if (_ui != null)
            _ui.ActiveChanged.AddListener(CheckActiveSlot);
        if (_icon != null)
            _icon.sprite = pet != null ? pet.Icon : null;
        if (_background == null)
            Debug.LogWarning("[BigPetSetSlot] Background is not assigned.", this);

        RefreshLocalizedText();
    }

    public void OnClick()
    {
        if (_active || _ui == null || _pet == null) return;
        _ui.OnPetClicked(_pet);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnClick);
        if (_ui != null)
            _ui.ActiveChanged.RemoveListener(CheckActiveSlot);
    }

    private void CheckActiveSlot(Brainrot activePet)
    {
        _active = activePet != null && activePet == _pet;
        if (_activeIndicator != null)
            _activeIndicator.SetActive(_active);
        if (_selectedBadge != null)
            _selectedBadge.SetActive(_active);
    }

    public void RefreshLocalizedText()
    {
        if (_nameText == null || _pet == null)
            return;

        string fallback = !string.IsNullOrWhiteSpace(_pet.Name) ? _pet.Name.Trim() : _pet.name;
        _nameText.text = ItemDisplayNameResolver.ResolveItemName(_pet.Name, fallback);
    }

    private void ConfigureVisuals()
    {
        if (_background != null)
        {
            _background.color = _pet != null
                ? BlockyUITheme.GetRareColor(_pet.RareType)
                : BlockyUITheme.BrownBody;
            _background.type = _background.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            _background.pixelsPerUnitMultiplier = 1f;
        }

        if (_icon != null)
        {
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            SetRect(_icon.rectTransform, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.94f));
        }

        Transform gradient = transform.Find("Gradient");
        if (gradient != null)
        {
            var gradientImage = gradient.GetComponent<Image>();
            if (gradientImage != null)
            {
                gradientImage.color = new Color(1f, 1f, 1f, 0.22f);
                gradientImage.raycastTarget = false;
            }
        }

        ConfigureActiveFrame();
        EnsureNamePlate();
        EnsureSelectedBadge();
    }

    private void ConfigureActiveFrame()
    {
        if (_activeIndicator == null)
            return;

        _activeIndicator.transform.SetAsFirstSibling();
        var activeImage = _activeIndicator.GetComponent<Image>();
        if (activeImage != null)
        {
            activeImage.color = new Color(0.12f, 1f, 0.28f, 0.42f);
            activeImage.raycastTarget = false;
        }

        var outline = _activeIndicator.GetComponent<Outline>();
        if (outline == null)
            outline = _activeIndicator.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(5f, -5f);
        outline.useGraphicAlpha = true;
    }

    private void EnsureNamePlate()
    {
        Transform plate = transform.Find("NamePlate");
        if (plate == null)
        {
            var plateObject = new GameObject(
                "NamePlate",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            plateObject.transform.SetParent(transform, false);
            plate = plateObject.transform;

            var valueObject = new GameObject(
                "Value",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            valueObject.transform.SetParent(plate, false);
            _nameText = valueObject.GetComponent<TMP_Text>();
        }
        else if (_nameText == null)
        {
            _nameText = plate.GetComponentInChildren<TMP_Text>(true);
        }

        SetRect(plate as RectTransform, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.25f));
        plate.SetAsLastSibling();
        var plateImage = plate.GetComponent<Image>();
        if (plateImage != null)
        {
            plateImage.sprite = _background != null ? _background.sprite : null;
            plateImage.type = plateImage.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            plateImage.color = new Color(0.08f, 0.04f, 0.015f, 0.92f);
            plateImage.raycastTarget = false;
        }

        if (_nameText == null)
            return;

        SetRect(_nameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
        TmpUiTextFactory.ApplyDefaults(_nameText);
        _nameText.fontStyle = FontStyles.Bold;
        _nameText.fontSize = 28f;
        _nameText.enableAutoSizing = true;
        _nameText.fontSizeMin = 14f;
        _nameText.fontSizeMax = 30f;
        _nameText.alignment = TextAlignmentOptions.Center;
        _nameText.color = Color.white;
        _nameText.outlineColor = BlockyUITheme.BlackStroke;
        _nameText.outlineWidth = Mathf.Max(_nameText.outlineWidth, 0.16f);
        _nameText.raycastTarget = false;
    }

    private void EnsureSelectedBadge()
    {
        if (_selectedBadge == null)
            _selectedBadge = transform.Find("SelectedBadge")?.gameObject;

        if (_selectedBadge == null)
        {
            _selectedBadge = new GameObject(
                "SelectedBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _selectedBadge.transform.SetParent(transform, false);

            var checkObject = new GameObject(
                "Check",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            checkObject.transform.SetParent(_selectedBadge.transform, false);
            var checkText = checkObject.GetComponent<TMP_Text>();
            SetRect(checkText.rectTransform, Vector2.zero, Vector2.one);
            TmpUiTextFactory.ApplyDefaults(checkText);
            checkText.text = string.Empty;
            checkText.fontStyle = FontStyles.Bold;
            checkText.fontSize = 34f;
            checkText.enableAutoSizing = true;
            checkText.fontSizeMin = 18f;
            checkText.fontSizeMax = 38f;
            checkText.alignment = TextAlignmentOptions.Center;
            checkText.color = Color.white;
            checkText.outlineColor = BlockyUITheme.BlackStroke;
            checkText.outlineWidth = 0.18f;
            checkText.raycastTarget = false;
        }

        var legacyCheckText = _selectedBadge.GetComponentInChildren<TMP_Text>(true);
        if (legacyCheckText != null)
        {
            legacyCheckText.text = string.Empty;
            legacyCheckText.raycastTarget = false;
        }

        EnsureCheckBar("CheckShort", new Vector2(0.35f, 0.45f), new Vector2(18f, 6f), -42f);
        EnsureCheckBar("CheckLong", new Vector2(0.59f, 0.53f), new Vector2(28f, 6f), 47f);

        SetRect(
            _selectedBadge.transform as RectTransform,
            new Vector2(0.72f, 0.72f),
            new Vector2(0.96f, 0.96f));
        _selectedBadge.transform.SetAsLastSibling();
        var badgeImage = _selectedBadge.GetComponent<Image>();
        if (badgeImage != null)
        {
            badgeImage.sprite = _background != null ? _background.sprite : null;
            badgeImage.type = badgeImage.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            badgeImage.color = new Color(0.05f, 0.72f, 0.16f, 0.98f);
            badgeImage.raycastTarget = false;
        }
        _selectedBadge.SetActive(_active);
    }

    private void EnsureCheckBar(string name, Vector2 anchor, Vector2 size, float rotation)
    {
        if (_selectedBadge == null)
            return;

        Transform bar = _selectedBadge.transform.Find(name);
        if (bar == null)
        {
            var barObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            barObject.transform.SetParent(_selectedBadge.transform, false);
            bar = barObject.transform;
        }

        var rect = bar as RectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        bar.SetAsLastSibling();

        var image = bar.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2? offsetMin = null,
        Vector2? offsetMax = null)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        if (offsetMin.HasValue)
            rect.offsetMin = offsetMin.Value;
        if (offsetMax.HasValue)
            rect.offsetMax = offsetMax.Value;
    }

    private void EnsureClickTarget()
    {
        _button = GetComponent<Button>();
        if (_button == null)
            _button = gameObject.AddComponent<Button>();

        Graphic targetGraphic = _background != null ? _background : GetComponent<Graphic>();
        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
            _button.targetGraphic = targetGraphic;
        }

        _button.interactable = true;
        _button.transition = Selectable.Transition.ColorTint;
        var colors = _button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        _button.colors = colors;
        _button.navigation = new Navigation { mode = Navigation.Mode.None };
        _button.onClick.RemoveListener(OnClick);

        bool hasPersistentClick = false;
        for (int i = 0; i < _button.onClick.GetPersistentEventCount(); i++)
        {
            if (_button.onClick.GetPersistentTarget(i) == this &&
                _button.onClick.GetPersistentMethodName(i) == nameof(OnClick))
            {
                hasPersistentClick = true;
                break;
            }
        }

        if (!hasPersistentClick)
            _button.onClick.AddListener(OnClick);

        var graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            var graphic = graphics[i];
            if (graphic == null)
                continue;

            graphic.raycastTarget = graphic == targetGraphic;
        }
    }
}
