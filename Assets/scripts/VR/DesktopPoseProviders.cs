using UnityEngine;

public class DesktopViewPoseProvider : MonoBehaviour, IViewPoseProvider
{
    [SerializeField] private Transform viewTransform;

    private Transform ActiveView => viewTransform != null ? viewTransform : transform;

    public Vector3 Position => ActiveView.position;
    public Quaternion Rotation => ActiveView.rotation;
    public Vector3 Forward => ActiveView.forward;
}

public class DesktopAimPoseProvider : MonoBehaviour, IAimPoseProvider
{
    [SerializeField] private Transform aimTransform;

    private Transform ActiveAim => aimTransform != null ? aimTransform : transform;

    public Vector3 Position => ActiveAim.position;
    public Quaternion Rotation => ActiveAim.rotation;
    public Vector3 Forward => ActiveAim.forward;
}

public class DesktopInteractionSource : MonoBehaviour, IInteractionSource
{
    [SerializeField] private Camera sourceCamera;

    private Camera ActiveCamera => sourceCamera != null ? sourceCamera : Camera.main;

    public bool IsPrimaryUsePressed()
    {
        return Input.GetMouseButtonDown(0);
    }

    public Ray GetInteractionRay()
    {
        var cam = ActiveCamera;
        if (cam == null)
        {
            return new Ray(transform.position, transform.forward);
        }

        return cam.ScreenPointToRay(Input.mousePosition);
    }
}
