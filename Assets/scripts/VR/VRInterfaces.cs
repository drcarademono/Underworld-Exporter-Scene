using UnityEngine;

public interface IViewPoseProvider
{
    Vector3 Position { get; }
    Quaternion Rotation { get; }
    Vector3 Forward { get; }
}

public interface IAimPoseProvider
{
    Vector3 Position { get; }
    Quaternion Rotation { get; }
    Vector3 Forward { get; }
}

public interface IInteractionSource
{
    bool IsPrimaryUsePressed();
    Ray GetInteractionRay();
}
