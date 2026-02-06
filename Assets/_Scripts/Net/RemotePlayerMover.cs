using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerMover : MonoBehaviour
{
    [SerializeField] private float delaySec = 0.1f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotateLerp = 12f;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string holdingParam = "IsHolding";

    private struct Sample
    {
        public float t;
        public Vector3 pos;
    }

    private readonly List<Sample> _samples = new();
    private Vector3 _lastPos;
    private float _lastTime;
    private Animator _animator;
    private bool _isHolding;

    public void SetDelay(float delay)
    {
        delaySec = Mathf.Max(0f, delay);
    }

    public void SetHolding(bool holding)
    {
        _isHolding = holding;
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
            _animator.SetBool(holdingParam, _isHolding);
    }

    public void PushSamples(List<LobbyPosSampleDto> samples)
    {
        if (samples == null || samples.Count == 0) return;

        var baseTime = Time.time + delaySec;
        foreach (var s in samples)
        {
            _samples.Add(new Sample
            {
                t = baseTime + s.dt / 1000f,
                pos = new Vector3(s.x, s.y, s.z)
            });
        }
    }

    private void Update()
    {
        if (_samples.Count == 0) return;

        var now = Time.time;

        while (_samples.Count >= 2 && _samples[1].t <= now)
            _samples.RemoveAt(0);

        if (_samples.Count == 1)
        {
            transform.position = _samples[0].pos;
            UpdateAnimAndRotation(transform.position);
            return;
        }

        if (_samples.Count >= 2)
        {
            var a = _samples[0];
            var b = _samples[1];
            var t = Mathf.InverseLerp(a.t, b.t, now);
            transform.position = Vector3.Lerp(a.pos, b.pos, t);
            UpdateAnimAndRotation(transform.position);
        }
    }

    private void UpdateAnimAndRotation(Vector3 currentPos)
    {
        var now = Time.time;
        var dt = now - _lastTime;
        if (dt > 0.0001f)
        {
            var vel = (currentPos - _lastPos) / dt;
            var speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                var normalized = runSpeed > 0.001f ? Mathf.Clamp01(speed / runSpeed) : 0f;
                _animator.SetFloat(speedParam, normalized);
                _animator.SetBool(holdingParam, _isHolding);
            }

            if (speed > 0.05f)
            {
                var dir = new Vector3(vel.x, 0f, vel.z);
                var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotateLerp * Time.deltaTime);
            }
        }

        _lastPos = currentPos;
        _lastTime = now;
    }
}
