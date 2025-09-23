using UnityEngine;

public class TestInitBrainrot : MonoBehaviour
{

    [SerializeField] private InteractionRaycastListener _floorListener;
    [SerializeField] private Brainrot _brainrot;
    [SerializeField] private BrainrotData _data;
    [SerializeField] private Rarity _rarity;
    private Brainrot _currentBrainrot;
    private bool _use = false;
    void Start()
    {
        _floorListener = GetComponent<InteractionRaycastListener>();
        _floorListener._hitEvent.AddListener(SpawnBrainrot);
    }
    private void SpawnBrainrot()
    {
        if (_use) return; _use = true;

        _currentBrainrot = Instantiate(_brainrot,transform);
        _currentBrainrot.Init(_data,_rarity, _floorListener);
    }

    public void _GetBrainrot()
    {
        if (!_use) return; _use = false;
        Destroy(_currentBrainrot.gameObject);
        _currentBrainrot = null;

    }
}
