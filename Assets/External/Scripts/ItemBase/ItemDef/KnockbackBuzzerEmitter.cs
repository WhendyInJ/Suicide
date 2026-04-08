using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class KnockbackBuzzerEmitter : MonoBehaviourPun
{
    [Header("Runtime Debug")]
    [SerializeField] private bool initialized;
    [SerializeField] private int pulsesEmitted;
    [SerializeField] private float debugRadius;

    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly HashSet<PlayerMovementEffectReceiver> uniqueTargets = new();

    private Transform ownerRoot;
    private int pulseCount;
    private float pulseInterval;
    private float radius;
    private LayerMask targetMask;
    private QueryTriggerInteraction triggerInteraction;
    private bool ignoreSelf;

    private float horizontalImpulse;
    private float upwardImpulse;
    private ImpulseControlReleaseMode controlReleaseMode;
    private float controlReleaseTimeout;
    private bool facePushDirection;
    private bool clearTargetMovementCommands;

    private GameObject pulseEffectPrefab;
    private string pulseEffectResourceName;
    private float pulseEffectScaleMultiplier;
    private float nextPulseTime;

    public void InitializeAsOwner(
        Transform ownerRoot,
        int pulseCount,
        float initialPulseDelay,
        float pulseInterval,
        float radius,
        LayerMask targetMask,
        QueryTriggerInteraction triggerInteraction,
        bool ignoreSelf,
        float horizontalImpulse,
        float upwardImpulse,
        ImpulseControlReleaseMode controlReleaseMode,
        float controlReleaseTimeout,
        bool facePushDirection,
        bool clearTargetMovementCommands,
        GameObject pulseEffectPrefab,
        float pulseEffectScaleMultiplier)
    {
        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        this.ownerRoot = ownerRoot;
        this.pulseCount = Mathf.Max(1, pulseCount);
        this.pulseInterval = Mathf.Max(0.01f, pulseInterval);
        this.radius = Mathf.Max(0.1f, radius);
        this.targetMask = targetMask;
        this.triggerInteraction = triggerInteraction;
        this.ignoreSelf = ignoreSelf;
        this.horizontalImpulse = Mathf.Max(0f, horizontalImpulse);
        this.upwardImpulse = Mathf.Max(0f, upwardImpulse);
        this.controlReleaseMode = controlReleaseMode;
        this.controlReleaseTimeout = Mathf.Max(0f, controlReleaseTimeout);
        this.facePushDirection = facePushDirection;
        this.clearTargetMovementCommands = clearTargetMovementCommands;
        this.pulseEffectPrefab = pulseEffectPrefab;
        pulseEffectResourceName = pulseEffectPrefab != null ? pulseEffectPrefab.name : string.Empty;
        this.pulseEffectScaleMultiplier = Mathf.Max(0f, pulseEffectScaleMultiplier);

        debugRadius = this.radius;
        pulsesEmitted = 0;
        nextPulseTime = Time.time + Mathf.Max(0f, initialPulseDelay);
        initialized = true;
    }

    private void Update()
    {
        if (!CanOwnerSimulate())
            return;

        if (ownerRoot != null)
            transform.position = ownerRoot.position;

        if (Time.time < nextPulseTime)
            return;

        EmitPulse();
        pulsesEmitted++;

        if (pulsesEmitted >= pulseCount)
        {
            DestroyEmitter();
            return;
        }

        nextPulseTime = Time.time + pulseInterval;
    }

    private bool CanOwnerSimulate()
    {
        if (!initialized)
            return false;

        if (!PhotonNetwork.InRoom)
            return true;

        return photonView != null && photonView.IsMine;
    }

    private void EmitPulse()
    {
        Vector3 center = ownerRoot != null ? ownerRoot.position : transform.position;
        SpawnPulseEffect(center);

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            overlapBuffer,
            targetMask,
            triggerInteraction);

        uniqueTargets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null)
                continue;

            PlayerMovementEffectReceiver receiver = hit.GetComponentInParent<PlayerMovementEffectReceiver>();
            if (receiver == null)
                continue;

            if (!uniqueTargets.Add(receiver))
                continue;

            if (ignoreSelf && ownerRoot != null && receiver.transform.root == ownerRoot.root)
                continue;

            Vector3 pushDirection = receiver.transform.position - center;
            pushDirection.y = 0f;

            if (pushDirection.sqrMagnitude <= 0.0001f)
            {
                pushDirection = ownerRoot != null ? ownerRoot.forward : transform.forward;
                pushDirection.y = 0f;
            }

            if (pushDirection.sqrMagnitude <= 0.0001f)
                pushDirection = Vector3.forward;

            pushDirection.Normalize();

            Vector3 finalImpulse =
                pushDirection * horizontalImpulse +
                Vector3.up * upwardImpulse;

            receiver.RequestImpulseKnockback(
                finalImpulse,
                controlReleaseMode,
                controlReleaseTimeout,
                facePushDirection,
                clearTargetMovementCommands);
        }
    }

    private void SpawnPulseEffect(Vector3 center)
    {
        if (pulseEffectPrefab == null)
            return;

        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(
                nameof(RPC_SpawnPulseEffect),
                RpcTarget.All,
                center,
                radius,
                pulseEffectResourceName,
                pulseEffectScaleMultiplier);
            return;
        }

        GameObject spawnedEffect = Instantiate(pulseEffectPrefab, center, Quaternion.identity);
        ApplyPulseEffectScale(spawnedEffect, pulseEffectPrefab, radius, pulseEffectScaleMultiplier);
    }

    [PunRPC]
    private void RPC_SpawnPulseEffect(
        Vector3 center,
        float effectRadius,
        string effectResourceName,
        float effectScaleMultiplier)
    {
        if (string.IsNullOrWhiteSpace(effectResourceName))
            return;

        GameObject effectPrefab = Resources.Load<GameObject>(effectResourceName);
        if (effectPrefab == null)
            return;

        GameObject spawnedEffect = Instantiate(effectPrefab, center, Quaternion.identity);
        ApplyPulseEffectScale(spawnedEffect, effectPrefab, effectRadius, effectScaleMultiplier);
    }

    private void ApplyPulseEffectScale(
        GameObject spawnedEffect,
        GameObject effectPrefab,
        float effectRadius,
        float effectScaleMultiplier)
    {
        if (spawnedEffect == null || effectPrefab == null)
            return;

        float scaleMultiplier = Mathf.Max(0.01f, effectRadius * Mathf.Max(0f, effectScaleMultiplier));
        Vector3 baseScale = GetNormalizedScale(effectPrefab.transform.localScale);
        spawnedEffect.transform.localScale = baseScale * scaleMultiplier;
    }

    private Vector3 GetNormalizedScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private void DestroyEmitter()
    {
        if (PhotonNetwork.InRoom)
        {
            if (photonView != null && photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);

            return;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, debugRadius > 0f ? debugRadius : radius);
    }
}
