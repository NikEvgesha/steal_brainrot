using UnityEngine;

/// <summary>
/// ѕровайдер по умолчанию, использующий Unity API.
/// </summary>
public class DefaultDeviceProvider : DeviceProvider
{
    public override bool IsInitialized()
    {
        return true;
    }
    public override bool IsMobileDevice() => Application.isMobilePlatform;

    public override DeviceSystemType GetSystemType()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android: return DeviceSystemType.Android;
            case RuntimePlatform.IPhonePlayer: return DeviceSystemType.iOS;
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor: return DeviceSystemType.Windows;
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor: return DeviceSystemType.MacOS;
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxEditor: return DeviceSystemType.Linux;
            default: return DeviceSystemType.Unknown;
        }
    }

    public override bool IsCursorVisible() => Cursor.visible;
    public override CursorLockMode GetCursorLockState() => Cursor.lockState;

    public override void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
    }

    public override void SetCursorLockState(CursorLockMode lockState)
    {
        Cursor.lockState = lockState;
    }
}
