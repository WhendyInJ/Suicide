using UnityEngine;

[CreateAssetMenu(
    fileName = "KnockbackBuzzerItemDefinition",
    menuName = "Game/Items/Knockback Buzzer Item Definition")]
public class KnockbackBuzzerItemDefinition : ItemDefinition
{
    [Header("Emitter")]
    [SerializeField] private GameObject emitterPrefab;
    [SerializeField, Min(1)] private int pulseCount = 3;
    [SerializeField, Min(0f)] private float initialPulseDelay = 0f;
    [SerializeField, Min(0.01f)] private float pulseInterval = 0.5f;
    [SerializeField] private GameObject pulseEffectPrefab;
    [SerializeField, Min(0f)] private float pulseEffectScaleMultiplier = 1f;

    [Header("Targeting")]
    [SerializeField, Min(0.1f)] private float radius = 3f;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool ignoreSelf = true;

    [Header("Impulse Push")]
    [SerializeField, Min(0f)] private float horizontalImpulse = 7f;
    [SerializeField, Min(0f)] private float upwardImpulse = 1.2f;
    [SerializeField] private ImpulseControlReleaseMode controlReleaseMode = ImpulseControlReleaseMode.UntilGrounded;
    [SerializeField, Min(0f)] private float controlReleaseTimeout = 1f;
    [SerializeField] private bool facePushDirection = true;
    [SerializeField] private bool clearTargetMovementCommands = true;

    public GameObject EmitterPrefab => emitterPrefab != null
        ? emitterPrefab
        : Resources.Load<GameObject>("KnockbackBuzzerEmitter");
    public int PulseCount => Mathf.Max(1, pulseCount);
    public float InitialPulseDelay => Mathf.Max(0f, initialPulseDelay);
    public float PulseInterval => Mathf.Max(0.01f, pulseInterval);
    public GameObject PulseEffectPrefab => pulseEffectPrefab;
    public float PulseEffectScaleMultiplier => Mathf.Max(0f, pulseEffectScaleMultiplier);

    public float Radius => Mathf.Max(0.1f, radius);
    public LayerMask TargetMask => targetMask;
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;
    public bool IgnoreSelf => ignoreSelf;

    public float HorizontalImpulse => Mathf.Max(0f, horizontalImpulse);
    public float UpwardImpulse => Mathf.Max(0f, upwardImpulse);
    public ImpulseControlReleaseMode ControlReleaseMode => controlReleaseMode;
    public float ControlReleaseTimeout => Mathf.Max(0f, controlReleaseTimeout);
    public bool FacePushDirection => facePushDirection;
    public bool ClearTargetMovementCommands => clearTargetMovementCommands;

    public override IItemRuntime CreateRuntime(ItemRuntimeContext context)
    {
        return new KnockbackBuzzerItemRuntime(this, context);
    }
}
