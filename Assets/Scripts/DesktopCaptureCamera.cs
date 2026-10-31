using UnityEngine;

/// <summary>Keep spectator/recording outputs off the standalone headset GPU.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-1000)]
public sealed class DesktopCaptureCamera : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        GetComponent<Camera>().enabled = false;
        var listener = GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;
#endif
    }
}
