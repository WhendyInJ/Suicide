using UnityEngine;

public sealed class KnockbackBuzzerItemRuntime : ItemRuntimeBase<KnockbackBuzzerItemDefinition>
{
    public KnockbackBuzzerItemRuntime(KnockbackBuzzerItemDefinition definition, ItemRuntimeContext context)
        : base(definition, context)
    {
    }

    protected override bool UseInternal(in ItemUseRequest request)
    {
        if (Context.Bridge == null || TypedDefinition.EmitterPrefab == null)
            return false;

        Transform ownerTransform = Context.OwnerTransform;
        if (ownerTransform == null)
            return false;

        GameObject spawned = Context.Bridge.SpawnNetworkObject(
            TypedDefinition.EmitterPrefab,
            ownerTransform.position,
            ownerTransform.rotation);

        if (spawned == null)
            return false;

        KnockbackBuzzerEmitter emitter = spawned.GetComponent<KnockbackBuzzerEmitter>();
        if (emitter == null)
            emitter = spawned.AddComponent<KnockbackBuzzerEmitter>();

        emitter.InitializeAsOwner(
            ownerTransform,
            TypedDefinition.PulseCount,
            TypedDefinition.InitialPulseDelay,
            TypedDefinition.PulseInterval,
            TypedDefinition.Radius,
            TypedDefinition.TargetMask,
            TypedDefinition.TriggerInteraction,
            TypedDefinition.IgnoreSelf,
            TypedDefinition.HorizontalImpulse,
            TypedDefinition.UpwardImpulse,
            TypedDefinition.ControlReleaseMode,
            TypedDefinition.ControlReleaseTimeout,
            TypedDefinition.FacePushDirection,
            TypedDefinition.ClearTargetMovementCommands,
            TypedDefinition.PulseEffectPrefab,
            TypedDefinition.PulseEffectScaleMultiplier);

        return true;
    }
}
