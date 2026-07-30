using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ElementMultiplier
{
    public ElementType Type;
    public float IncomeMultiplier;
    public float Weight;
}
public class ElementTypeMultiplaer : MonoBehaviour
{ 
    [SerializeField]private List<ElementMultiplier> _elementMultipliers = new List<ElementMultiplier>();

    // кэши
    private Dictionary<ElementType, float> _incomeByType;
    private readonly List<(ElementType type, float cum)> _cdf = new(); // cumulative weights
    private float _totalWeight;
    private float _noElementWeight;
    private float _elementOnlyWeight;

    private void Awake()
    {
        if (G.Elements == null)
        {
            G.Elements = this;
            BuildCaches();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void BuildCaches()
    {
        _incomeByType = new Dictionary<ElementType, float>(_elementMultipliers.Count);
        _cdf.Clear();
        _totalWeight = 0f;
        _noElementWeight = 0f;
        _elementOnlyWeight = 0f;

        foreach (var e in _elementMultipliers)
        {
            // кэш множителей дохода
            _incomeByType[e.Type] = e.IncomeMultiplier;

            // кэш CDF по положительным весам
            if (e.Weight > 0f)
            {
                _totalWeight += e.Weight;
                _cdf.Add((e.Type, _totalWeight));

                if (e.Type == ElementType.NoElement)
                    _noElementWeight += e.Weight;
                else
                    _elementOnlyWeight += e.Weight;
            }
        }

        // защита от пустого/нулевого набора весов
        if (_totalWeight <= 0f && _elementMultipliers.Count > 0)
        {
            // если весов нет — сделаем равновероятно
            float step = 1f / _elementMultipliers.Count;
            _cdf.Clear();
            _totalWeight = 1f;
            float acc = 0f;
            foreach (var e in _elementMultipliers)
            {
                acc += step;
                _cdf.Add((e.Type, acc));

                if (e.Type == ElementType.NoElement)
                    _noElementWeight += step;
                else
                    _elementOnlyWeight += step;
            }
        }
    }

    public float GetMultiplaer(ElementType type)
    {
        return _incomeByType != null && _incomeByType.TryGetValue(type, out var mult)
            ? mult
            : 1f; // дефолт
    }
    /// <summary>
    /// Случайный тип согласно весам из _elementMultipliers.
    /// </summary>
    public ElementType GetRandomWeighted()
    {
        float elementChanceBonus = G.Luck != null ? G.Luck.ElementChanceBonus01 : 0f;
        return GetRandomWeightedInternal(elementChanceBonus, 0f);
    }

    public ElementType GetRandomWeighted(float elementChanceBonus01)
    {
        return GetRandomWeightedInternal(elementChanceBonus01, 0f);
    }

    public ElementType GetRandomWeightedWithMinimumChance(float minimumElementChance01)
    {
        float elementChanceBonus = G.Luck != null ? G.Luck.ElementChanceBonus01 : 0f;
        return GetRandomWeightedInternal(
            elementChanceBonus,
            Mathf.Clamp01(minimumElementChance01));
    }

    private ElementType GetRandomWeightedInternal(
        float elementChanceBonus01,
        float minimumElementChance01)
    {
        if (_cdf.Count == 0) // нет данных
            return default;

        float permanentMultiplier = G.ShopEffects != null
            ? Mathf.Max(1f, G.ShopEffects.PermanentElementChanceMultiplier)
            : 1f;
        if (_noElementWeight <= 0f || _elementOnlyWeight <= 0f)
        {
            return GetRandomWeightedWithoutLuck();
        }

        float baseElementChance = _elementOnlyWeight / _totalWeight;
        float boostedElementChance = Mathf.Max(
            minimumElementChance01,
            Mathf.Clamp01(baseElementChance * permanentMultiplier + elementChanceBonus01));

        if (UnityEngine.Random.value > boostedElementChance)
            return ElementType.NoElement;

        float elementRoll = UnityEngine.Random.value * _elementOnlyWeight;
        float elementAcc = 0f;

        for (int i = 0; i < _elementMultipliers.Count; i++)
        {
            var element = _elementMultipliers[i];
            if (element.Type == ElementType.NoElement || element.Weight <= 0f)
                continue;

            elementAcc += element.Weight;
            if (elementRoll <= elementAcc)
                return element.Type;
        }

        return GetRandomWeightedWithoutLuck();
    }

    private ElementType GetRandomWeightedWithoutLuck()
    {
        if (_cdf.Count == 0) // нет данных
            return default;

        float r = UnityEngine.Random.value * _totalWeight;

        // линейный проход (для малых списков ок). Можно заменить на бинарный поиск.
        for (int i = 0; i < _cdf.Count; i++)
        {
            if (r <= _cdf[i].cum)
                return _cdf[i].type;
        }
        return _cdf[_cdf.Count - 1].type; // на всякий случай
    }
    /// <summary>
    /// Если нужно равномерно по enum (пропуская "ElementType/None" на 0).
    /// </summary>
    public static ElementType GetRandomUniformEnum()
    {
        var values = (ElementType[])Enum.GetValues(typeof(ElementType));
        int index = UnityEngine.Random.Range(1, values.Length);
        return values[index];
    }
}
