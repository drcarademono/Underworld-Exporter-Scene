using UnityEngine;
using UnityEngine.InputSystem;

public class XRControllerPoseProvider : MonoBehaviour, IAimPoseProvider, IViewPoseProvider, IInteractionSource
{
    [SerializeField] private Transform poseTransform;
    [SerializeField] private InputActionProperty triggerAction;

    private Transform ActivePose => poseTransform != null ? poseTransform : transform;

    public Vector3 Position => ActivePose.position;
    public Quaternion Rotation => ActivePose.rotation;
    public Vector3 Forward => ActivePose.forward;

    public bool IsPrimaryUsePressed()
    {
        return triggerAction.action != null && triggerAction.action.WasPressedThisFrame();
    }

    public Ray GetInteractionRay()
    {
        return new Ray(Position, Forward);
    }
}
