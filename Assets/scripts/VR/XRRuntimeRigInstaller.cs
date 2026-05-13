using UnityEngine;
#if UNITY_XR_MANAGEMENT || UNITY_OPENXR || UNITY_EDITOR
using Unity.XR.CoreUtils;
#endif

/// <summary>
/// Scene binding helper for XR. Intentionally avoids spawning rig objects at runtime;
/// XR Origin/controllers should be authored in-scene.
/// </summary>
public class XRRuntimeRigInstaller : MonoBehaviour
{
#if UNITY_XR_MANAGEMENT || UNITY_OPENXR || UNITY_EDITOR
    [SerializeField] private XROrigin sceneXrOrigin;
    [SerializeField] private XRControllerPoseProvider leftControllerPoseProvider;
    [SerializeField] private XRControllerPoseProvider rightControllerPoseProvider;

    private void Awake()
    {
        if (sceneXrOrigin == null)
        {
            sceneXrOrigin = FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        }

        if (sceneXrOrigin == null)
        {
            Debug.LogWarning("XRRuntimeRigInstaller: No XROrigin found in-scene. Please add and wire an XR Origin in the Unity scene.");
            return;
        }

        if (leftControllerPoseProvider == null || rightControllerPoseProvider == null)
        {
            var providers = sceneXrOrigin.GetComponentsInChildren<XRControllerPoseProvider>(true);
            foreach (var provider in providers)
            {
                if (provider.gameObject.name.ToLowerInvariant().Contains("left") && leftControllerPoseProvider == null)
                {
                    leftControllerPoseProvider = provider;
                }
                else if (provider.gameObject.name.ToLowerInvariant().Contains("right") && rightControllerPoseProvider == null)
                {
                    rightControllerPoseProvider = provider;
                }
            }
        }

        if (leftControllerPoseProvider == null || rightControllerPoseProvider == null)
        {
            Debug.LogWarning("XRRuntimeRigInstaller: Missing left/right XRControllerPoseProvider on XR Origin children. Assign them in scene.");
        }
    }
#endif
}
