using UnityEngine;

[CreateAssetMenu(
    fileName = "GrabItemDefinition",
    menuName = "Game/Items/Grab Item Definition")]
public class GrabItemDefinition : ItemDefinition
{
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

    public override IItemRuntime CreateRuntime(ItemRuntimeContext context)
    {
        return new GrabItemRuntime(this, context);
    }
}
