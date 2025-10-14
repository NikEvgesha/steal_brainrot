using UnityEngine;

public abstract class InitializeProvider : MonoBehaviour 
{
    public bool Initialized;
    public abstract void Initialize();
}