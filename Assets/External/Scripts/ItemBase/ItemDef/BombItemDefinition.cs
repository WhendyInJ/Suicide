using UnityEngine;

[CreateAssetMenu(
    fileName = "BombItemDefinition",
    menuName = "Game/Items/Bomb Item Definition")]
public class BombItemDefinition : ItemDefinition
{
    [Header("Aim Preview")]
    [SerializeField] private BombAimPreviewEffect trajectoryPreviewPrefab;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private BombTrajectoryType trajectoryType = BombTrajectoryType.Parabolic;
    [SerializeField, Min(0.1f)] private float launchSpeed = 12f;
    [SerializeField, Min(0f)] private float additionalUpwardSpeed = 4f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 5f;
    [SerializeField] private LayerMask impactMask = ~0;

    [Header("Explosion")]
    [SerializeField, Min(0.1f)] private float explosionRadius = 3f;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField, Min(0f)] private float stunDuration = 2f;
    [SerializeField] private int stunPriority = 100;
    [SerializeField] private GameObject explosionEffectPrefab;

    public BombAimPreviewEffect TrajectoryPreviewPrefab => trajectoryPreviewPrefab;
    public GameObject ProjectilePrefab => projectilePrefab;
    public BombTrajectoryType TrajectoryType => trajectoryType;
    public float LaunchSpeed => launchSpeed;
    public float AdditionalUpwardSpeed => additionalUpwardSpeed;
    public float MaxLifetime => maxLifetime;
    public LayerMask ImpactMask => impactMask;

    public float ExplosionRadius => explosionRadius;
    public LayerMask TargetMask => targetMask;
    public float StunDuration => stunDuration;
    public int StunPriority => stunPriority;
    public GameObject ExplosionEffectPrefab => explosionEffectPrefab;

    public override bool TryBuildAimPreview(
        in ItemAimPreviewContext context,
        out ItemAimPreviewRequest request)
    {
        request = new ItemAimPreviewRequest
        {
            ShowTrajectory = true,
            TrajectoryPrefab = trajectoryPreviewPrefab,
            TrajectoryData = new ItemTrajectoryPreviewData
            {
                TrajectoryType = trajectoryType,
                LaunchSpeed = launchSpeed,
                AdditionalUpwardSpeed = additionalUpwardSpeed,
                MaxLifetime = maxLifetime,
                ImpactMask = impactMask,
                ExplosionRadius = explosionRadius
            }
        };

        return true;
    }

    public override IItemRuntime CreateRuntime(ItemRuntimeContext context)
    {
        return new BombItemRuntime(this, context);
    }
}
