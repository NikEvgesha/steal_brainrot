using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Material _mt;
    [SerializeField] private float _speed;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        _mt.mainTextureOffset = new Vector2(0, Time.time * _speed * Time.fixedDeltaTime);
        Vector3 pos = _rb.position;
        _rb.position += Vector3.forward * _speed * Time.fixedDeltaTime;
        _rb.MovePosition(pos);
    }
}
