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
    public static ElementTypeMultiplaer Init;
    [SerializeField]private List<ElementMultiplier> _elementMultipliers = new List<ElementMultiplier>();

    // кэши
    private Dictionary<ElementType, float> _incomeByType;
    private readonly List<(ElementType type, float cum)> _cdf = new(); // cumulative weights
    private float _totalWeight;

    private void Awake()
    {
        if (Init == null)
        {
            Init = this;
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

        foreach (var e in _elementMultipliers)
        {
            // кэш множителей дохода
            _incomeByType[e.Type] = e.IncomeMultiplier;

            // кэш CDF по положительным весам
            if (e.Weight > 0f)
            {
                _totalWeight += e.Weight;
                _cdf.Add((e.Type, _totalWeight));
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