using UnityEngine;

public readonly struct ItemAimPreviewContext
{
    public Transform OwnerTransform { get; }
    public Transform SpawnTransform { get; }
    public Vector3 UseOrigin { get; }
    public Vector3 AimPoint { get; }
    public Vector3 AimDirection { get; }

    public ItemAimPreviewContext(
        Transform ownerTransform,
        Transform spawnTransform,
        Vector3 useOrigin,
        Vector3 aimPoint,
        Vector3 aimDirection)
    {
        OwnerTransform = ownerTransform;
        SpawnTransform = spawnTransform;
        UseOrigin = useOrigin;
        AimPoint = aimPoint;
        AimDirection = aimDirection;
    }
}

public struct ItemTrajectoryPreviewData
{
    public BombTrajectoryType TrajectoryType;
    public float LaunchSpeed;
    public float AdditionalUpwardSpeed;
    public float MaxLifetime;
    public LayerMask ImpactMask;
    public float ExplosionRadius;
}

public struct ItemAimPreviewRequest
{
    public bool ShowCrosshair;
    public CrosshairAimPreview CrosshairPrefab;

    public bool ShowTrajectory;
    public BombAimPreviewEffect TrajectoryPrefab;
    public ItemTrajectoryPreviewData TrajectoryData;

    public bool ShowWorldMarker;
    public GameObject WorldMarkerPrefab;
    public Vector3 WorldMarkerPosition;
    public Quaternion WorldMarkerRotation;
}
