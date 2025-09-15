using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerBase : MonoBehaviour
{
    [SerializeField] private Transform _platformsParent;
    [SerializeField] private TriggerChecker _entryArea;

    private BaseOwner _owner;
    private List<BrainrotPlatform> _platforms;

    public Transform EntryPoint { get { return _entryArea.transform; } }


    private void OnEnable()
    {
        _entryArea.EnterArea += onBrainrotArrival;
    }
    private void Start()
    {
        _platforms = _platformsParent.GetComponentsInChildren<BrainrotPlatform>().ToList();
        foreach (BrainrotPlatform platform in _platforms)
        {
            platform.SetPlayerBase(this);
        }
    }

    //public void PlatformStatusChanged(BrainrotPlatform platform, bool empty)
    //{
    //    if (empty && !_emptyPlatforms.Contains(platform))
    //        _emptyPlatforms.Add(platform);
    //    else if (!empty && _emptyPlatforms.Contains(platform))
    //        _emptyPlatforms.Remove(platform);
    //}

    public BrainrotPlatform GetEmptyPlatform()
    {
        foreach (BrainrotPlatform platform in _platforms)
        {
            if (platform.Empty)
                return platform;
        }

        return null;
    }

    public void SetOwner(BaseOwner owner)
    {
        _owner = owner;
    }


    public void onBrainrotArrival(Brainrot brainrot)
    {
        foreach (BrainrotPlatform platform in _platforms)
        {
            if (platform.Brainrot == brainrot) {
                platform.OnBrainrotArrival();
            }
        }
    }

}
