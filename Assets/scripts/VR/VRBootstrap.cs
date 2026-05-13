using UnityEngine;

public class VRBootstrap : MonoBehaviour
{
    [SerializeField] private bool forceExclusiveFullscreenOnDesktop = true;
    [SerializeField] private bool disableComfortBreakingEffects = true;

    private void Awake()
    {
        if (forceExclusiveFullscreenOnDesktop && !Application.isMobilePlatform)
        {
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            Screen.fullScreen = true;
        }

        if (disableComfortBreakingEffects)
        {
            DisableComponentType("CameraBob");
            DisableComponentType("CameraShake");
        }
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
