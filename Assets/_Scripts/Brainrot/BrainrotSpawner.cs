using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public struct Rarity
{
    public ElementType Type;
    public float IncomeMultiplier;
    public float WeightMultiplier;
}

public class BrainrotSpawner : MonoBehaviour
{
    [SerializeField] private List<BrainrotData> _brainrots;
    [SerializeField] private List<Rarity> _rarityList;
    [SerializeField] private float _spawnInterval;
    [SerializeField] private float _legendarySpawnInterval;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private TriggerChecker _destroyArea;
    [SerializeField] private Brainrot _brainrotPrefab;

    private float _totalBrainrotWeight = 0;
    private float _totalRarityWeight = 0;

    private float _timeToLegendary;


    private void OnEnable()
    {
        _destroyArea.EnterArea += DestroyBrainrot;
    }

    private void OnDisable()
    {
        _destroyArea.EnterArea -= DestroyBrainrot;
    }

    private void Start()
    {
        _totalBrainrotWeight = _brainrots.Sum(item => item.MinWeight);
        //_totalRarityWeight = _rarityList.Sum(item => item.Weight);
        StartCoroutine(SpawnBrainrots());
    }

    private void DestroyBrainrot(Brainrot brainrot)
    {
        Destroy(brainrot.gameObject);
    }

    private IEnumerator SpawnBrainrots()
    {
        while (enabled)
        {

            Brainrot brainrot = Instantiate(_brainrotPrefab, _spawnPoint.position, Quaternion.identity, transform);
            //brainrot.Init(GetNextBrainrot(), GetNextRarity());
            //brainrot.SetDestination(_destroyArea.transform);
            yield return new WaitForSeconds(_spawnInterval);
        }
    }


    private BrainrotData GetNextBrainrot()
    {
        float rand = UnityEngine.Random.Range(0, _totalBrainrotWeight);
        float current = 0;
        BrainrotData res = _brainrots[0];

        foreach (BrainrotData data in _brainrots)
        {
            current += data.MinWeight;

            if (current >= rand)
            {
                res = data;
                break;
            }            
        }

        return res;
    }


    private Rarity GetNextRarity()
    {
        float rand = UnityEngine.Random.Range(0, _totalRarityWeight);
        float current = 0;
        Rarity res = _rarityList[0];

        foreach (Rarity rarity in _rarityList)
        {
            if (current >= rand)
            {
                res = rarity;
                break;
            }

            //current += rarity.Weight;
        }

        return res;
    }

}
