using UnityEngine;

public class VRBootstrap : MonoBehaviour
{
    private enum DesktopFullscreenMode
    {
        None,
        FullScreenWindow,
        ExclusiveFullScreen
    }

    [SerializeField] private DesktopFullscreenMode desktopFullscreenMode = DesktopFullscreenMode.FullScreenWindow;
    [SerializeField] private bool disableComfortBreakingEffects = true;

    private void Awake()
    {
        ApplyDesktopFullscreenMode();

        if (disableComfortBreakingEffects)
        {
            DisableComponentType("CameraBob");
            DisableComponentType("CameraShake");
        }
    }

    private void ApplyDesktopFullscreenMode()
    {
        if (Application.isMobilePlatform || desktopFullscreenMode == DesktopFullscreenMode.None)
        {
            return;
        }

        Screen.fullScreenMode = desktopFullscreenMode == DesktopFullscreenMode.ExclusiveFullScreen
            ? FullScreenMode.ExclusiveFullScreen
            : FullScreenMode.FullScreenWindow;
        Screen.fullScreen = true;
    }

    private static void DisableComponentType(string componentName)
    {
        var components = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var component in components)
        {
            if (component != null && component.GetType().Name == componentName)
            {
                component.enabled = false;
            }
        }
    }
}
