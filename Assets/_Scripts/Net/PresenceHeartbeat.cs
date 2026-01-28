using System.Collections;
using UnityEngine;

public class PresenceHeartbeat : MonoBehaviour
{
    public FriendsApi api;
    public float intervalSec = 300f;

    private void Awake()
    {
        if (api == null) api = G.Backend.FriendsApi;
    }

    private void Start()
    {
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return api.EnsureGuest();
            yield return api.PresencePing();
            yield return new WaitForSeconds(intervalSec);
        }
    }
}
