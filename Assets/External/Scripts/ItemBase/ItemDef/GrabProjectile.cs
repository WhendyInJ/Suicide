using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class GrabProjectile : MonoBehaviourPun
{
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

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
        int hitCount = projectileCastRadius > 0f
            ? Physics.SphereCastNonAlloc(
                start,
                projectileCastRadius,
                castDirection,
                hitBuffer,
                distance,
                hitMask,
                triggerInteraction)
            : Physics.RaycastNonAlloc(
                start,
                castDirection,
                hitBuffer,
                distance,
                hitMask,
                triggerInteraction);

        bool found = false;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null)
                continue;

            if (ShouldIgnoreCollider(hit.collider))
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
            Vector3 ownerForward = ownerRoot.forward;
            ownerForward.y = 0f;
            if (ownerForward.sqrMagnitude <= 0.0001f)
                ownerForward = direction;
            ownerForward.y = 0f;
            if (ownerForward.sqrMagnitude <= 0.0001f)
                ownerForward = Vector3.forward;
            ownerForward.Normalize();

            Vector3 pullPoint = ownerRoot.position + ownerForward * targetFrontDistance;
            pullPoint.y = targetReceiver.transform.position.y;

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

    private float CalculateProjectileCastRadius(Collider[] colliders)
    {
        if (colliders == null || colliders.Length == 0)
            return 0f;

        float radius = 0f;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider projectileCollider = colliders[i];
            if (projectileCollider == null)
                continue;

            Bounds bounds = projectileCollider.bounds;
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (extent > radius)
                radius = extent;
        }

        return radius;
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
