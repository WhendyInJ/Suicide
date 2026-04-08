using UnityEngine;

[CreateAssetMenu(
    fileName = "GrabItemDefinition",
    menuName = "Game/Items/Grab Item Definition")]
public class GrabItemDefinition : ItemDefinition
{
    [Header("Aim Preview")]
    [SerializeField] private CrosshairAimPreview crosshairPreviewPrefab;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject destinationPreviewPrefab;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 28f;
    [SerializeField, Min(0f)] private float spawnForwardOffset = 0.6f;
    [SerializeField, Min(0f)] private float spawnUpwardOffset = 0.2f;

    [Header("Targeting")]
    [SerializeField, Min(0.1f)] private float castRange = 14f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private LayerMask structureMask;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Self Pull")]
    [SerializeField, Min(0.1f)] private float selfPullSpeed = 18f;
    [SerializeField, Min(0.01f)] private float selfStopDistance = 1.2f;
    [SerializeField, Min(0.01f)] private float selfMaxPullDuration = 1.2f;
    [SerializeField] private int selfPullPriority = 220;

    [Header("Target Pull")]
    [SerializeField, Min(0.1f)] private float targetPullSpeed = 18f;
    [SerializeField, Min(0.01f)] private float targetStopDistance = 1.2f;
    [SerializeField, Min(0.01f)] private float targetMaxPullDuration = 1.2f;
    [SerializeField, Min(0f)] private float targetFrontDistance = 1.5f;
    [SerializeField] private int targetPullPriority = 220;

    public CrosshairAimPreview CrosshairPreviewPrefab => crosshairPreviewPrefab;
    public GameObject ProjectilePrefab => projectilePrefab != null
        ? projectilePrefab
        : Resources.Load<GameObject>("GrabProjectile");
    public GameObject DestinationPreviewPrefab => destinationPreviewPrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float SpawnForwardOffset => spawnForwardOffset;
    public float SpawnUpwardOffset => spawnUpwardOffset;

    public float CastRange => castRange;
    public LayerMask HitMask => hitMask;
    public LayerMask StructureMask => structureMask.value == 0 ? LayerMask.GetMask("Structure") : structureMask;
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;

    public float SelfPullSpeed => selfPullSpeed;
    public float SelfStopDistance => selfStopDistance;
    public float SelfMaxPullDuration => selfMaxPullDuration;
    public int SelfPullPriority => selfPullPriority;

    public float TargetPullSpeed => targetPullSpeed;
    public float TargetStopDistance => targetStopDistance;
    public float TargetMaxPullDuration => targetMaxPullDuration;
    public float TargetFrontDistance => targetFrontDistance;
    public int TargetPullPriority => targetPullPriority;

    private void OnValidate()
    {
        if (structureMask.value == 0)
            structureMask = LayerMask.GetMask("Structure");
    }

    public override bool TryBuildAimPreview(
        in ItemAimPreviewContext context,
        out ItemAimPreviewRequest request)
    {
        request = new ItemAimPreviewRequest
        {
            // ShowCrosshair = true,
            // CrosshairPrefab = crosshairPreviewPrefab
        };

        if (destinationPreviewPrefab == null || context.SpawnTransform == null)
            return true;

        Vector3 aimDirection = context.AimDirection.sqrMagnitude > 0.0001f
            ? context.AimDirection.normalized
            : context.SpawnTransform.forward;
        Vector3 start =
            context.SpawnTransform.position +
            aimDirection * spawnForwardOffset +
            Vector3.up * spawnUpwardOffset;

        if (!GrabProjectile.TryPredictResolution(
                ProjectilePrefab,
                context.OwnerTransform,
                start,
                aimDirection,
                castRange,
                hitMask,
                StructureMask,
                triggerInteraction,
                targetFrontDistance,
                out Vector3 previewPoint,
                out _))
        {
            return true;
        }

        request.ShowWorldMarker = true;
        request.WorldMarkerPrefab = destinationPreviewPrefab;
        request.WorldMarkerPosition = previewPoint;
        request.WorldMarkerRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        return true;
    }

    public override IItemRuntime CreateRuntime(ItemRuntimeContext context)
    {
        return new GrabItemRuntime(this, context);
    }
}
