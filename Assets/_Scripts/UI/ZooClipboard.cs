using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class ZooClipboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int ZooClipboardCopyText(string text);
#endif

    public static bool TryCopyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            return ZooClipboardCopyText(text) != 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Clipboard] Browser clipboard failed: {exception.Message}");
            return false;
        }
#else
        GUIUtility.systemCopyBuffer = text;
        return true;
#endif
    }
}
