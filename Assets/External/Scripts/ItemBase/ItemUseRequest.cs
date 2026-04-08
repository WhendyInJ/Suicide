using UnityEngine;

public readonly struct ItemUseRequest
{
    public readonly Vector3 UseOrigin;
    public readonly Vector3 AimPoint;
    public readonly Vector3 AimDirection;
    public readonly GameObject ExplicitTarget;

    public ItemUseRequest(
        Vector3 useOrigin,
        Vector3 aimPoint,
        Vector3 aimDirection,
        GameObject explicitTarget = null)
    {
        UseOrigin = useOrigin;
        AimPoint = aimPoint;
        AimDirection = aimDirection;
        ExplicitTarget = explicitTarget;
    }
}