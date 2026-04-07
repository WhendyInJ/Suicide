using UnityEngine;
using Photon.Pun;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private PlayerInputSource inputSource;

    [Header("Follow")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField, Min(0.1f)] private float distance = 4f;
    [SerializeField, Min(0.1f)] private float minDistance = 0.5f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.03f;

    [Header("Look")]
    [SerializeField] private float yawSpeed = 220f;
    [SerializeField] private float pitchSpeed = 160f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private bool invertY = false;

    [Header("Camera Collision")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Min(0f)] private float collisionBuffer = 0.05f;
    [SerializeField, Min(0f)] private float distanceSmoothSpeed = 20f;

    public float Yaw => yaw;
    public float Pitch => pitch;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private Vector3 currentPivotPosition;
    private Vector3 pivotVelocity;

    private void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        currentDistance = distance;

        // 여기만 추가
        if (followTarget == null)
        {
            FindLocalPlayerTarget();
        }

        if (followTarget != null)
        {
            currentPivotPosition = followTarget.position + pivotOffset;
        }
    }

    private void LateUpdate()
    {
        if (followTarget == null)
            return;

        UpdateLookAngles();
        UpdateCameraTransform();
    }

    private void FindLocalPlayerTarget()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (PlayerController player in players)
        {
            PhotonView pv = player.GetComponent<PhotonView>();

            if (pv != null && pv.IsMine)
            {
                followTarget = player.transform;
                return;
            }
        }

        Debug.LogWarning("로컬 플레이어를 찾지 못해서 CameraController의 followTarget이 비어 있습니다.");
    }

    private void UpdateLookAngles()
    {
        Vector2 lookInput = inputSource != null
            ? inputSource.LookInput
            : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        yaw += lookInput.x * yawSpeed * Time.unscaledDeltaTime;

        float pitchDelta = lookInput.y * pitchSpeed * Time.unscaledDeltaTime;
        pitch += invertY ? pitchDelta : -pitchDelta;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateCameraTransform()
    {
        Vector3 targetPivotPosition = followTarget.position + pivotOffset;

        currentPivotPosition = Vector3.SmoothDamp(
            currentPivotPosition,
            targetPivotPosition,
            ref pivotVelocity,
            followSmoothTime);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 cameraDirection = rotation * Vector3.back;

        float targetDistance = distance;

        if (Physics.SphereCast(
                currentPivotPosition,
                collisionRadius,
                cameraDirection,
                out RaycastHit hit,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Max(minDistance, hit.distance - collisionBuffer);
        }

        float lerpFactor = 1f - Mathf.Exp(-distanceSmoothSpeed * Time.unscaledDeltaTime);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, lerpFactor);

        Vector3 cameraPosition = currentPivotPosition + cameraDirection * currentDistance;
        transform.SetPositionAndRotation(cameraPosition, rotation);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    public void SetFollowTarget(Transform newTarget)
    {
        followTarget = newTarget;
    }
}