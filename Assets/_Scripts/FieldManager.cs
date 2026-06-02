using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    [SerializeField] private Transform _fieldParent;
    [SerializeField] private bool _useDistanceBasedUnlockPrice = true;
    [SerializeField] private float _baseUnlockPrice = 900f;
    [SerializeField] private float _distancePriceMultiplier = 2.9f;
    [SerializeField] private float _priceRoundStep = 100f;
    [SerializeField] private float _fallbackFieldStep = 4f;
    [SerializeField] private int _latePriceStartRing = 5;
    [SerializeField] private float _latePriceMultiplier = 60f;

    private List<List<Field>> _fields = new();
    private bool _initialized;

    public void Start()
    {
        InitFields();
    }

    public void EnsureInitialized()
    {
        if (_initialized) return;
        InitFields();
    }

    public void ReloadFromSave()
    {
        if (_fieldParent == null || G.Save == null)
            return;

        EnsureInitialized();
        ApplyDistanceBasedPrices();

        int fieldId = 0;
        for (int i = 0; i < _fieldParent.childCount; i++)
        {
            var row = _fieldParent.GetChild(i);
            var rowFields = row.GetComponentsInChildren<Field>(true).ToList();
            foreach (var field in rowFields)
            {
                if (field == null)
                {
                    fieldId++;
                    continue;
                }

                field.SetID(fieldId);
                field.SetRemoteMode(false);
                var isUnblocked = field.DefaultUnblocked || G.Save.LoadFieldUnblockStatus(fieldId);
                field.SetUnblockedVisual(isUnblocked);
                if (isUnblocked)
                    field.ReloadFromSaveState();
                else
                    field.ClearLoadedActors();
                fieldId++;
            }
        }
    }

    private void InitFields()
    {
        if (_initialized) return;

        BuildFieldsCache();
        ApplyDistanceBasedPrices();

        int fieldId = 0;
        for (int i = 0; i < _fields.Count; i++)
        {
            _fields[i].ForEach(x =>
            {
                if (x == null) { fieldId++; return; }
                x.SetID(fieldId);
                x.Init();
                var isUnblocked = x.DefaultUnblocked || G.Save.LoadFieldUnblockStatus(fieldId);
                x.SetUnblockedVisual(isUnblocked);
                if (isUnblocked)
                    x.LoadData();
                else
                    x.ClearLoadedActors();
                fieldId++;
            });
        }
        _initialized = true;
    }

    private void BuildFieldsCache()
    {
        _fields.Clear();
        int rowsCount = _fieldParent.childCount;
        for (int i = 0; i < rowsCount; i++)
        {
            Transform row = _fieldParent.GetChild(i);
            _fields.Add(row.GetComponentsInChildren<Field>(true).ToList());
        }
    }

    private void ApplyDistanceBasedPrices()
    {
        if (!_useDistanceBasedUnlockPrice || _fieldParent == null)
            return;

        var entries = GetFieldLayoutEntries();
        if (entries.Count == 0)
            return;

        Vector2 center = CalculateUnlockCenter(entries);
        float fieldStep = CalculateFieldStep(entries);
        foreach (var entry in entries)
        {
            if (entry.Field == null)
                continue;

            float price = entry.Field.DefaultUnblocked
                ? 0f
                : CalculateDistancePrice(entry.Position, center, fieldStep);
            entry.Field.SetUnlockPrice(price);
        }
    }

    private List<FieldLayoutEntry> GetFieldLayoutEntries()
    {
        if (_fields.Count == 0)
            BuildFieldsCache();

        var entries = new List<FieldLayoutEntry>();
        foreach (var row in _fields)
        {
            foreach (var field in row)
            {
                if (field == null)
                    continue;

                Vector3 localPosition = _fieldParent.InverseTransformPoint(field.transform.position);
                entries.Add(new FieldLayoutEntry(field, new Vector2(localPosition.x, localPosition.z)));
            }
        }

        return entries;
    }

    private Vector2 CalculateUnlockCenter(List<FieldLayoutEntry> entries)
    {
        var centerEntries = entries.Where(entry => entry.Field.DefaultUnblocked).ToList();
        if (centerEntries.Count == 0)
            centerEntries = entries;

        Vector2 sum = Vector2.zero;
        foreach (var entry in centerEntries)
            sum += entry.Position;

        return sum / centerEntries.Count;
    }

    private float CalculateFieldStep(List<FieldLayoutEntry> entries)
    {
        float minDistance = float.MaxValue;
        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                float distance = Vector2.Distance(entries[i].Position, entries[j].Position);
                if (distance > 0.01f && distance < minDistance)
                    minDistance = distance;
            }
        }

        if (minDistance == float.MaxValue)
            return Mathf.Max(0.01f, _fallbackFieldStep);

        return minDistance;
    }

    private float CalculateDistancePrice(Vector2 fieldPosition, Vector2 center, float fieldStep)
    {
        fieldStep = Mathf.Max(0.01f, fieldStep);
        float distance = Vector2.Distance(fieldPosition, center);
        int ring = Mathf.Max(1, Mathf.CeilToInt(distance / fieldStep));
        float multiplier = Mathf.Max(1f, _distancePriceMultiplier);
        float price = Mathf.Max(0f, _baseUnlockPrice) * Mathf.Pow(multiplier, ring - 1);
        if (ring >= _latePriceStartRing)
            price *= Mathf.Max(1f, _latePriceMultiplier);
        return RoundPrice(price);
    }

    private float RoundPrice(float price)
    {
        if (_priceRoundStep <= 0f)
            return Mathf.Round(price);

        return Mathf.Round(price / _priceRoundStep) * _priceRoundStep;
    }

    private readonly struct FieldLayoutEntry
    {
        public FieldLayoutEntry(Field field, Vector2 position)
        {
            Field = field;
            Position = position;
        }

        public readonly Field Field;
        public readonly Vector2 Position;
    }

}
