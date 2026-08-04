using UnityEngine;

public sealed class BigPetLevelIncomeModifierMB : IncomeModifierBehaviour
{
    private const int MaxBalancedLevel = 450;
    private const float MaxIncomeBonus = 50f;
    private const float BaselineMaxIncomeBonus = 1.75f;
    private const float LateGameIncomeBonus = MaxIncomeBonus - BaselineMaxIncomeBonus;

    public override string Id => "big_pet_level";
    public override ModifierKind Kind => ModifierKind.Multiplier;
    public override bool IsActive => base.IsActive && G.Save != null && G.Save.IsReady && G.Save.LoadBigPetStatus();
    public override float Value => 1f + GetIncomeBonus(GetLevel());
    public override float Progress01 => Mathf.Clamp01(GetLevel() / (float)MaxBalancedLevel);
    public override string Description => LocalizationUtils.Format(
        "UI/Income/BigPetLevelBonus",
        "Big animal level {0}: +{1}% farm income",
        GetLevel(),
        Mathf.RoundToInt((Value - 1f) * 100f));

    private void OnEnable()
    {
        BigPetPoint.LocalLevelChanged += OnLevelChanged;
    }

    private void OnDisable()
    {
        BigPetPoint.LocalLevelChanged -= OnLevelChanged;
    }

    private void OnLevelChanged(int _)
    {
        NotifyChanged();
    }

    private static int GetLevel()
    {
        return G.Save != null && G.Save.IsReady ? Mathf.Max(1, G.Save.LoadBigPetLvl()) : 1;
    }

    private static float GetIncomeBonus(int level)
    {
        int safeLevel = Mathf.Clamp(level, 1, MaxBalancedLevel);
        float baselineBonus = Mathf.Min(safeLevel, 50) * 0.01f;
        baselineBonus += Mathf.Min(Mathf.Max(0, safeLevel - 50), 100) * 0.005f;
        baselineBonus += Mathf.Min(Mathf.Max(0, safeLevel - 150), 150) * 0.003f;
        baselineBonus += Mathf.Min(Mathf.Max(0, safeLevel - 300), 150) * 0.002f;

        float levelProgress = safeLevel / (float)MaxBalancedLevel;
        float lateGameProgress = levelProgress * levelProgress * levelProgress * levelProgress;
        return Mathf.Min(MaxIncomeBonus, baselineBonus + LateGameIncomeBonus * lateGameProgress);
    }
}
