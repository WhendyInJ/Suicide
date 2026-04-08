using UnityEngine;

public interface IItemExecutionBridge
{
    bool Raycast(
        Ray ray,
        float maxDistance,
        LayerMask layerMask,
        QueryTriggerInteraction triggerInteraction,
        out RaycastHit hit);

    void SpawnNetworkEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation);
    GameObject SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation);
}

public sealed class ItemRuntimeContext
{
    public Transform OwnerTransform { get; }
    public Transform CastOrigin { get; }
    public Transform EffectSpawnPoint { get; }
    public PlayerController PlayerController { get; }
    public PlayerMovementEffectReceiver SelfMovementReceiver { get; }
    public IItemExecutionBridge Bridge { get; }

    public ItemRuntimeContext(
        Transform ownerTransform,
        Transform castOrigin,
        Transform effectSpawnPoint,
        PlayerController playerController,
        PlayerMovementEffectReceiver selfMovementReceiver,
        IItemExecutionBridge bridge)
    {
        OwnerTransform = ownerTransform;
        CastOrigin = castOrigin;
        EffectSpawnPoint = effectSpawnPoint;
        PlayerController = playerController;
        SelfMovementReceiver = selfMovementReceiver;
        Bridge = bridge;
    }
}