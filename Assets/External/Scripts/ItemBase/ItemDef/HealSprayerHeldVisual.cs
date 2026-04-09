using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class HealSprayerHeldVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. Gun mesh animator; CrossFade only runs when assigned and enabled below.")]
    [FormerlySerializedAs("animator")]
    [SerializeField] private Animator heldItemAnimator;
    [Tooltip("Player character animator for GunIdle / GunWalk bools. If empty, resolved from PlayerItemRunner at Bind.")]
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private Rigidbody ownerRigidbody;
    [SerializeField] private GameObject healZoneRoot;
    [SerializeField] private HealSprayerHealZone healZone;

    [Header("Held item animation (CrossFade)")]
    [SerializeField] private bool useHeldItemAnimatorCrossFade = true;
    [SerializeField] private string defaultIdleStateName = "Idle";
    [SerializeField] private string aimIdleStateName = "GunIdle";
    [SerializeField] private string aimWalkStateName = "GunWalk";
    [SerializeField, Min(0f)] private float crossFadeDuration = 0.08f;

    [Header("Player animator (bool parameters)")]
    [SerializeField] private bool driveCharacterAnimatorGunBools = true;
    [SerializeField] private string gunIdleBoolParameterName = "GunIdle";
    [SerializeField] private string gunWalkBoolParameterName = "GunWalk";

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeedThreshold = 0.1f;

    private PlayerItemRunner ownerRunner;
    private int currentHeldStateHash;
    private bool previousZoneActive;

    private int gunIdleParamId;
    private int gunWalkParamId;
    private bool lastGunIdleBool;
    private bool lastGunWalkBool;
    private bool gunBoolParamsInitialized;

    private void Reset()
    {
        heldItemAnimator = GetComponent<Animator>();
        ownerRigidbody = GetComponentInParent<Rigidbody>();
        if (healZone == null)
            healZone = GetComponentInChildren<HealSprayerHealZone>(true);

        if (healZoneRoot == null && healZone != null)
            healZoneRoot = healZone.gameObject;
    }

    private void Awake()
    {
        if (heldItemAnimator == null)
            heldItemAnimator = GetComponent<Animator>();

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

    private void OnDisable()
    {
        ResetCharacterGunBoolParameters();
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

        if (characterAnimator == null && ownerRunner != null)
        {
            characterAnimator = ownerRunner.GetComponent<Animator>()
                ?? ownerRunner.GetComponentInParent<Animator>();
        }

        if (healZone == null)
            healZone = GetComponentInChildren<HealSprayerHealZone>(true);

        if (healZoneRoot == null && healZone != null)
            healZoneRoot = healZone.gameObject;

        if (healZone != null)
        {
            healZone.Configure(ownerRunner, definition);
            healZone.SetSprayVolumeActive(false);
        }

        previousZoneActive = false;
        gunBoolParamsInitialized = false;

        RefreshVisualState(true);
    }

    private void RefreshVisualState(bool forceRefresh)
    {
        bool isAiming = ownerRunner != null && ownerRunner.IsAimingItem;
        bool isMoving = ResolveIsMoving();
        bool sprayActive =
            ownerRunner != null &&
            ownerRunner.IsAimingItem &&
            ownerRunner.IsPrimaryItemInUse;

        if (healZone != null && (forceRefresh || previousZoneActive != sprayActive))
        {
            healZone.SetSprayVolumeActive(sprayActive);
            previousZoneActive = sprayActive;
        }
        else if (healZone == null && healZoneRoot != null &&
                 (forceRefresh || previousZoneActive != sprayActive))
        {
            healZoneRoot.SetActive(sprayActive);
            previousZoneActive = sprayActive;
        }

        RefreshCharacterAnimatorGunBools(isAiming, isMoving, forceRefresh);
        RefreshHeldItemCrossFade(isAiming, isMoving, forceRefresh);
    }

    private void RefreshCharacterAnimatorGunBools(bool isAiming, bool isMoving, bool forceRefresh)
    {
        if (!driveCharacterAnimatorGunBools || characterAnimator == null)
            return;

        if (!gunBoolParamsInitialized)
        {
            gunIdleParamId = Animator.StringToHash(gunIdleBoolParameterName);
            gunWalkParamId = Animator.StringToHash(gunWalkBoolParameterName);
            gunBoolParamsInitialized = true;
        }

        bool gunIdle = isAiming && !isMoving;
        bool gunWalk = isAiming && isMoving;

        if (!forceRefresh && gunIdle == lastGunIdleBool && gunWalk == lastGunWalkBool)
            return;

        characterAnimator.SetBool(gunIdleParamId, gunIdle);
        characterAnimator.SetBool(gunWalkParamId, gunWalk);
        lastGunIdleBool = gunIdle;
        lastGunWalkBool = gunWalk;
    }

    private void RefreshHeldItemCrossFade(bool isAiming, bool isMoving, bool forceRefresh)
    {
        if (!useHeldItemAnimatorCrossFade || heldItemAnimator == null)
            return;

        string targetStateName = !isAiming
            ? defaultIdleStateName
            : isMoving
                ? aimWalkStateName
                : aimIdleStateName;

        if (string.IsNullOrWhiteSpace(targetStateName))
            return;

        int targetStateHash = Animator.StringToHash(targetStateName);
        if (!forceRefresh && currentHeldStateHash == targetStateHash)
            return;

        heldItemAnimator.CrossFadeInFixedTime(targetStateHash, crossFadeDuration);
        currentHeldStateHash = targetStateHash;
    }

    private void ResetCharacterGunBoolParameters()
    {
        if (!driveCharacterAnimatorGunBools || characterAnimator == null || !gunBoolParamsInitialized)
            return;

        characterAnimator.SetBool(gunIdleParamId, false);
        characterAnimator.SetBool(gunWalkParamId, false);
        lastGunIdleBool = false;
        lastGunWalkBool = false;
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
