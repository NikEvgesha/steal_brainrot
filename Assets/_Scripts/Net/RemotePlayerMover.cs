using System.Collections.Generic;
using UnityEngine;

public class RemotePlayerMover : MonoBehaviour
{
    [SerializeField] private float delaySec = 0.1f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotateLerp = 12f;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string holdingParam = "IsHolding";
    [SerializeField] private string handPointName = "handPoint";
    [SerializeField] private string carryPointName = "GetItem";

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
    private Transform _handPoint;
    private Transform _carryPoint;
    private GameObject _heldVisual;
    private string _heldType;
    private string _heldId;
    private int? _heldElement;
    private float? _heldWeight;
    private double? _heldIncome;
    private int _lastSamplesHash;
    private int _lastSamplesCount;

    public void SetDelay(float delay)
    {
        delaySec = Mathf.Max(0f, delay);
    }

    public void ResetTransientState()
    {
        _samples.Clear();
        _head = 0;
        _lastSamplesHash = 0;
        _lastSamplesCount = 0;
        _lastTime = 0f;
        _lastPos = transform.position;
        SetHolding(false);
        ClearHeldVisual();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _animator.SetFloat(speedParam, 0f);
            _animator.SetBool(holdingParam, false);
        }
    }

    public void SetHolding(bool holding)
    {
        _isHolding = holding;
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
            _animator.SetBool(holdingParam, _isHolding);
    }

    public void SetHand(LobbyHandItemDto hand)
    {
        var type = hand != null ? (hand.type ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        var id = hand != null ? hand.id : null;
        var element = hand != null ? hand.element : null;
        var weight = hand != null ? hand.weight : null;
        var income = hand != null ? hand.income : null;

        var hasVisualItem = !string.IsNullOrEmpty(type) &&
                            !string.IsNullOrEmpty(id) &&
                            type != "hammer";
        SetHolding(hasVisualItem);

        if (!hasVisualItem)
        {
            ClearHeldVisual();
            return;
        }

        if (_heldVisual != null &&
            _heldType == type &&
            _heldId == id &&
            _heldElement == element &&
            NullableFloatEquals(_heldWeight, weight) &&
            NullableDoubleEquals(_heldIncome, income))
            return;

        ClearHeldVisual();
        var parent = ResolveHoldParent(type);
        if (parent == null)
            parent = transform;

        _heldVisual = BuildHandVisual(type, id, parent);
        if (_heldVisual == null)
            return;

        ApplyHandTraits(type, hand, _heldVisual);
        _heldType = type;
        _heldId = id;
        _heldElement = element;
        _heldWeight = weight;
        _heldIncome = income;
    }

    public void PushSamples(List<LobbyPosSampleDto> samples)
    {
        if (samples == null || samples.Count == 0) return;
        var currentHash = ComputeSamplesHash(samples);
        if (_lastSamplesCount == samples.Count && _lastSamplesHash == currentHash)
            return;
        _lastSamplesCount = samples.Count;
        _lastSamplesHash = currentHash;

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

    private static int ComputeSamplesHash(List<LobbyPosSampleDto> samples)
    {
        unchecked
        {
            var hash = 17;
            for (var i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                hash = hash * 31 + Mathf.RoundToInt(s.dt);
                hash = hash * 31 + Mathf.RoundToInt(s.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(s.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(s.z * 100f);
            }
            return hash;
        }
    }

    private Transform ResolveHoldParent(string type)
    {
        if (_carryPoint == null)
            _carryPoint = FindDeepChildByName(transform, carryPointName);
        if (_handPoint == null)
            _handPoint = FindDeepChildByName(transform, handPointName);

        if (type == "hammer" && _handPoint != null)
            return _handPoint;

        if (_carryPoint != null)
            return _carryPoint;
        if (_handPoint != null)
            return _handPoint;

        return transform;
    }

    private GameObject BuildHandVisual(string type, string id, Transform parent)
    {
        if (G.Storage == null || parent == null)
            return null;

        GameObject visual = null;
        switch (type)
        {
            case "egg":
            {
                var prefab = G.Storage.GetEgg(id);
                if (prefab != null)
                    visual = Instantiate(prefab.gameObject, parent, false);
                break;
            }
            case "brainrot":
            {
                var prefab = G.Storage.GetPet(id);
                if (prefab != null && prefab.Model != null)
                    visual = Instantiate(prefab.Model, parent, false);
                break;
            }
            case "food":
            {
                var prefab = G.Storage.GetFood(id);
                if (prefab != null)
                    visual = Instantiate(prefab.gameObject, parent, false);
                break;
            }
        }

        if (visual == null)
            return null;

        PrepareHandVisual(visual);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        return visual;
    }

    private void ApplyHandTraits(string type, LobbyHandItemDto hand, GameObject visual)
    {
        if (hand == null || visual == null)
            return;

        if ((type == "brainrot" || type == "egg") && hand.element.HasValue)
            ApplyElementColor(visual, hand.element.Value);

        if (type == "brainrot" && hand.weight.HasValue)
            ApplyWeightScale(visual, hand.weight.Value);
    }

    private void ApplyElementColor(GameObject visual, int elementRaw)
    {
        var color = GetElementColor(elementRaw);
        if (color.a <= 0f)
            return;

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var materialCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0;
            if (materialCount <= 0)
                materialCount = 1;

            for (int i = 0; i < materialCount; i++)
            {
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    private void ApplyWeightScale(GameObject visual, float weightMultiplier)
    {
        var m = Mathf.Max(1f, weightMultiplier);
        var scale = 1f + (m - 1f) * 0.25f;
        visual.transform.localScale = visual.transform.localScale * scale;
    }

    private Color GetElementColor(int elementRaw)
    {
        switch ((ElementType)elementRaw)
        {
            case ElementType.Gold:
                return Color.yellow;
            case ElementType.Diamond:
                return Color.blue;
            case ElementType.Electric:
                return Color.magenta;
            case ElementType.Fire:
                return Color.red;
            default:
                return new Color(0f, 0f, 0f, 0f);
        }
    }

    private void PrepareHandVisual(GameObject visual)
    {
        if (visual == null)
            return;

        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        foreach (var canvas in visual.GetComponentsInChildren<Canvas>(true))
            canvas.enabled = false;
        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
    }

    private void ClearHeldVisual()
    {
        if (_heldVisual != null)
            Destroy(_heldVisual);
        _heldVisual = null;
        _heldType = null;
        _heldId = null;
        _heldElement = null;
        _heldWeight = null;
        _heldIncome = null;
    }

    private static bool NullableFloatEquals(float? a, float? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return Mathf.Abs(a.Value - b.Value) <= 0.0001f;
    }

    private static bool NullableDoubleEquals(double? a, double? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;
        return System.Math.Abs(a.Value - b.Value) <= 0.0001d;
    }

    private static Transform FindDeepChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        var queue = new Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            if (t.name == name)
                return t;
            for (int i = 0; i < t.childCount; i++)
                queue.Enqueue(t.GetChild(i));
        }

        return null;
    }
}
