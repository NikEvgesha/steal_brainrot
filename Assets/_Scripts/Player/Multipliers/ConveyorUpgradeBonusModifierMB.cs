using UnityEngine;

public interface IConveyorPercentSource
{
    float GetPercentBonus(); // 0.15f = +15%
}

public sealed class ConveyorUpgradeBonusModifierMB : IncomeModifierBehaviour
{
    [SerializeField] private MonoBehaviour _source;

    private IConveyorPercentSource _typedSource;

    public override string Id => "conveyor_upgrade_bonus";
    public override ModifierKind Kind => ModifierKind.PercentAdd;

    private void Awake()
    {
        ResolveSource();
    }

    public override float Value
    {
        get
        {
            if (_typedSource == null)
                ResolveSource();

            if (_typedSource == null) return 0f;
            return Mathf.Max(0f, _typedSource.GetPercentBonus());
        }
    }

    public override string Description => "Conveyor upgrade money bonus";

    public void NotifyConveyorChanged()
    {
        NotifyChanged();
    }

    private void ResolveSource()
    {
        _typedSource = _source as IConveyorPercentSource;
        if (_typedSource != null)
            return;

        var conveyors = FindObjectsByType<Conveyor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < conveyors.Length; i++)
        {
            if (conveyors[i] != null && !conveyors[i].IsRemoteMode)
            {
                _source = conveyors[i];
                _typedSource = conveyors[i];
                return;
            }
        }
    }
}
