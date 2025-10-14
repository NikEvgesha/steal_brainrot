using System.Collections.Generic;
using UnityEngine;

public class LoadingCollection : MonoBehaviour
{
    [SerializeField] private InitializeManager _initManager;
    [SerializeField] private List<GameObject> _activateObjects;

    private static LoadingCollection _instance;
    public static LoadingCollection Instance => _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            if (!_initManager.Initialized)
                _initManager.InitializeComplete += OnSDKInitialize;
            else
                OnSDKInitialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSDKInitialize()
    {
        foreach (var item in _activateObjects)
        {
            item.SetActive(true);
        }
    }
}
