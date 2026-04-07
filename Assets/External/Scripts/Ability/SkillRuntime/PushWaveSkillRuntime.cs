using System.Collections.Generic;
using UnityEngine;

public sealed class PushWaveSkillRuntime : SkillRuntimeBase<PushWaveSkillDefinition>
{
    private readonly Collider[] hitBuffer = new Collider[16];
    private readonly HashSet<PlayerMovementEffectReceiver> uniqueTargets = new();

    public PushWaveSkillRuntime(PushWaveSkillDefinition definition, SkillRuntimeContext context)
        : base(definition, context)
    {
    }

    protected override bool Activate()
    {
        Transform castOrigin = Context.CastOrigin != null ? Context.CastOrigin : Context.OwnerTransform;
        if (castOrigin == null)
            return false;

        SpawnEffectIfNeeded(castOrigin);
        PushTargets(castOrigin);

        return true;
    }

    private void SpawnEffectIfNeeded(Transform castOrigin)
    {
        if (TypedDefinition.NetworkEffectPrefab == null)
            return;

        Transform spawnPoint = Context.EffectSpawnPoint != null ? Context.EffectSpawnPoint : castOrigin;
        Context.Bridge.SpawnNetworkEffect(
            TypedDefinition.NetworkEffectPrefab,
            spawnPoint.position,
            spawnPoint.rotation);
    }

    private void PushTargets(Transform castOrigin)
    {
        Vector3 center = GetBoxCenter(castOrigin);
        Vector3 halfExtents = new Vector3(
            TypedDefinition.Width * 0.5f,
            TypedDefinition.Height * 0.5f,
            TypedDefinition.Range * 0.5f);

        int hitCount = Context.Bridge.OverlapBox(
            center,
            halfExtents,
            castOrigin.rotation,
            hitBuffer,
            TypedDefinition.TargetMask,
            TypedDefinition.TriggerInteraction);

        uniqueTargets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null)
                continue;

            PlayerMovementEffectReceiver receiver = hit.GetComponentInParent<PlayerMovementEffectReceiver>();
            if (receiver == null)
                continue;

            if (receiver == Context.SelfMovementReceiver)
                continue;

            if (!uniqueTargets.Add(receiver))
                continue;

            Vector3 pushDirection = GetPushDirection(castOrigin, receiver.transform);

            receiver.RequestKnockback(
                pushDirection,
                TypedDefinition.PushSpeed,
                TypedDefinition.PushDuration,
                TypedDefinition.PushPriority,
                TypedDefinition.FacePushDirection);
        }
    }

    private Vector3 GetBoxCenter(Transform castOrigin)
    {
        return castOrigin.position + castOrigin.forward * (TypedDefinition.ForwardOffset + TypedDefinition.Range * 0.5f);
    }

    private Vector3 GetPushDirection(Transform castOrigin, Transform targetTransform)
    {
        Vector3 direction;

        if (TypedDefinition.PushFromCasterCenter)
        {
            direction = targetTransform.position - Context.OwnerTransform.position;
        }
        else
        {
            direction = castOrigin.forward;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = castOrigin.forward;

        direction.y = 0f;
        return direction.normalized;
    }
}