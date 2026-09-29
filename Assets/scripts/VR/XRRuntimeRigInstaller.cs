using System.Reflection;
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
    [SerializeField] private XRHmdViewPoseProvider hmdViewPoseProvider;
    [SerializeField] private bool autoWireGameplayComponents = true;

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

        if (hmdViewPoseProvider == null && sceneXrOrigin.Camera != null)
        {
            hmdViewPoseProvider = sceneXrOrigin.Camera.GetComponent<XRHmdViewPoseProvider>();
            if (hmdViewPoseProvider == null)
            {
                hmdViewPoseProvider = sceneXrOrigin.Camera.gameObject.AddComponent<XRHmdViewPoseProvider>();
            }
        }

        if (leftControllerPoseProvider == null || rightControllerPoseProvider == null)
        {
            Debug.LogWarning("XRRuntimeRigInstaller: Missing left/right XRControllerPoseProvider on XR Origin children. Assign them in scene.");
            return;
        }

        if (autoWireGameplayComponents)
        {
            AutoWireGameplayBindings();
        }
    }

    private void AutoWireGameplayBindings()
    {
        var character = FindAnyObjectByType<Character>(FindObjectsInactive.Include);
        if (character != null)
        {
            SetSerializedMonoBehaviourField(character, "viewPoseProviderBehaviour", hmdViewPoseProvider);
            SetSerializedMonoBehaviourField(character, "interactionSourceBehaviour", rightControllerPoseProvider);
        }

        var combat = FindAnyObjectByType<UWCombat>(FindObjectsInactive.Include);
        if (combat != null)
        {
            SetSerializedMonoBehaviourField(combat, "interactionSourceBehaviour", rightControllerPoseProvider);
        }
    }

    private static void SetSerializedMonoBehaviourField(object target, string fieldName, MonoBehaviour value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    public XRControllerPoseProvider GetRightControllerProvider()
    {
        return rightControllerPoseProvider;
    }
#endif
}
