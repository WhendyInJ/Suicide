using System;
using UnityEngine;

public class PlayerGroundSensor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform probeOrigin;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float probeRadius = 0.25f;
    [SerializeField, Min(0.01f)] private float probeDistance = 0.35f;
    [SerializeField] private Vector3 probeOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 60f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    public bool IsGrounded { get; private set; }
    public bool WasGroundedLastStep { get; private set; }
    public bool JustLanded { get; private set; }
    public bool JustLeftGround { get; private set; }

    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public Collider GroundCollider { get; private set; }

    public float TimeSinceGrounded { get; private set; }
    public float TimeSinceUngrounded { get; private set; }

    public event Action Landed;
    public event Action LeftGround;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    private void Reset()
    {
        probeOrigin = transform;
    }

    public void RefreshGrounding(float deltaTime)
    {
        Transform originTransform = probeOrigin != null ? probeOrigin : transform;
        Vector3 sphereOrigin = originTransform.position + probeOffset;

        int hitCount = Physics.SphereCastNonAlloc(
            sphereOrigin,
            probeRadius,
            Vector3.down,
            hitBuffer,
            probeDistance,
            groundMask,
            triggerInteraction);

        bool foundGround = TryFindBestGround(hitCount, out RaycastHit bestHit);

        WasGroundedLastStep = IsGrounded;
        IsGrounded = foundGround;

        JustLanded = !WasGroundedLastStep && IsGrounded;
        JustLeftGround = WasGroundedLastStep && !IsGrounded;

        if (IsGrounded)
        {
            GroundNormal = bestHit.normal;
            GroundCollider = bestHit.collider;
            TimeSinceGrounded = 0f;
            TimeSinceUngrounded += deltaTime;
        }
        else
        {
            GroundNormal = Vector3.up;
            GroundCollider = null;
            TimeSinceGrounded += deltaTime;
            TimeSinceUngrounded = 0f;
        }

        if (JustLanded)
        {
            Landed?.Invoke();
        }

        if (JustLeftGround)
        {
            LeftGround?.Invoke();
        }
    }

    private bool TryFindBestGround(int hitCount, out RaycastHit bestHit)
    {
        bestHit = default;

        bool found = false;
        float bestDot = -1f;
        float minGroundDot = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null)
                continue;

            float upDot = Vector3.Dot(hit.normal.normalized, Vector3.up);
            if (upDot < minGroundDot)
                continue;

            if (!found || upDot > bestDot)
            {
                found = true;
                bestDot = upDot;
                bestHit = hit;
            }
        }

        return found;
    }

    private void OnDrawGizmosSelected()
    {
        Transform originTransform = probeOrigin != null ? probeOrigin : transform;
        if (originTransform == null)
            return;

        Vector3 sphereOrigin = originTransform.position + probeOffset;
        Vector3 endPoint = sphereOrigin + Vector3.down * probeDistance;

        Gizmos.color = IsGrounded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(sphereOrigin, probeRadius);
        Gizmos.DrawLine(sphereOrigin, endPoint);
        Gizmos.DrawWireSphere(endPoint, probeRadius);
    }
}