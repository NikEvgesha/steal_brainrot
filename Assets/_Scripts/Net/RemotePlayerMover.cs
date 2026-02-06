using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerMover : MonoBehaviour
{
    [SerializeField] private float delaySec = 0.1f;

    private struct Sample
    {
        public float t;
        public Vector3 pos;
    }

    private readonly List<Sample> _samples = new();

    public void SetDelay(float delay)
    {
        delaySec = Mathf.Max(0f, delay);
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
            return;
        }

        if (_samples.Count >= 2)
        {
            var a = _samples[0];
            var b = _samples[1];
            var t = Mathf.InverseLerp(a.t, b.t, now);
            transform.position = Vector3.Lerp(a.pos, b.pos, t);
        }
    }
}
