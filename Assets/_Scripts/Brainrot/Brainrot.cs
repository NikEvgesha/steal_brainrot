using System.Collections;
using UnityEngine;

public class Brainrot : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] Transform _modelPoint;
    private BrainrotData _data;
    private Rarity _rarity;
    private BrainrotStatus _status;
    private BrainrotInfoUI _canvas;

    private bool _isMoving;
    private Transform _destinationPoint;
    private GameObject _model;
    private Animator _animatorModel;

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
        _model = Instantiate(_data.Model,_modelPoint);
        _animatorModel = _model.GetComponent<Animator>();
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
                _animatorModel.SetBool("Move", true);
                break;
            case BrainrotStatus.MovingToBase:
                _animatorModel.SetBool("Move", true);
                break;
            case BrainrotStatus.Base:
                _animatorModel.SetBool("Move", false);
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
