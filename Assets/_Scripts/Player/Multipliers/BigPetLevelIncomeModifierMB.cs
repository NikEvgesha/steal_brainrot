using UnityEngine;

public sealed class BigPetLevelIncomeModifierMB : IncomeModifierBehaviour
{
    private const float BonusPerLevel = 0.10f;

    public override string Id => "big_pet_level";
    public override ModifierKind Kind => ModifierKind.Multiplier;
    public override bool IsActive => base.IsActive && G.Save != null && G.Save.IsReady && G.Save.LoadBigPetStatus();
    public override float Value => 1f + Mathf.Max(1, GetLevel()) * BonusPerLevel;
    public override float Progress01 => Mathf.Clamp01(GetLevel() / 25f);
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
}
