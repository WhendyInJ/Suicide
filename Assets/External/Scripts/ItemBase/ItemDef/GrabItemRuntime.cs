using UnityEngine;

public sealed class GrabItemRuntime : ItemRuntimeBase<GrabItemDefinition>
{
    public GrabItemRuntime(GrabItemDefinition definition, ItemRuntimeContext context)
        : base(definition, context)
    {
    }

    protected override bool UseInternal(in ItemUseRequest request)
    {
        if (Context.Bridge == null || TypedDefinition.ProjectilePrefab == null)
            return false;

        Vector3 aimDirection = request.AimDirection.sqrMagnitude > 0.0001f
            ? request.AimDirection.normalized
            : GetFallbackForward();

        Transform spawnTransform = Context.EffectSpawnPoint != null
            ? Context.EffectSpawnPoint
            : Context.CastOrigin != null
                ? Context.CastOrigin
                : Context.OwnerTransform;

        if (spawnTransform == null)
            return false;

        Vector3 spawnOffsetDirection = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection
            : GetFallbackForward();
        spawnOffsetDirection.Normalize();

        Vector3 spawnPosition =
            spawnTransform.position +
            spawnOffsetDirection * TypedDefinition.SpawnForwardOffset +
            Vector3.up * TypedDefinition.SpawnUpwardOffset;

        Quaternion spawnRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
        GameObject spawned = Context.Bridge.SpawnNetworkObject(
            TypedDefinition.ProjectilePrefab,
            spawnPosition,
            spawnRotation);

        if (spawned == null)
            return false;

        GrabProjectile projectile = spawned.GetComponent<GrabProjectile>();
        if (projectile == null)
            projectile = spawned.AddComponent<GrabProjectile>();

        projectile.InitializeAsOwner(
            Context.OwnerTransform,
            Context.SelfMovementReceiver,
            aimDirection,
            TypedDefinition.ProjectileSpeed,
            TypedDefinition.CastRange,
            TypedDefinition.HitMask,
            TypedDefinition.StructureMask,
            TypedDefinition.TriggerInteraction,
            TypedDefinition.SelfPullSpeed,
            TypedDefinition.SelfStopDistance,
            TypedDefinition.SelfMaxPullDuration,
            TypedDefinition.SelfPullPriority,
            TypedDefinition.TargetPullSpeed,
            TypedDefinition.TargetStopDistance,
            TypedDefinition.TargetMaxPullDuration,
            TypedDefinition.TargetFrontDistance,
            TypedDefinition.TargetPullPriority);

        return true;
    }

    private Vector3 GetFallbackForward()
    {
        Transform reference = Context.CastOrigin != null
            ? Context.CastOrigin
            : Context.OwnerTransform;

        return reference != null ? reference.forward : Vector3.forward;
    }
}
