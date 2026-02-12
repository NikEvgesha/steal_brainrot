using System.Collections;
using UnityEngine;

public class TeleportPoint : MonoBehaviour
{
    [SerializeField] private ScenePoint _destination;
    public ScenePoint Destination => _destination;

    private void Start()
    {
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        while (G.Game.Teleporter == null)
            yield return null;

        G.Game.Teleporter.RegisterPoint(this);
    }
}
