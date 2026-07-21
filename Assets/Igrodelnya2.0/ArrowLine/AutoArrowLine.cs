using UnityEngine;

[RequireComponent(typeof(ArrowLine))]
public class AutoArrowLine : MonoBehaviour
{
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private bool _usePlayerAsStart = true;

    private ArrowLine _arrowLine;

    private void Awake()
    {
        _arrowLine = GetComponent<ArrowLine>();
    }

    private void Start()
    {
        TryStartArrowLine();
    }

    private void Update()
    {
        if (_usePlayerAsStart && !_startPoint && G.Player != null)
        {
            _startPoint = G.Player.transform;
            TryStartArrowLine();
        }

        if (!_endPoint)
            _arrowLine?.ActiveArrowLine(false);
    }

    public void SetTarget(Transform endPoint)
    {
        _usePlayerAsStart = true;
        _startPoint = G.Player != null ? G.Player.transform : null;
        _endPoint = endPoint;
        TryStartArrowLine();
    }

    public void SetTargets(Transform startPoint, Transform endPoint)
    {
        _usePlayerAsStart = false;
        _startPoint = startPoint;
        _endPoint = endPoint;
        TryStartArrowLine();
    }

    public void StopArrowLine()
    {
        _endPoint = null;
        _arrowLine?.ActiveArrowLine(false);
    }

    private void TryStartArrowLine()
    {
        if (_arrowLine == null)
            _arrowLine = GetComponent<ArrowLine>();

        if (_usePlayerAsStart && !_startPoint && G.Player != null)
            _startPoint = G.Player.transform;

        _arrowLine.StartArrowLine(_startPoint, _endPoint);
    }

    private void OnDisable()
    {
        _arrowLine?.ActiveArrowLine(false);
    }

    private void OnEnable()
    {
        if (_arrowLine != null && _endPoint)
            TryStartArrowLine();
    }
}
