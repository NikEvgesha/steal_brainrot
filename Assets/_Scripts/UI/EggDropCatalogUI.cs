using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EggDropCatalogUI : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private ItemPrefabStorage itemStorage;
    [SerializeField] private Conveyor conveyor;

    [Header("Display")]
    [SerializeField] private bool autoRefreshOnOpen = true;
    [SerializeField] private bool applyLuckBonus = true;
    [SerializeField] private bool sortEggsByName = true;
    [SerializeField] private bool includeEggsWithoutBrainrots = true;
    [SerializeField] private bool showEggLuckInHeader = true;

    [Header("Localization")]
    [SerializeField] private string titleLocalizationKey = "UI/EggCatalog/Title";
    [SerializeField] private string titleTextFallback = "Egg Drop Chances";
    [SerializeField] private string emptyListLocalizationKey = "UI/EggCatalog/Empty";
    [SerializeField] private string emptyListFallback = "No eggs configured.";
    [SerializeField] private string eggWithoutPetsLocalizationKey = "UI/EggCatalog/NoPets";
    [SerializeField] private string eggWithoutPetsFallback = "No animals configured.";
    [SerializeField] private string luckLabelLocalizationKey = "UI/EggCatalog/LuckLabel";
    [SerializeField] private string luckLabelFallback = "Luck";
    [SerializeField] private string baseChanceNoteLocalizationKey = "UI/EggCatalog/BaseChanceNote";
    [SerializeField] private string baseChanceNoteFallback = "The chances below are shown without luck bonuses.";

    private const string GeneratedObjectPrefix = "ConveyorChance_";
    private RectTransform _cardsRoot;
    private LocalizationManager _subscribedLocalizationManager;

    public bool IsOpen => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        AutoSetupReferences();
        Refresh();
        ToggleOpen(false);
    }

    private void OnEnable()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationManager.OnInstanceReady += OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        LocalizationUtils.OnFallbackLanguageChanged += OnLanguageChanged;
        SubscribeToLocalizationManager(LocalizationManager.Instance);
    }

    private void OnDisable()
    {
        LocalizationManager.OnInstanceReady -= OnLocalizationManagerReady;
        LocalizationUtils.OnFallbackLanguageChanged -= OnLanguageChanged;
        UnsubscribeFromLocalizationManager();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        AutoSetupReferences();
    }

    [ContextMenu("EggCatalog/Refresh")]
    public void Refresh()
    {
        if (titleText != null)
            titleText.text = L(titleLocalizationKey, titleTextFallback);

        if (contentText == null)
            return;

        if (TryRefreshConveyorCards())
            return;

        ClearConveyorCards();
        contentText.gameObject.SetActive(true);

        var storage = itemStorage != null ? itemStorage : G.Storage;
        if (storage == null)
        {
            contentText.text = L(emptyListLocalizationKey, emptyListFallback);
            return;
        }

        var eggs = storage.GetAllEggPrefabs();
        if (eggs == null || eggs.Count == 0)
        {
            contentText.text = L(emptyListLocalizationKey, emptyListFallback);
            return;
        }

        var uniqueEggs = new List<Egg>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            if (egg == null)
                continue;

            var id = GetId(egg);
            if (!seen.Add(id))
                continue;

            var chances = ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonus);
            if (!includeEggsWithoutBrainrots && (chances == null || chances.Count == 0))
                continue;

            uniqueEggs.Add(egg);
        }

        if (uniqueEggs.Count == 0)
        {
            contentText.text = L(emptyListLocalizationKey, emptyListFallback);
            return;
        }

        if (sortEggsByName)
            uniqueEggs.Sort((a, b) => string.Compare(GetDisplayName(a), GetDisplayName(b), StringComparison.OrdinalIgnoreCase));

        var sb = new StringBuilder();
        for (var i = 0; i < uniqueEggs.Count; i++)
        {
            var egg = uniqueEggs[i];
            AppendEggSection(sb, egg);
            if (i < uniqueEggs.Count - 1)
                sb.Append('\n').Append('\n');
        }

        contentText.text = sb.ToString();
    }

    public void ToggleOpen(bool open)
    {
        AutoSetupReferences();
        if (panelRoot != null)
            panelRoot.SetActive(open);
        else
            gameObject.SetActive(open);

        if (open && (autoRefreshOnOpen || conveyor != null))
            Refresh();
    }

    public void Open()
    {
        ToggleOpen(true);
    }

    public void Close()
    {
        ToggleOpen(false);
    }

    private void AppendEggSection(StringBuilder sb, Egg egg)
    {
        if (egg == null)
            return;

        sb.Append(GetDisplayName(egg));
        if (showEggLuckInHeader)
        {
            var luckLabel = L(luckLabelLocalizationKey, luckLabelFallback);
            var sanitizedLuck = Mathf.Clamp(egg.Data.Luck, 1, 10);
            sb.Append(" [");
            sb.Append(luckLabel);
            sb.Append(": x");
            sb.Append(sanitizedLuck);
            sb.Append(']');
        }

        var chances = ConveyorDropChanceCalculator.BuildBrainrotChances(egg, applyLuckBonus);
        if (chances == null || chances.Count == 0)
        {
            sb.Append('\n');
            sb.Append("- ");
            sb.Append(L(eggWithoutPetsLocalizationKey, eggWithoutPetsFallback));
            return;
        }

        for (var i = 0; i < chances.Count; i++)
        {
            var row = chances[i];
            if (row == null)
                continue;

            sb.Append('\n');
            sb.Append("- ");
            sb.Append(row.name);
            sb.Append(": ");
            sb.Append((row.chance * 100d).ToString("0.00"));
            sb.Append('%');
        }
    }

    private string GetDisplayName(Egg egg)
    {
        if (egg == null)
            return string.Empty;

        var id = GetId(egg);
        var fallback = !string.IsNullOrWhiteSpace(egg.Name) ? egg.Name.Trim() : egg.name;
        return ItemDisplayNameResolver.ResolveItemName(id, fallback);
    }

    private bool TryRefreshConveyorCards()
    {
        if (conveyor == null || conveyor.Levels == null || conveyor.Levels.Count == 0 || contentText == null)
            return false;

        var contentRoot = contentText.transform.parent as RectTransform;
        if (contentRoot == null)
            return false;

        _cardsRoot = contentRoot;

        ConfigureViewportMask(contentRoot.parent);
        ConfigureCardsRoot(contentRoot);
        ClearConveyorCards();
        contentText.gameObject.SetActive(true);
        ConfigureCatalogText(contentText);
        contentText.text = BuildConveyorCatalogText();
        contentText.ForceMeshUpdate();

        var textLayout = contentText.GetComponent<LayoutElement>();
        if (textLayout == null)
            textLayout = contentText.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredHeight = Mathf.Max(120f, contentText.preferredHeight + 24f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        Canvas.ForceUpdateCanvases();
        return true;
    }

    private static void ConfigureCatalogText(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontSize = 29f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
    }

    private string BuildConveyorCatalogText()
    {
        var sb = new StringBuilder(2048);
        sb.Append("<align=center><color=#18E9FF><b>");
        sb.Append(EscapeRichText(L(baseChanceNoteLocalizationKey, baseChanceNoteFallback)));
        sb.Append("</b></color></align>\n<size=10>\n</size>");

        for (var levelIndex = 0; levelIndex < conveyor.Levels.Count; levelIndex++)
        {
            var level = conveyor.Levels[levelIndex];
            if (level == null)
                continue;

            var headerColor = BlockyUITheme.GetRareColor(level.RareType);
            var headerHex = ColorUtility.ToHtmlStringRGB(headerColor);
            sb.Append("<mark=#").Append(headerHex).Append("FF><color=#FFFFFF><size=34><b><space=12>");
            sb.Append(EscapeRichText(GetLocalizedLevelTitle(level)));
            sb.Append("<pos=98%><space=4></b></size></color></mark>\n");

            var chances = ConveyorDropChanceCalculator.BuildEggChances(level);
            if (chances == null || chances.Count == 0)
            {
                AppendChanceTextRow(sb, L(emptyListLocalizationKey, emptyListFallback), string.Empty, 0);
            }
            else
            {
                for (var rowIndex = 0; rowIndex < chances.Count; rowIndex++)
                {
                    var chance = chances[rowIndex];
                    if (chance == null)
                        continue;

                    AppendChanceTextRow(
                        sb,
                        L("Item/" + chance.id, chance.name),
                        (chance.chance * 100d).ToString("0.00") + "%",
                        rowIndex);
                }
            }

            if (levelIndex < conveyor.Levels.Count - 1)
                sb.Append("<size=11>\n</size>");
        }

        return sb.ToString();
    }

    private string GetLocalizedLevelTitle(ConveyorLevel level)
    {
        if (level == null)
            return L("UI/Conveyor/Title", "Conveyor");

        var fallback = !string.IsNullOrWhiteSpace(level.Name)
            ? level.Name.Trim()
            : level.RareType.ToString();
        var levelName = level.RareType == RareType.RareType
            ? fallback
            : L("Boost/RareType/" + level.RareType, fallback);
        return levelName + " " + L("UI/Conveyor/Title", "Conveyor");
    }

    private static void AppendChanceTextRow(StringBuilder sb, string eggName, string chance, int rowIndex)
    {
        var background = rowIndex % 2 == 0 ? "190A05F2" : "2D1208E8";
        sb.Append("<mark=#").Append(background).Append("><color=#FFFFFF><space=12>");
        sb.Append(EscapeRichText(string.IsNullOrWhiteSpace(eggName) ? "-" : eggName));
        sb.Append("<pos=82%>");
        sb.Append(EscapeRichText(chance));
        sb.Append("<pos=98%><space=4></color></mark>\n");
    }

    private static string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static void ConfigureCardsRoot(RectTransform root)
    {
        if (root == null)
            return;

        var layout = root.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = root.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(16, 16, 14, 18);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = root.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void ConfigureViewportMask(Transform viewport)
    {
        if (viewport == null)
            return;

        // The legacy UIMask sprite has transparent regions. A regular Mask uses
        // that alpha for the stencil and can cut the list into a narrow strip.
        var legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
            legacyMask.enabled = false;

        var maskGraphic = viewport.GetComponent<Image>();
        if (maskGraphic != null)
            maskGraphic.enabled = false;

        var scrollBackground = viewport.parent != null
            ? viewport.parent.GetComponent<Image>()
            : null;
        if (scrollBackground != null)
        {
            scrollBackground.sprite = null;
            scrollBackground.type = Image.Type.Simple;
            scrollBackground.color = new Color(0.105f, 0.045f, 0.018f, 0.97f);
        }

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();
    }

    private void ClearConveyorCards()
    {
        if (_cardsRoot == null && contentText != null)
            _cardsRoot = contentText.transform.parent as RectTransform;
        if (_cardsRoot == null)
            return;

        for (var i = _cardsRoot.childCount - 1; i >= 0; i--)
        {
            var child = _cardsRoot.GetChild(i);
            if (child == null || !child.name.StartsWith(GeneratedObjectPrefix, StringComparison.Ordinal))
                continue;

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static string GetId(Egg egg)
    {
        if (egg == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(egg.Name))
            return egg.Name.Trim();
        return egg.name != null ? egg.name.Trim() : string.Empty;
    }

    private void AutoSetupReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (itemStorage == null && G.Storage != null)
            itemStorage = G.Storage;

        if (conveyor == null)
            conveyor = GetComponentInParent<Conveyor>(true);

        if (titleText == null || contentText == null)
        {
            var allTexts = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allTexts.Length; i++)
            {
                var text = allTexts[i];
                if (text == null)
                    continue;

                var lowerName = text.name != null ? text.name.ToLowerInvariant() : string.Empty;
                if (titleText == null && lowerName.Contains("title"))
                    titleText = text;
                else if (contentText == null && (lowerName.Contains("content") || lowerName.Contains("list") || lowerName.Contains("body")))
                    contentText = text;
            }
        }
    }

    private void OnLocalizationManagerReady(LocalizationManager manager)
    {
        SubscribeToLocalizationManager(manager);
    }

    private void SubscribeToLocalizationManager(LocalizationManager manager)
    {
        if (manager == null || manager == _subscribedLocalizationManager)
            return;

        UnsubscribeFromLocalizationManager();
        _subscribedLocalizationManager = manager;
        _subscribedLocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeFromLocalizationManager()
    {
        if (_subscribedLocalizationManager == null)
            return;

        _subscribedLocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        _subscribedLocalizationManager = null;
    }

    private void OnLanguageChanged(string _)
    {
        if (IsOpen)
            Refresh();
    }

    private static string L(string key, string fallback)
    {
        return LocalizationUtils.T(key, fallback);
    }
}
