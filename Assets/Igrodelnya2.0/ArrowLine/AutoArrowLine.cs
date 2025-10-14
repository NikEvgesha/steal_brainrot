using UnityEngine;
[RequireComponent(typeof(ArrowLine))]
public class AutoArrowLine : MonoBehaviour
{
    [SerializeField] private Transform _endPoint;
    private Transform _startPoint;
    private ArrowLine _arrowLine;
    private void Awake()
    {
        //_startPoint = Player.Instance.transform;
        _arrowLine = GetComponent<ArrowLine>();
        _endPoint = !_endPoint ? transform : _endPoint;
    }
    private void Start()
    {
        _arrowLine.StartArrowLine(_startPoint,_endPoint);
    }
}
