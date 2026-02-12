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

    private const int MaxBufferedSamples = 240;
    private const float KeepPastSeconds = 3f;

    private readonly List<Sample> _samples = new();
    private int _head;
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

        var now = Time.time;
        var lastDtMs = Mathf.Max(0f, samples[samples.Count - 1].dt);
        var baseTime = now + delaySec - lastDtMs * 0.001f;

        // Replace overlapping future path with fresher data.
        TrimFutureFrom(baseTime);

        var prevT = _samples.Count > _head ? _samples[_samples.Count - 1].t : float.NegativeInfinity;
        foreach (var s in samples)
        {
            var sampleTime = baseTime + Mathf.Max(0f, s.dt) * 0.001f;
            if (sampleTime <= prevT)
                sampleTime = prevT + 0.001f;

            _samples.Add(new Sample
            {
                t = sampleTime,
                pos = new Vector3(s.x, s.y, s.z)
            });
            prevT = sampleTime;
        }

        CompactSamples(now);
    }

    private void Update()
    {
        if (_head >= _samples.Count) return;

        var now = Time.time;

        while (_head + 1 < _samples.Count && _samples[_head + 1].t <= now)
            _head++;

        Vector3 pos;
        if (_head + 1 >= _samples.Count)
        {
            pos = _samples[_head].pos;
        }
        else
        {
            var a = _samples[_head];
            var b = _samples[_head + 1];
            var t = Mathf.InverseLerp(a.t, b.t, now);
            pos = Vector3.LerpUnclamped(a.pos, b.pos, t);
        }

        transform.position = pos;
        UpdateAnimAndRotation(pos);
        CompactSamples(now);
    }

    private void UpdateAnimAndRotation(Vector3 currentPos)
    {
        var now = Time.time;
        if (_lastTime <= 0f)
        {
            _lastPos = currentPos;
            _lastTime = now;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animator.SetFloat(speedParam, 0f);
                _animator.SetBool(holdingParam, _isHolding);
            }
            return;
        }

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

            if (speed > 0.1f)
            {
                var dir = new Vector3(vel.x, 0f, vel.z);
                var target = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotateLerp * Time.deltaTime);
            }
        }

        _lastPos = currentPos;
        _lastTime = now;
    }

    private void TrimFutureFrom(float fromTime)
    {
        if (_head >= _samples.Count)
        {
            _samples.Clear();
            _head = 0;
            return;
        }

        var cut = _samples.Count - 1;
        while (cut >= _head && _samples[cut].t >= fromTime)
            cut--;

        if (cut < _head)
        {
            _samples.Clear();
            _head = 0;
            return;
        }

        if (cut < _samples.Count - 1)
            _samples.RemoveRange(cut + 1, _samples.Count - (cut + 1));
    }

    private void CompactSamples(float now)
    {
        if (_samples.Count == 0)
        {
            _head = 0;
            return;
        }

        var minTime = now - KeepPastSeconds;
        while (_head + 1 < _samples.Count && _samples[_head + 1].t < minTime)
            _head++;

        if (_head > 64)
        {
            _samples.RemoveRange(0, _head);
            _head = 0;
        }

        var overflow = _samples.Count - MaxBufferedSamples;
        if (overflow > 0)
        {
            _samples.RemoveRange(0, overflow);
            _head = Mathf.Max(0, _head - overflow);
        }
    }
}
