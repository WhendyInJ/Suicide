using System.Collections;
using UnityEngine;

public class NautilusGrabAbility : MonoBehaviour
{
    [Header("Targeting")]
    public Transform castOrigin;
    public float castRange = 14f;
    public LayerMask hitMask = ~0;
    public LayerMask structureMask;

    [Header("Movement")]
    public float pullSpeed = 18f;
    public float stopDistance = 1.2f;
    public float maxPullDuration = 1.2f;
    public float enemyFrontDistance = 1.5f;

    private Rigidbody rb;

    private void Reset()
    {
        ApplyDefaultMasks();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ApplyDefaultMasks();

        if (castOrigin == null)
            castOrigin = transform;
    }

    private void ApplyDefaultMasks()
    {
        if (structureMask.value == 0)
            structureMask = LayerMask.GetMask("Structure");
    }

    public IEnumerator Execute()
    {
        Ray ray = new Ray(castOrigin.position, castOrigin.forward);

        // Structure 레이어를 우선 판정해서 맞으면 항상 자기 자신을 끌어당긴다.
        if (Physics.Raycast(ray, out RaycastHit structureHit, castRange, structureMask, QueryTriggerInteraction.Ignore))
        {
            yield return PullSelf(structureHit.point);
            yield break;
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, castRange, hitMask, QueryTriggerInteraction.Ignore))
            yield break;

        Rigidbody enemyRb = hit.collider.attachedRigidbody;

        if (enemyRb != null && enemyRb.CompareTag("Enemy"))
        {
            yield return PullEnemy(enemyRb);
        }
    }

    private IEnumerator PullSelf(Vector3 targetPoint)
    {
        float elapsed = 0f;

        while (elapsed < maxPullDuration)
        {
            elapsed += Time.fixedDeltaTime;

            Vector3 dir = targetPoint - transform.position;
            dir.y = 0;

            if (dir.magnitude <= stopDistance)
                break;

            rb.linearVelocity = new Vector3(dir.normalized.x * pullSpeed, rb.linearVelocity.y, dir.normalized.z * pullSpeed);

            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    private IEnumerator PullEnemy(Rigidbody enemyRb)
    {
        float elapsed = 0f;

        while (elapsed < maxPullDuration)
        {
            elapsed += Time.fixedDeltaTime;

            Vector3 forward = castOrigin.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 pullPoint = transform.position + forward * enemyFrontDistance;
            Vector3 dir = pullPoint - enemyRb.position;
            dir.y = 0;

            if (dir.magnitude <= stopDistance)
                break;

            enemyRb.linearVelocity = new Vector3(dir.normalized.x * pullSpeed, enemyRb.linearVelocity.y, dir.normalized.z * pullSpeed);

            yield return new WaitForFixedUpdate();
        }

        enemyRb.linearVelocity = new Vector3(0, enemyRb.linearVelocity.y, 0);
    }
}