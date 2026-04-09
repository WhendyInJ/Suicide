using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class HealSprayerHeldVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody ownerRigidbody;
    [SerializeField] private GameObject healZoneRoot;
    [SerializeField] private HealSprayerHealZone healZone;

    [Header("Animation State Names")]
    [SerializeField] private string defaultIdleStateName = "Idle";
    [SerializeField] private string aimIdleStateName = "GunIdle";
    [SerializeField] private string aimWalkStateName = "GunWalk";

    [Header("Animation")]
    [SerializeField, Min(0f)] private float moveSpeedThreshold = 0.1f;
    [SerializeField, Min(0f)] private float crossFadeDuration = 0.08f;

    private PlayerItemRunner ownerRunner;
    private int currentStateHash;
    private bool previousZoneActive;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        ownerRigidbody = GetComponentInParent<Rigidbody>();
        if (healZone == null)
            healZone = GetComponentInChildren<HealSprayerHealZone>(true);

        if (healZoneRoot == null && healZone != null)
            healZoneRoot = healZone.gameObject;
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (ownerRigidbody == null)
            ownerRigidbody = GetComponentInParent<Rigidbody>();

        if (healZone == null)
            healZone = GetComponentInChildren<HealSprayerHealZone>(true);

        if (healZoneRoot == null && healZone != null)
            healZoneRoot = healZone.gameObject;
    }

    private void OnEnable()
    {
        RefreshVisualState(true);
    }

    private void LateUpdate()
    {
        RefreshVisualState(false);
    }

    public void Bind(PlayerItemRunner runner, HealSprayerItemDefinition definition)
    {
        ownerRunner = runner;

        if (ownerRigidbody == null && ownerRunner != null)
            ownerRigidbody = ownerRunner.GetComponentInParent<Rigidbody>();

        if (healZone == null)
            healZone = GetComponentInChildren<HealSprayerHealZone>(true);

        if (healZoneRoot == null && healZone != null)
            healZoneRoot = healZone.gameObject;

        if (healZone != null)
            healZone.Configure(ownerRunner, definition);

        RefreshVisualState(true);
    }

    private void RefreshVisualState(bool forceRefresh)
    {
        bool isAiming = ownerRunner != null && ownerRunner.IsAimingItem;
        bool isMoving = ResolveIsMoving();
        bool zoneActive = ownerRunner != null && ownerRunner.IsPrimaryItemInUse;

        if (healZoneRoot != null && (forceRefresh || previousZoneActive != zoneActive))
        {
            healZoneRoot.SetActive(zoneActive);
            previousZoneActive = zoneActive;
        }

        if (animator == null)
            return;

        string targetStateName = !isAiming
            ? defaultIdleStateName
            : isMoving
                ? aimWalkStateName
                : aimIdleStateName;

        if (string.IsNullOrWhiteSpace(targetStateName))
            return;

        int targetStateHash = Animator.StringToHash(targetStateName);
        if (!forceRefresh && currentStateHash == targetStateHash)
            return;

        animator.CrossFadeInFixedTime(targetStateHash, crossFadeDuration);
        currentStateHash = targetStateHash;
    }

    private bool ResolveIsMoving()
    {
        if (ownerRigidbody == null)
            return false;

        Vector3 velocity = ownerRigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude > moveSpeedThreshold;
    }
}
