using UnityEngine;

public class BaseOwner : MonoBehaviour
{
    [SerializeField] private PlayerBase _playerBase;

    public PlayerBase Base { get { return _playerBase; } }

    public void SetBase(PlayerBase playerBase)
    {
        _playerBase = playerBase;
        _playerBase.SetOwner(this);
    }
}
