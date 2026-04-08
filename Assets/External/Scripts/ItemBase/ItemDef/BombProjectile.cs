using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(Rigidbody))]
public class BombProjectile : MonoBehaviourPun
{
    [Header("Runtime Debug")]
    [SerializeField] private bool initialized;
    [SerializeField] private bool hasExploded;
    [SerializeField] private float debugExplosionRadius;

    private Rigidbody rb;
    private Transform ownerRoot;

    private BombTrajectoryType trajectoryType;
    private float launchSpeed;
    private float additionalUpwardSpeed;
    private float maxLifetime;
    private LayerMask impactMask;
    private LayerMask targetMask;
    private float explosionRadius;
    private float stunDuration;
    private int stunPriority;
    private GameObject explosionEffectPrefab;

    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly HashSet<PlayerMovementEffectReceiver> uniqueTargets = new();
    private Collider[] projectileColliders;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        projectileColliders = GetComponentsInChildren<Collider>(true);

        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    public void InitializeAsOwner(
        Transform ownerRoot,
        Vector3 launchDirection,
        BombTrajectoryType trajectoryType,
        float launchSpeed,
        float additionalUpwardSpeed,
        float maxLifetime,
        LayerMask impactMask,
        LayerMask targetMask,
        float explosionRadius,
        float stunDuration,
        int stunPriority,
        GameObject explosionEffectPrefab)
    {
        if (PhotonNetwork.InRoom && photonView != null && !photonView.IsMine)
            return;

        this.ownerRoot = ownerRoot;
        this.trajectoryType = trajectoryType;
        this.launchSpeed = launchSpeed;
        this.additionalUpwardSpeed = additionalUpwardSpeed;
        this.maxLifetime = maxLifetime;
        this.impactMask = impactMask;
        this.targetMask = targetMask;
        this.explosionRadius = explosionRadius;
        this.stunDuration = stunDuration;
        this.stunPriority = stunPriority;
        this.explosionEffectPrefab = explosionEffectPrefab;

        debugExplosionRadius = explosionRadius;
        initialized = true;

        PrepareOwnerProjectilePhysics();
        IgnoreOwnerCollisions();
        Launch(launchDirection.normalized);

        CancelInvoke(nameof(ExplodeByLifetime));
        Invoke(nameof(ExplodeByLifetime), this.maxLifetime);
    }

    private void PrepareOwnerProjectilePhysics()
    {
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.detectCollisions = true;

        if (projectileColliders == null || projectileColliders.Length == 0)
            projectileColliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            if (projectileColliders[i] != null)
            {
                projectileColliders[i].enabled = true;
            }
        }
    }

    private void IgnoreOwnerCollisions()
    {
        if (ownerRoot == null)
            return;

        if (projectileColliders == null || projectileColliders.Length == 0)
            projectileColliders = GetComponentsInChildren<Collider>(true);

        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < projectileColliders.Length; i++)
        {
            Collider projectileCollider = projectileColliders[i];
            if (projectileCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider == null || ownerCollider == projectileCollider)
                    continue;

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    private void Launch(Vector3 launchDirection)
    {
        Vector3 velocity = launchDirection * launchSpeed;

        switch (trajectoryType)
        {
            case BombTrajectoryType.Straight:
                rb.useGravity = false;
                break;

            case BombTrajectoryType.Parabolic:
                rb.useGravity = true;
                velocity += Vector3.up * additionalUpwardSpeed;
                break;
        }

        rb.linearVelocity = velocity;
    }

    private void ExplodeByLifetime()
    {
        if (!CanOwnerExplode())
            return;

        Explode(transform.position);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!CanOwnerExplode())
            return;

        if (!IsInImpactMask(collision.gameObject.layer))
            return;

        Vector3 explodePoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        Explode(explodePoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanOwnerExplode())
            return;

        if (!IsInImpactMask(other.gameObject.layer))
            return;

        Explode(other.ClosestPoint(transform.position));
    }

    private bool CanOwnerExplode()
    {
        if (!initialized || hasExploded)
            return false;

        if (!PhotonNetwork.InRoom)
            return true;

        return photonView != null && photonView.IsMine;
    }

    private bool IsInImpactMask(int layer)
    {
        return (impactMask.value & (1 << layer)) != 0;
    }

    private void Explode(Vector3 explodePoint)
    {
        if (hasExploded)
            return;

        hasExploded = true;

        ApplyExplosionEffects(explodePoint);
        SpawnExplosionEffect(explodePoint);
        DestroyProjectile();
    }

    private void ApplyExplosionEffects(Vector3 explodePoint)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            explodePoint,
            explosionRadius,
            overlapBuffer,
            targetMask,
            QueryTriggerInteraction.Ignore);

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

            if (ownerRoot != null && receiver.transform.root == ownerRoot)
                continue;

            receiver.RequestStun(stunDuration, stunPriority);
        }
    }

    private void SpawnExplosionEffect(Vector3 explodePoint)
    {
        if (explosionEffectPrefab == null)
            return;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.Instantiate(
                explosionEffectPrefab.name,
                explodePoint,
                Quaternion.identity);
        }
        else
        {
            Instantiate(explosionEffectPrefab, explodePoint, Quaternion.identity);
        }
    }

    private void DestroyProjectile()
    {
        CancelInvoke(nameof(ExplodeByLifetime));

        if (PhotonNetwork.InRoom)
        {
            if (photonView != null && photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, debugExplosionRadius > 0f ? debugExplosionRadius : explosionRadius);
    }
}
