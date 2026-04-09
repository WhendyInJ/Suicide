using UnityEngine;

[CreateAssetMenu(
    fileName = "HealSprayerItemDefinition",
    menuName = "Game/Items/Heal Sprayer Item Definition")]
public class HealSprayerItemDefinition : ItemDefinition
{
    [Header("Aim Preview")]
    [SerializeField] private CrosshairAimPreview crosshairPreviewPrefab;

    [Header("Heal")]
    [SerializeField, Min(0.1f)] private float maxDistance = 12f;
    [SerializeField, Min(0f)] private float healPerTick = 1f;
    [SerializeField, Min(0.01f)] private float healTickInterval = 0.2f;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private GameObject healEffectPrefab;

    public CrosshairAimPreview CrosshairPreviewPrefab => crosshairPreviewPrefab;
    public float MaxDistance => maxDistance;
    public float HealPerTick => healPerTick;
    public float HealTickInterval => healTickInterval;
    public LayerMask TargetMask => targetMask;
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;
    public GameObject HealEffectPrefab => healEffectPrefab;

    public override bool UseHandHeldItemAnchor => true;

    public override bool TryBuildAimPreview(
        in ItemAimPreviewContext context,
        out ItemAimPreviewRequest request)
    {
        request = new ItemAimPreviewRequest
        {
            ShowCrosshair = true,
            CrosshairPrefab = crosshairPreviewPrefab
        };

        return true;
    }

    public override IItemRuntime CreateRuntime(ItemRuntimeContext context)
    {
        return new HealSprayerItemRuntime(this, context);
    }
}
