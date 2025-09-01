using MirraGames.SDK;
using UnityEngine;

public class MirraSDKInitializeProvider: InitializeProvider
{
    public override void Initialize()
    {
        MirraSDK.WaitForProviders(() =>
        {
            Debug.Log("MirraSDK initialized");
            Initialized = true;
            InitializeManager.Instance.InitializeComplete?.Invoke();
        });
    }
}