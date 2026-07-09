using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class EggDropCatalogUI : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private ItemPrefabStorage itemStorage;

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

    public bool IsOpen => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        AutoSetupReferences();
        Refresh();
        ToggleOpen(false);
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

        if (open && autoRefreshOnOpen)
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

    private static string L(string key, string fallback)
    {
        if (LocalizationManager.Instance != null && LocalizationManager.Instance.LocalizationData != null)
        {
            var translated = LocalizationManager.Instance.LocalizationData.GetTranslation(key);
            if (!string.IsNullOrWhiteSpace(translated) && !string.Equals(translated, key, StringComparison.Ordinal))
                return translated;
        }

        return fallback;
    }
}
