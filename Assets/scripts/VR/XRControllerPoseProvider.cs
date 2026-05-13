using UnityEngine;
using UnityEngine.InputSystem;

public class XRControllerPoseProvider : MonoBehaviour, IAimPoseProvider, IViewPoseProvider, IInteractionSource
{
    [SerializeField] private Transform poseTransform;
    [SerializeField] private InputActionProperty triggerAction;
    [SerializeField] private InputActionProperty gripAction;
    [SerializeField] private InputActionProperty thumbstickAction;
    [SerializeField] private InputActionProperty secondaryUseAction;

    private Transform ActivePose => poseTransform != null ? poseTransform : transform;

    public Vector3 Position => ActivePose.position;
    public Quaternion Rotation => ActivePose.rotation;
    public Vector3 Forward => ActivePose.forward;

    public bool IsPrimaryUsePressed()
    {
        return triggerAction.action != null && triggerAction.action.WasPressedThisFrame();
    }

    public bool IsSecondaryUsePressed()
    {
        return secondaryUseAction.action != null && secondaryUseAction.action.WasPressedThisFrame();
    }

    public float GripValue()
    {
        return gripAction.action != null ? gripAction.action.ReadValue<float>() : 0f;
    }

    public Vector2 ThumbstickValue()
    {
        return thumbstickAction.action != null ? thumbstickAction.action.ReadValue<Vector2>() : Vector2.zero;
    }

    public Ray GetInteractionRay()
    {
        return new Ray(Position, Forward);
    }
}
