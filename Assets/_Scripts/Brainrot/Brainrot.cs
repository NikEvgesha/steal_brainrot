using System.Collections;
using UnityEngine;

public class Brainrot : MonoBehaviour
{
    [SerializeField] private float _speed;

    private BrainrotData _data;
    private Rarity _rarity;
    private BrainrotStatus _status;
    private BrainrotInfoUI _canvas;

    private bool _isMoving;
    private Transform _destinationPoint;

    private void Awake()
    {
        _canvas = GetComponentInChildren<BrainrotInfoUI>();
    }

    public void Init(BrainrotData data, Rarity rarity)
    {
        _data = data;
        _rarity = rarity;
        _status = BrainrotStatus.Conveyer;
        _canvas.SetInfo(data, rarity);

        //Set model from _data;
        //Set rarity material;
    }

    public void SetDestination(Transform destination)
    {
        _destinationPoint = destination;
        StopAllCoroutines();
        StartCoroutine(MoveToDestination());
    }

    public void SetStatus(BrainrotStatus status)
    {
        _status = status;
        switch (status)
        {
            case BrainrotStatus.Conveyer:
                break;
            case BrainrotStatus.MovingToBase:
                break;
            case BrainrotStatus.Base:
                break;
        }
    }

    private IEnumerator MoveToDestination()
    {
        while (transform.position != _destinationPoint.position)
        {
            transform.position = Vector3.MoveTowards(transform.position, _destinationPoint.position, _speed);
            yield return new WaitForFixedUpdate();
        }
        _destinationPoint = null;
        if (_status == BrainrotStatus.Conveyer)
        {
            Destroy(gameObject);
        }
    }
}
