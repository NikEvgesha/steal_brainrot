using System;
using UnityEngine;

/// <summary>
/// “ипы операционных систем устройства.
/// </summary>
public enum DeviceSystemType
{
    Unknown,
    Android,
    iOS,
    Windows,
    MacOS,
    Linux
}

/// <summary>
/// »нтерфейс дл€ провайдеров, предоставл€ющих информацию об устройстве и состо€нии курсора.
/// </summary>
public abstract class DeviceProvider : MonoBehaviour
{

    /// <summary> ¬идим ли текущий курсор? </summary>
    public abstract bool IsInitialized();

    /// <summary> явл€етс€ ли устройство мобильным? </summary>
    public abstract bool IsMobileDevice();

    /// <summary> “ип операционной системы устройства. </summary>
    public abstract DeviceSystemType GetSystemType();

    /// <summary> ¬идим ли текущий курсор? </summary>
    public abstract bool IsCursorVisible();

    /// <summary> —осто€ние блокировки курсора. </summary>
    public abstract CursorLockMode GetCursorLockState();

    /// <summary> —делать курсор видимым/невидимым. </summary>
    public abstract void SetCursorVisible(bool visible);

    /// <summary> ”становить режим блокировки курсора. </summary>
    public abstract void SetCursorLockState(CursorLockMode lockState);
}
