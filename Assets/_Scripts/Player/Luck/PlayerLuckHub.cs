using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface INoElementLuckBonusSource
{
    float GetNoElementChanceBonus01();
}

public sealed class PlayerLuckHub : MonoBehaviour
{
    [SerializeField] private bool _useConveyorProgressBonus = true;
    [SerializeField] private bool _useUnlockedConveyorLevel = true;
    [SerializeField] private float _maxConveyorNoElementBonus = 0.009f;
    [SerializeField] private MonoBehaviour _conveyorSource;

    private readonly List<INoElementLuckBonusSource> _bonusSources = new();
    private Conveyor _conveyor;

    public event UnityAction Changed;

    public float NoElementChanceBonus01 => CalculateNoElementChanceBonus();
    public float ConveyorNoElementChanceBonus01 => CalculateConveyorBonus();
    public float BoosterNoElementChanceBonus01 => CalculateBonusSourceBonus();

    public static PlayerLuckHub EnsureExists()
    {
        if (G.Luck != null)
            return G.Luck;

        var existing = FindFirstObjectByType<PlayerLuckHub>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(PlayerLuckHub));
        return go.AddComponent<PlayerLuckHub>();
    }

    private void Awake()
    {
        if (G.Luck != null && G.Luck != this)
        {
            Destroy(gameObject);
            return;
        }

        G.Luck = this;
        ResolveConveyor();
    }

    private void OnDestroy()
    {
        if (G.Luck == this)
            G.Luck = null;
    }

    public void Register(INoElementLuckBonusSource source)
    {
        if (source == null) return;
        if (_bonusSources.Contains(source)) return;

        _bonusSources.Add(source);
        Changed?.Invoke();
    }

    public void Unregister(INoElementLuckBonusSource source)
    {
        if (source == null) return;
        if (!_bonusSources.Remove(source)) return;

        Changed?.Invoke();
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private float CalculateNoElementChanceBonus()
    {
        return Mathf.Clamp01(CalculateConveyorBonus() + CalculateBonusSourceBonus());
    }

    private float CalculateConveyorBonus()
    {
        if (!_useConveyorProgressBonus)
            return 0f;

        var conveyor = GetConveyor();
        if (conveyor == null)
            return 0f;

        float progress = _useUnlockedConveyorLevel
            ? conveyor.UnlockedLevelProgress01
            : conveyor.CurrentLevelProgress01;

        return Mathf.Clamp01(progress) * Mathf.Max(0f, _maxConveyorNoElementBonus);
    }

    private float CalculateBonusSourceBonus()
    {
        float bonus = 0f;

        for (int i = _bonusSources.Count - 1; i >= 0; i--)
        {
            var source = _bonusSources[i];
            if (source == null || source is Object unityObject && unityObject == null)
            {
                _bonusSources.RemoveAt(i);
                continue;
            }

            bonus += Mathf.Max(0f, source.GetNoElementChanceBonus01());
        }

        return bonus;
    }

    private Conveyor GetConveyor()
    {
        if (_conveyor == null)
            ResolveConveyor();

        return _conveyor;
    }

    private void ResolveConveyor()
    {
        _conveyor = _conveyorSource as Conveyor;
        if (_conveyor != null)
            return;

        var conveyors = FindObjectsByType<Conveyor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < conveyors.Length; i++)
        {
            if (conveyors[i] != null && !conveyors[i].IsRemoteMode)
            {
                _conveyor = conveyors[i];
                return;
            }
        }

        if (conveyors.Length > 0)
            _conveyor = conveyors[0];
    }
}
