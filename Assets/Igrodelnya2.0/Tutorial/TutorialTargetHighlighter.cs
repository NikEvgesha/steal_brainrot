using System.Collections.Generic;
using UnityEngine;

public sealed class TutorialTargetHighlighter : MonoBehaviour
{
    private sealed class RendererState
    {
        public Renderer renderer;
        public MaterialPropertyBlock original;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly List<RendererState> _states = new();
    private Transform _target;

    public Transform Target => _target;

    public void SetTarget(Transform target)
    {
        if (_target == target)
            return;

        Restore();
        _target = target;
        if (_target == null)
            return;

        Renderer[] renderers = _target.GetComponentsInChildren<Renderer>(true);
        int count = Mathf.Min(renderers.Length, 16);
        for (int i = 0; i < count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            var original = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original);
            _states.Add(new RendererState { renderer = renderer, original = original });
        }
    }

    private void LateUpdate()
    {
        if (_target == null || _states.Count == 0)
            return;

        float pulse = 0.55f + Mathf.PingPong(Time.unscaledTime * 0.8f, 0.45f);
        Color tint = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.08f, 1f), pulse);
        Color emission = new Color(1f, 0.62f, 0.02f, 1f) * (1.2f + pulse);

        for (int i = 0; i < _states.Count; i++)
        {
            Renderer renderer = _states[i].renderer;
            if (renderer == null)
                continue;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            block.SetColor(ColorId, tint);
            block.SetColor(EmissionColorId, emission);
            renderer.SetPropertyBlock(block);
        }
    }

    public void ClearTarget()
    {
        Restore();
        _target = null;
    }

    private void OnDisable()
    {
        Restore();
    }

    private void OnDestroy()
    {
        Restore();
    }

    private void Restore()
    {
        for (int i = 0; i < _states.Count; i++)
        {
            RendererState state = _states[i];
            if (state.renderer != null)
                state.renderer.SetPropertyBlock(state.original);
        }

        _states.Clear();
    }
}
