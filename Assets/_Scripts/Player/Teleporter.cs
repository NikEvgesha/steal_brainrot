using System.Collections.Generic;
using UnityEngine;

public class Teleporter : MonoBehaviour
{
    private Dictionary<ScenePoint, Transform> _points = new();

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
        if (_points.ContainsKey(point.Destination))
        {
            Debug.Log("Trying to register existing teleport point");
            return;
        }

        _points.Add(point.Destination, point.transform);
    }

    private void Teleport(ScenePoint destination)
    {
        if (!_points.ContainsKey(destination)) return;


        G.Player.Teleport(_points[destination]);
    }
}
