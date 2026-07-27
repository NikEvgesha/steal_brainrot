using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationAudioEvents : MonoBehaviour
{
    private TPPlayerController _controller;

    private void Awake()
    {
        ResolveController();
    }

    public void Footstep()
    {
        if (_controller == null)
            ResolveController();

        _controller?.OnFootstepAnimationEvent();
    }

    private void ResolveController()
    {
        _controller = GetComponentInParent<TPPlayerController>();
    }
}
