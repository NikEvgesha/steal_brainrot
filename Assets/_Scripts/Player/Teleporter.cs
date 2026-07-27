using System.Collections.Generic;
using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [SerializeField] private bool useLobbySlotTeleport = true;

    private readonly Dictionary<ScenePoint, List<Transform>> _points = new();
    private RemoteBasesApplier _remoteBases;

    private void Start()
    {
        G.Game.SetTeleporter(this);
        TeleportButton[] buttons = GetComponentsInChildren<TeleportButton>();
        foreach (TeleportButton button in buttons)
        {
            button.teleportButtonClicked.AddListener(Teleport);
        }

    }

    public void RegisterPoint(TeleportPoint point)
    {
        if (point == null) return;

        if (!_points.TryGetValue(point.Destination, out var list))
        {
            list = new List<Transform>();
            _points.Add(point.Destination, list);
        }

        if (!list.Contains(point.transform))
            list.Add(point.transform);
    }

    private void Teleport(ScenePoint destination)
    {
        var target = ResolveDestination(destination);
        if (target == null) return;

        if (G.Player != null)
            G.Sound?.PlayAt(GameAudioId.SFX_TELEPORT, G.Player.transform.position);
        G.Player.Teleport(target);
    }

    private Transform ResolveDestination(ScenePoint destination)
    {
        var remoteBases = GetRemoteBases();
        if (useLobbySlotTeleport && destination == ScenePoint.HOME && remoteBases != null)
        {
            var localEntry = remoteBases.GetLocalSlotEntryPoint();
            if (localEntry != null)
                return localEntry;
        }

        if (!_points.TryGetValue(destination, out var list) || list == null || list.Count == 0)
            return null;

        if (remoteBases != null)
        {
            var localRoot = remoteBases.GetLocalSlotRoot();
            if (localRoot != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t != null && t.IsChildOf(localRoot))
                        return t;
                }
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                return list[i];
        }

        return null;
    }

    private RemoteBasesApplier GetRemoteBases()
    {
        if (_remoteBases == null)
            _remoteBases = FindAnyObjectByType<RemoteBasesApplier>();
        return _remoteBases;
    }
}
