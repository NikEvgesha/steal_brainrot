using UnityEngine;
using UnityEngine.Events;

public enum ModifierKind
{
    PercentAdd,
    Multiplier
}

public abstract class IncomeModifierBehaviour : MonoBehaviour
{
    [SerializeField] private string _title = "Modifier";

    public string Title => _title;
    public abstract string Id { get; }

    public virtual bool IsActive => enabled && gameObject.activeInHierarchy;

    /// PercentAdd: 0.10 = +10% | Multiplier: 1.10 = x1.10
    public abstract ModifierKind Kind { get; }
    public abstract float Value { get; }

    // UI
    public virtual string DisplayValue =>
        Kind == ModifierKind.PercentAdd ? $"+{Mathf.RoundToInt(Value * 100f)}%" : $"x{Value:0.##}";

    public virtual float Progress01 => 0f;
    public virtual string Description => "";

    public event UnityAction Changed;

    protected void NotifyChanged() => Changed?.Invoke();
}
