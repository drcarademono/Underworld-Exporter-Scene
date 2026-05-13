using UnityEngine;
#if UNITY_XR_MANAGEMENT || UNITY_OPENXR || UNITY_EDITOR
using Unity.XR.CoreUtils;
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
#endif

public class XRRuntimeRigInstaller : MonoBehaviour
{
#if UNITY_XR_MANAGEMENT || UNITY_OPENXR || UNITY_EDITOR
    [SerializeField] private bool spawnIfMissing = true;
    [SerializeField] private bool createEditorSimulator = true;

    private void Awake()
    {
        EnsureXROrigin();
#if UNITY_EDITOR
        if (createEditorSimulator)
        {
            EnsureXRDeviceSimulator();
        }
#endif
    }

    private static void EnsureXROrigin()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        if (origin != null)
        {
            return;
        }

        var originObj = new GameObject("XR Origin");
        origin = originObj.AddComponent<XROrigin>();

        var cameraOffset = new GameObject("Camera Offset");
        cameraOffset.transform.SetParent(originObj.transform, false);
        origin.CameraFloorOffsetObject = cameraOffset;

        var cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        cameraObj.transform.SetParent(cameraOffset.transform, false);
        cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();
        cameraObj.AddComponent<TrackedPoseDriver>();
        origin.Camera = cameraObj.GetComponent<Camera>();

        CreateController("Left Controller", cameraOffset.transform, XRNode.LeftHand);
        CreateController("Right Controller", cameraOffset.transform, XRNode.RightHand);
    }

    private static void CreateController(string name, Transform parent, XRNode node)
    {
        var controller = new GameObject(name);
        controller.transform.SetParent(parent, false);
        controller.AddComponent<XRController>();
        var pose = controller.AddComponent<TrackedPoseDriver>();
        pose.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRController, node == XRNode.LeftHand ? TrackedPoseDriver.TrackedPose.LeftPose : TrackedPoseDriver.TrackedPose.RightPose);
        controller.AddComponent<XRControllerPoseProvider>();
    }

#if UNITY_EDITOR
    private static void EnsureXRDeviceSimulator()
    {
        if (FindAnyObjectByType<XRDeviceSimulator>() != null)
        {
            return;
        }

        var simulator = new GameObject("XR Device Simulator");
        simulator.AddComponent<XRDeviceSimulator>();
    }
#endif
#endif
}
