using UnityEngine;

public interface IConveyorPercentSource
{
    float GetPercentBonus(); // 0.15f = +15%
}

public sealed class ConveyorUpgradeBonusModifierMB : IncomeModifierBehaviour
{
    [SerializeField] private MonoBehaviour _source; // должен реализовать IConveyorPercentSource

    private IConveyorPercentSource _typedSource;

    public override string Id => "conveyor_upgrade_bonus";
    public override ModifierKind Kind => ModifierKind.PercentAdd;

    private void Awake()
    {
        _typedSource = _source as IConveyorPercentSource;
    }

    public override float Value
    {
        get
        {
            if (_typedSource == null) return 0f;
            return Mathf.Max(0f, _typedSource.GetPercentBonus());
        }
    }

    public override string Description => "Бонус зависит от уровня конвейера";

    public void NotifyConveyorChanged()
    {
        NotifyChanged();
    }
}
