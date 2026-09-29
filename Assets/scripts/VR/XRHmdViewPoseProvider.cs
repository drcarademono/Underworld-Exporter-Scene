using UnityEngine;

public class XRHmdViewPoseProvider : MonoBehaviour, IViewPoseProvider
{
    [SerializeField] private Transform hmdTransform;

    private Transform ActiveTransform => hmdTransform != null ? hmdTransform : transform;

    public Vector3 Position => ActiveTransform.position;
    public Quaternion Rotation => ActiveTransform.rotation;
    public Vector3 Forward => ActiveTransform.forward;
}
