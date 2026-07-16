using System;

public static class ItemDisplayNameResolver
{
    private static readonly string[] EggNamesRu =
    {
        "\u041b\u0443\u0433\u043e\u0432\u043e\u0435",
        "\u041f\u044f\u0442\u043d\u0438\u0441\u0442\u043e\u0435",
        "\u0420\u0435\u0447\u043d\u043e\u0435",
        "\u041b\u0435\u0441\u043d\u043e\u0435",
        "\u041e\u0433\u043d\u0435\u043d\u043d\u043e\u0435",
        "\u0422\u0440\u043e\u043f\u0438\u0447\u0435\u0441\u043a\u043e\u0435",
        "\u0418\u0437\u0443\u043c\u0440\u0443\u0434\u043d\u043e\u0435",
        "\u041b\u0443\u043d\u043d\u043e\u0435",
        "\u0417\u0432\u0435\u0437\u0434\u043d\u043e\u0435",
        "\u0412\u0443\u043b\u043a\u0430\u043d\u0438\u0447\u0435\u0441\u043a\u043e\u0435",
        "\u0413\u0440\u043e\u0437\u043e\u0432\u043e\u0435",
        "\u0414\u0440\u0430\u043a\u043e\u043d\u044c\u0435"
    };

    private static readonly string[] EggNamesEn =
    {
        "Meadow",
        "Spotted",
        "River",
        "Forest",
        "Flame",
        "Tropical",
        "Emerald",
        "Moonlit",
        "Starlit",
        "Volcanic",
        "Storm",
        "Dragon"
    };

    public static string ResolveItemName(string id, string fallback = null)
    {
        var fallbackText = NormalizeUnityInstanceName(fallback);
        var normalizedId = NormalizeItemId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
            normalizedId = NormalizeItemId(fallbackText);

        if (string.IsNullOrWhiteSpace(normalizedId))
            return fallbackText ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fallbackText))
            fallbackText = normalizedId;

        var localizationKey = "Item/" + normalizedId;
        var localized = LocalizationUtils.T(localizationKey, null);
        if (IsResolved(localized, localizationKey, normalizedId))
            return localized.Trim();

        if (TryGetBuiltInEggName(normalizedId, out var builtInName))
            return builtInName;

        return fallbackText ?? normalizedId;
    }

    private static string NormalizeItemId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        var result = NormalizeUnityInstanceName(id);
        const string prefix = "Item/";
        if (result.StartsWith(prefix, StringComparison.Ordinal))
            result = result.Substring(prefix.Length);
        return result;
    }

    private static string NormalizeUnityInstanceName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = value.Trim();
        const string cloneSuffix = "(Clone)";
        if (result.EndsWith(cloneSuffix, StringComparison.Ordinal))
            result = result.Substring(0, result.Length - cloneSuffix.Length).TrimEnd();
        return result;
    }

    private static bool IsResolved(string value, string localizationKey, string rawId)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !string.Equals(value, localizationKey, StringComparison.Ordinal)
               && !string.Equals(value, rawId, StringComparison.Ordinal);
    }

    private static bool TryGetBuiltInEggName(string id, out string displayName)
    {
        displayName = null;
        if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("egg", StringComparison.OrdinalIgnoreCase))
            return false;

        var numberPart = id.Substring(3);
        if (!int.TryParse(numberPart, out var eggNumber))
            return false;

        var index = eggNumber - 1;
        if (index < 0 || index >= EggNamesRu.Length)
            return false;

        displayName = UseEnglishFallback() ? EggNamesEn[index] : EggNamesRu[index];
        return true;
    }

    private static bool UseEnglishFallback()
    {
        var manager = LocalizationManager.Instance;
        var language = manager != null ? manager.CurrentLanguage : null;
        return !string.IsNullOrWhiteSpace(language)
               && language.StartsWith("En", StringComparison.OrdinalIgnoreCase);
    }
}
