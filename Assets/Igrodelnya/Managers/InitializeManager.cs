using System;
using UnityEngine;

public class InitializeManager : MonoBehaviour
{
    [SerializeField] InitializeProvider provider;

    public Action InitializeComplete;
    public bool Initialized => provider.Initialized;

    private static InitializeManager _instance;
    public static InitializeManager Instance => _instance;


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (provider)
                provider.Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}