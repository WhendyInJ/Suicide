using UnityEngine;

public sealed class BombItemRuntime : ItemRuntimeBase<BombItemDefinition>
{
    public BombItemRuntime(BombItemDefinition definition, ItemRuntimeContext context)
        : base(definition, context)
    {
    }

    protected override bool UseInternal(in ItemUseRequest request)
    {
        if (TypedDefinition.ProjectilePrefab == null)
            return false;

        Transform spawnTransform = Context.EffectSpawnPoint != null
            ? Context.EffectSpawnPoint
            : Context.CastOrigin != null
                ? Context.CastOrigin
                : Context.OwnerTransform;

        Vector3 rawLaunchDirection = request.AimDirection.sqrMagnitude > 0.0001f
            ? request.AimDirection.normalized
            : spawnTransform.forward;

        Vector3 launchDirection = rawLaunchDirection.normalized;
        Vector3 spawnOffsetDirection = new Vector3(launchDirection.x, 0f, launchDirection.z);
        if (spawnOffsetDirection.sqrMagnitude <= 0.0001f)
        {
            spawnOffsetDirection = new Vector3(spawnTransform.forward.x, 0f, spawnTransform.forward.z);
        }

        if (spawnOffsetDirection.sqrMagnitude <= 0.0001f)
            spawnOffsetDirection = Vector3.forward;

        spawnOffsetDirection.Normalize();
        Quaternion spawnRotation = Quaternion.LookRotation(launchDirection, Vector3.up);
        Vector3 spawnPosition = spawnTransform.position + spawnOffsetDirection * 0.6f + Vector3.up * 0.2f;

        GameObject spawned = Context.Bridge.SpawnNetworkObject(
            TypedDefinition.ProjectilePrefab,
            spawnPosition,
            spawnRotation);

        if (spawned == null)
            return false;

        BombProjectile projectile = spawned.GetComponent<BombProjectile>();
        if (projectile == null)
            return true;

        projectile.InitializeAsOwner(
            ownerRoot: Context.OwnerTransform,
            launchDirection: launchDirection,
            trajectoryType: TypedDefinition.TrajectoryType,
            launchSpeed: TypedDefinition.LaunchSpeed,
            additionalUpwardSpeed: TypedDefinition.AdditionalUpwardSpeed,
            maxLifetime: TypedDefinition.MaxLifetime,
            impactMask: TypedDefinition.ImpactMask,
            targetMask: TypedDefinition.TargetMask,
            explosionRadius: TypedDefinition.ExplosionRadius,
            stunDuration: TypedDefinition.StunDuration,
            stunPriority: TypedDefinition.StunPriority,
            explosionEffectPrefab: TypedDefinition.ExplosionEffectPrefab);

        return true;
    }
}
