using UnityEngine;

[System.Serializable]
public struct ItemAimSettings
{
    [SerializeField] private ItemAimType aimType;
    [SerializeField, Min(0f)] private float maxDistance;
    [SerializeField] private LayerMask aimMask;
    [SerializeField] private QueryTriggerInteraction triggerInteraction;

    public ItemAimType AimType => aimType;
    public float MaxDistance => maxDistance;
    public LayerMask AimMask => aimMask;
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;
}