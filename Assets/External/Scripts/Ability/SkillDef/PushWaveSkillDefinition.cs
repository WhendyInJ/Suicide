using UnityEngine;

[CreateAssetMenu(
    fileName = "PushWaveSkillDefinition",
    menuName = "Game/Skills/Push Wave Skill Definition")]
public class PushWaveSkillDefinition : SkillDefinition
{
    [Header("Targeting")]
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0.1f)] private float range = 3f;
    [SerializeField, Min(0.1f)] private float width = 2f;
    [SerializeField, Min(0.1f)] private float height = 1.6f;
    [SerializeField, Min(0f)] private float forwardOffset = 0.8f;

    [Header("Push")]
    [SerializeField, Min(0f)] private float pushSpeed = 8f;
    [SerializeField, Min(0f)] private float pushDuration = 0.15f;
    [SerializeField] private int pushPriority = 100;
    [SerializeField] private bool pushFromCasterCenter = true;
    [SerializeField] private bool facePushDirection = true;

    [Header("Visual")]
    [SerializeField] private GameObject networkEffectPrefab;

    public LayerMask TargetMask => targetMask;
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;
    public float Range => range;
    public float Width => width;
    public float Height => height;
    public float ForwardOffset => forwardOffset;
    public float PushSpeed => pushSpeed;
    public float PushDuration => pushDuration;
    public int PushPriority => pushPriority;
    public bool PushFromCasterCenter => pushFromCasterCenter;
    public bool FacePushDirection => facePushDirection;
    public GameObject NetworkEffectPrefab => networkEffectPrefab;

    public override ISkillRuntime CreateRuntime(SkillRuntimeContext context)
    {
        return new PushWaveSkillRuntime(this, context);
    }
}