using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class GrabProjectile : MonoBehaviourPun
{
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private static readonly RaycastHit[] SharedPredictionHitBuffer = new RaycastHit[16];
    private static readonly Dictionary<int, float> CastRadiusCache = new();

    [Header("Runtime Debug")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool hasResolved;
    [SerializeField] private float debugMaxRange;
    [SerializeField] private float debugTravelledDistance;

    private Transform ownerRoot;
    private PlayerMovementEffectReceiver selfMovementReceiver;
    private Vector3 direction;
    private float speed;
    private float maxRange;
    private LayerMask hitMask;
    private LayerMask structureMask;
    private QueryTriggerInteraction triggerInteraction;

    private float selfPullSpeed;
    private float selfStopDistance;
    private float selfMaxPullDuration;
    private int selfPullPriority;

    private float targetPullSpeed;
    private float targetStopDistance;
    private float targetMaxPullDuration;
    private float targetFrontDistance;
    private int targetPullPriority;

    private Collider[] projectileColliders;
    private float projectileCastRadius;
    private Vector3 previousPosition;

    private void Awake()
    {
        projectileColliders = GetComponentsInChildren<Collider>(true);
        projectileCastRadius = CalculateProjectileCastRadius(projectileColliders);

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            enabled = false;
    }

    public void InitializeAsOwner(
        Transform ownerRoot,
        PlayerMovementEffectReceiver selfMovementReceiver,
        Vector3 launchDirection,
        float speed,
        float maxRange,
        LayerMask hitMask,
        LayerMask structureMask,
        QueryTriggerInteraction triggerInteraction,
        float selfPullSpeed,
        float selfStopDistance,
        float selfMaxPullDuration,
        int selfPullPriority,
        float targetPullSpeed,
        float targetStopDistance,
        float targetMaxPullDuration,
        float targetFrontDistance,
        int targetPullPriority)
    {
        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        this.ownerRoot = ownerRoot;
        this.selfMovementReceiver = selfMovementReceiver;
        this.direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : transform.forward;
        this.speed = Mathf.Max(0.1f, speed);
        this.maxRange = Mathf.Max(0.1f, maxRange);
        this.hitMask = hitMask;
        this.structureMask = structureMask.value == 0 ? LayerMask.GetMask("Structure") : structureMask;
        this.triggerInteraction = triggerInteraction;
        this.selfPullSpeed = selfPullSpeed;
        this.selfStopDistance = selfStopDistance;
        this.selfMaxPullDuration = selfMaxPullDuration;
        this.selfPullPriority = selfPullPriority;
        this.targetPullSpeed = targetPullSpeed;
        this.targetStopDistance = targetStopDistance;
        this.targetMaxPullDuration = targetMaxPullDuration;
        this.targetFrontDistance = targetFrontDistance;
        this.targetPullPriority = targetPullPriority;

        debugMaxRange = this.maxRange;
        initialized = true;
        previousPosition = transform.position;
        transform.rotation = Quaternion.LookRotation(this.direction, Vector3.up);
    }

    private void Update()
    {
        if (!CanOwnerSimulate())
            return;

        float travelStep = speed * Time.deltaTime;
        if (travelStep <= 0f)
            return;

        Vector3 start = transform.position;
        Vector3 end = start + direction * travelStep;

        if (TryFindHit(start, end, out RaycastHit hit))
        {
            ResolveHit(hit);
            return;
        }

        transform.position = end;
        previousPosition = end;
        debugTravelledDistance += travelStep;

        if (debugTravelledDistance >= maxRange)
            DestroyProjectile();
    }

    private bool CanOwnerSimulate()
    {
        if (!initialized || hasResolved)
            return false;

        if (!PhotonNetwork.InRoom)
            return true;

        return photonView != null && photonView.IsMine;
    }

    private bool TryFindHit(Vector3 start, Vector3 end, out RaycastHit bestHit)
    {
        Vector3 segment = end - start;
        float distance = segment.magnitude;
        bestHit = default;

        if (distance <= 0.0001f)
            return false;

        Vector3 castDirection = segment / distance;
        return TryFindNearestHit(
            start,
            castDirection,
            distance,
            projectileCastRadius,
            hitMask,
            triggerInteraction,
            ownerRoot,
            hitBuffer,
            out bestHit);
    }

    private bool ShouldIgnoreCollider(Collider candidate)
    {
        if (candidate == null)
            return true;

        if (ownerRoot != null && candidate.transform.root == ownerRoot.root)
            return true;

        if (projectileColliders == null)
            return false;

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            if (candidate == projectileColliders[i])
                return true;
        }

        return false;
    }

    private void ResolveHit(RaycastHit hit)
    {
        if (hasResolved)
            return;

        hasResolved = true;

        if (IsStructureHit(hit.collider))
        {
            selfMovementReceiver?.RequestPullToPoint(
                hit.point,
                selfPullSpeed,
                selfMaxPullDuration,
                selfStopDistance,
                selfPullPriority,
                true);

            DestroyProjectile();
            return;
        }

        PlayerMovementEffectReceiver targetReceiver = hit.collider != null
            ? hit.collider.GetComponentInParent<PlayerMovementEffectReceiver>()
            : null;

        if (targetReceiver != null && ownerRoot != null && targetReceiver.transform.root != ownerRoot.root)
        {
            Vector3 pullPoint = CalculateTargetPullPoint(
                ownerRoot,
                direction,
                targetReceiver.transform.position,
                targetFrontDistance);

            targetReceiver.RequestPullToPoint(
                pullPoint,
                targetPullSpeed,
                targetMaxPullDuration,
                targetStopDistance,
                targetPullPriority,
                true);
        }

        DestroyProjectile();
    }

    private bool IsStructureHit(Collider collider)
    {
        if (collider == null)
            return false;

        return (structureMask.value & (1 << collider.gameObject.layer)) != 0;
    }

    public static bool TryPredictResolution(
        GameObject projectilePrefab,
        Transform ownerRoot,
        Vector3 start,
        Vector3 direction,
        float maxRange,
        LayerMask hitMask,
        LayerMask structureMask,
        QueryTriggerInteraction triggerInteraction,
        float targetFrontDistance,
        out Vector3 previewPoint,
        out Vector3 previewNormal)
    {
        previewPoint = Vector3.zero;
        previewNormal = Vector3.up;

        if (projectilePrefab == null || direction.sqrMagnitude <= 0.0001f || maxRange <= 0f)
            return false;

        float castRadius = GetCastRadius(projectilePrefab);
        Vector3 castDirection = direction.normalized;

        if (!TryFindNearestHit(
                start,
                castDirection,
                maxRange,
                castRadius,
                hitMask,
                triggerInteraction,
                ownerRoot,
                SharedPredictionHitBuffer,
                out RaycastHit hit))
        {
            return false;
        }

        if (IsStructureHit(hit.collider, structureMask))
        {
            previewPoint = hit.point;
            previewNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            return true;
        }

        PlayerMovementEffectReceiver targetReceiver = hit.collider != null
            ? hit.collider.GetComponentInParent<PlayerMovementEffectReceiver>()
            : null;

        if (targetReceiver == null || ownerRoot == null || targetReceiver.transform.root == ownerRoot.root)
            return false;

        previewPoint = CalculateTargetPullPoint(
            ownerRoot,
            castDirection,
            targetReceiver.transform.position,
            targetFrontDistance);
        previewNormal = Vector3.up;
        return true;
    }

    public static float GetCastRadius(GameObject projectilePrefab)
    {
        if (projectilePrefab == null)
            return 0f;

        int prefabId = projectilePrefab.GetInstanceID();
        if (CastRadiusCache.TryGetValue(prefabId, out float cachedRadius))
            return cachedRadius;

        Collider[] colliders = projectilePrefab.GetComponentsInChildren<Collider>(true);
        float radius = CalculateProjectileCastRadius(colliders);
        CastRadiusCache[prefabId] = radius;
        return radius;
    }

    private static bool TryFindNearestHit(
        Vector3 start,
        Vector3 castDirection,
        float distance,
        float castRadius,
        LayerMask hitMask,
        QueryTriggerInteraction triggerInteraction,
        Transform ownerRoot,
        RaycastHit[] buffer,
        out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = castRadius > 0f
            ? Physics.SphereCastNonAlloc(
                start,
                castRadius,
                castDirection,
                buffer,
                distance,
                hitMask,
                triggerInteraction)
            : Physics.RaycastNonAlloc(
                start,
                castDirection,
                buffer,
                distance,
                hitMask,
                triggerInteraction);

        bool found = false;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = buffer[i];
            if (hit.collider == null)
                continue;

            if (ShouldIgnoreCollider(hit.collider, ownerRoot))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private static bool ShouldIgnoreCollider(Collider candidate, Transform ownerRoot)
    {
        if (candidate == null)
            return true;

        if (ownerRoot != null && candidate.transform.root == ownerRoot.root)
            return true;

        return false;
    }

    private static bool IsStructureHit(Collider collider, LayerMask structureMask)
    {
        if (collider == null)
            return false;

        return (structureMask.value & (1 << collider.gameObject.layer)) != 0;
    }

    private static Vector3 CalculateTargetPullPoint(
        Transform ownerRoot,
        Vector3 fallbackDirection,
        Vector3 targetPosition,
        float targetFrontDistance)
    {
        Vector3 ownerForward = ownerRoot != null ? ownerRoot.forward : fallbackDirection;
        ownerForward.y = 0f;

        if (ownerForward.sqrMagnitude <= 0.0001f)
        {
            ownerForward = fallbackDirection;
            ownerForward.y = 0f;
        }

        if (ownerForward.sqrMagnitude <= 0.0001f)
            ownerForward = Vector3.forward;

        ownerForward.Normalize();

        Vector3 ownerPosition = ownerRoot != null ? ownerRoot.position : Vector3.zero;
        Vector3 pullPoint = ownerPosition + ownerForward * targetFrontDistance;
        pullPoint.y = targetPosition.y;
        return pullPoint;
    }

    private static float CalculateProjectileCastRadius(Collider[] colliders)
    {
        if (colliders == null || colliders.Length == 0)
            return 0f;

        float radius = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider projectileCollider = colliders[i];
            if (projectileCollider == null)
                continue;

            float extent = GetColliderCastExtent(projectileCollider);
            if (extent > radius)
                radius = extent;
        }

        return radius;
    }

    private static float GetColliderCastExtent(Collider collider)
    {
        if (collider == null)
            return 0f;

        Vector3 scale = collider.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        switch (collider)
        {
            case SphereCollider sphere:
                return sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            case CapsuleCollider capsule:
                float radiusScale = capsule.direction switch
                {
                    0 => Mathf.Max(scale.y, scale.z),
                    1 => Mathf.Max(scale.x, scale.z),
                    _ => Mathf.Max(scale.x, scale.y)
                };

                float heightScale = capsule.direction switch
                {
                    0 => scale.x,
                    1 => scale.y,
                    _ => scale.z
                };

                float capsuleRadius = capsule.radius * radiusScale;
                float halfHeight = Mathf.Max(capsule.height * heightScale * 0.5f, capsuleRadius);
                return Mathf.Max(capsuleRadius, halfHeight);

            case BoxCollider box:
                Vector3 extents = Vector3.Scale(box.size * 0.5f, scale);
                return Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));

            default:
                Bounds bounds = collider.bounds;
                return Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
        }
    }

    private void DestroyProjectile()
    {
        if (PhotonNetwork.InRoom)
        {
            if (photonView != null && photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);

            return;
        }

        Destroy(gameObject);
    }
}
