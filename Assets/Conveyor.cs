using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Conveyor : MonoBehaviour
{
    [SerializeField] private float _spawnInterval;
    [SerializeField] private Material _mt;
    [SerializeField] private float _speed;
    [SerializeField] private float _matSpeedMultiplier;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _destroyPoint;
    [SerializeField] private LayerMask _egglayer;
    [SerializeField] private float _destroyDistance;

    // TODO: get egg from randomizer
    [SerializeField] private Egg _egg;

    private HashSet<Egg> _eggs;

    private void Start()
    {
        _eggs = new();
        StartCoroutine(Spawn());
    }

    private void FixedUpdate()
    {
        _mt.mainTextureOffset = new Vector2(0, Time.time * _speed * _matSpeedMultiplier * Time.fixedDeltaTime);


        Egg destroyEgg = null;
        
        foreach (Egg egg in _eggs)
        {
            if (Vector3.Distance(egg.transform.position, _destroyPoint.position) < _destroyDistance)
            {
                destroyEgg = egg;
            } else
            {
                egg.transform.position += egg.transform.forward * _speed * Time.fixedDeltaTime;
            }
                
        }

        if (destroyEgg)
        {
            _eggs.Remove(destroyEgg);
            Destroy(destroyEgg.gameObject);
        }
    }

    private IEnumerator Spawn()
    {
        while (enabled)
        {
            Egg egg = Instantiate(_egg, _spawnPoint.position, _spawnPoint.rotation);
            _eggs.Add(egg);
            egg.EggPurchased.AddListener(OnEggPurchase);
            yield return new WaitForSeconds(_spawnInterval);
        }
    }

    private void OnEggPurchase(Egg egg)
    {
        _eggs.Remove(egg);
    }

}
