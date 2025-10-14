using UnityEngine;
using MirraGames.SDK;  // MirraSDK core API
using MirraGames.SDK.Common;

//#if MIRRA_SDK_ENABLED
/// <summary>
/// Провайдер, использующий MirraSDK.Device API.
/// </summary>
public class MirraSDKDeviceProvider : DeviceProvider
{
    public override bool IsInitialized()
    {
        return MirraSDK.IsInitialized;
    }
    public override bool IsMobileDevice()
    {

        return MirraSDK.Device.IsMobile;
    }

    public override DeviceSystemType GetSystemType()
    {
        switch (MirraSDK.Device.SystemType)
        {
            case SystemType.Android:   return DeviceSystemType.Android;
            case SystemType.iOS:       return DeviceSystemType.iOS;
            case SystemType.Windows:   return DeviceSystemType.Windows;
            case SystemType.Mac:     return DeviceSystemType.MacOS;
            case SystemType.Linux:     return DeviceSystemType.Linux;
            default:                   return DeviceSystemType.Unknown;
        }
    }

    public override bool IsCursorVisible()
    {
        return MirraSDK.Device.CursorVisible;
    }

    public override CursorLockMode GetCursorLockState()
    {
        return MirraSDK.Device.CursorLock;
    }

    public override void SetCursorVisible(bool visible)
    {
        MirraSDK.Device.CursorVisible = visible;
    }

    public override void SetCursorLockState(CursorLockMode lockState)
    {
        MirraSDK.Device.CursorLock = lockState;
    }
}
//#endif
