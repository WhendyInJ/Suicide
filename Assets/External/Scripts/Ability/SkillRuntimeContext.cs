using UnityEngine;

public interface ISkillExecutionBridge
{
    int OverlapBox(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation,
        Collider[] results,
        LayerMask layerMask,
        QueryTriggerInteraction triggerInteraction);

    void SpawnNetworkEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation);
}

public sealed class SkillRuntimeContext
{
    public Transform OwnerTransform { get; }
    public Transform CastOrigin { get; }
    public Transform EffectSpawnPoint { get; }
    public PlayerMovementEffectReceiver SelfMovementReceiver { get; }
    public ISkillExecutionBridge Bridge { get; }

    public SkillRuntimeContext(
        Transform ownerTransform,
        Transform castOrigin,
        Transform effectSpawnPoint,
        PlayerMovementEffectReceiver selfMovementReceiver,
        ISkillExecutionBridge bridge)
    {
        OwnerTransform = ownerTransform;
        CastOrigin = castOrigin;
        EffectSpawnPoint = effectSpawnPoint;
        SelfMovementReceiver = selfMovementReceiver;
        Bridge = bridge;
    }
}