using System.Collections;
using UnityEngine;

public class NautilusGrabItem : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode castKey = KeyCode.Q;

    [Header("Targeting")]
    [SerializeField] private Transform castOrigin;
    [SerializeField] private float castRange = 14f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private LayerMask structureMask;

    [Header("Movement")]
    [SerializeField] private float pullSpeed = 18f;
    [SerializeField] private float stopDistance = 1.2f;
    [SerializeField] private float maxPullDuration = 1.2f;

    private Rigidbody rb;
    private Coroutine activePull;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (castOrigin == null)
            castOrigin = transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(castKey))
            TryCastGrab();
    }

    private void TryCastGrab()
    {
        if (activePull != null)
            return;

        Ray ray = new Ray(castOrigin.position, castOrigin.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, castRange, hitMask, QueryTriggerInteraction.Ignore))
            return;

        Rigidbody enemyRb = FindEnemyRigidbody(hit.collider);
        bool hitSelf = enemyRb != null && enemyRb.transform == transform;

        if (enemyRb != null && !hitSelf)
        {
            activePull = StartCoroutine(PullBothToMidpoint(enemyRb));
            return;
        }

        bool isStructure = (structureMask.value & (1 << hit.collider.gameObject.layer)) != 0;
        if (isStructure)
            activePull = StartCoroutine(PullSelfToPoint(hit.point));
    }

    private Rigidbody FindEnemyRigidbody(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        Rigidbody targetRb = hitCollider.attachedRigidbody;
        if (targetRb == null)
            return null;

        // "Player" 태그를 적 플레이어 판별 기준으로 사용
        if (!targetRb.CompareTag("Player"))
            return null;

        return targetRb;
    }

    private IEnumerator PullSelfToPoint(Vector3 targetPoint)
    {
        float elapsed = 0f;
        while (elapsed < maxPullDuration)
        {
            elapsed += Time.fixedDeltaTime;

            Vector3 toTarget = targetPoint - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= stopDistance)
                break;

            MoveRigidbody(rb, toTarget.normalized * pullSpeed);
            yield return new WaitForFixedUpdate();
        }

        StopHorizontalVelocity(rb);
        activePull = null;
    }

    private IEnumerator PullBothToMidpoint(Rigidbody enemyRb)
    {
        float elapsed = 0f;
        while (elapsed < maxPullDuration)
        {
            elapsed += Time.fixedDeltaTime;

            Vector3 midpoint = (transform.position + enemyRb.position) * 0.5f;

            Vector3 myDir = midpoint - transform.position;
            Vector3 enemyDir = midpoint - enemyRb.position;
            myDir.y = 0f;
            enemyDir.y = 0f;

            if (myDir.magnitude <= stopDistance || enemyDir.magnitude <= stopDistance)
                break;

            MoveRigidbody(rb, myDir.normalized * pullSpeed);
            MoveRigidbody(enemyRb, enemyDir.normalized * pullSpeed);
            yield return new WaitForFixedUpdate();
        }

        StopHorizontalVelocity(rb);
        StopHorizontalVelocity(enemyRb);
        activePull = null;
    }

    private void MoveRigidbody(Rigidbody targetRb, Vector3 horizontalVelocity)
    {
        if (targetRb == null)
            return;

        Vector3 current = targetRb.linearVelocity;
        targetRb.linearVelocity = new Vector3(horizontalVelocity.x, current.y, horizontalVelocity.z);
    }

    private void StopHorizontalVelocity(Rigidbody targetRb)
    {
        if (targetRb == null)
            return;

        Vector3 current = targetRb.linearVelocity;
        targetRb.linearVelocity = new Vector3(0f, current.y, 0f);
    }
}
