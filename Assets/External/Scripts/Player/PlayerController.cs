using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerInputSource inputSource;

    [Header("Move")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6f;
    [SerializeField, Min(0.01f)] private float timeToMaxSpeed = 0.08f;
    [SerializeField, Min(0.01f)] private float deceleration = 80f;
    [SerializeField] private bool instantStopWhenNoInput = true;
    [SerializeField, Min(0f)] private float inputDeadZone = 0.05f;

    [Header("Rotate")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;

    [Header("Options")]
    [SerializeField] private bool useCameraMainIfMissing = true;

    public Vector3 MoveDirectionWorld => desiredMoveDirection;
    public bool IsMoving => hasMoveInput;

    private Vector2 moveInput;
    private Vector3 desiredMoveDirection;
    private bool hasMoveInput;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (cameraTransform == null && useCameraMainIfMissing && Camera.main != null)
            cameraTransform = Camera.main.transform;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        UpdateMovement();
        UpdateRotation();
    }

    private void ReadInput()
    {
        if (inputSource != null)
        {
            moveInput = inputSource.MoveInput;
        }
        else
        {
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        if (moveInput.sqrMagnitude < inputDeadZone * inputDeadZone)
            moveInput = Vector2.zero;

        hasMoveInput = moveInput.sqrMagnitude > 0f;
        desiredMoveDirection = CalculateMoveDirection(moveInput);
    }

    private Vector3 CalculateMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0f)
            return Vector3.zero;

        if (cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y).normalized;
        }

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * input.y + right * input.x;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
    }

    private void UpdateMovement()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        if (hasMoveInput)
        {
            Vector3 targetHorizontalVelocity = desiredMoveDirection * maxMoveSpeed;

            float acceleration = maxMoveSpeed / Mathf.Max(0.0001f, timeToMaxSpeed);

            Vector3 nextHorizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetHorizontalVelocity,
                acceleration * Time.fixedDeltaTime);

            Vector3 velocityDelta = nextHorizontalVelocity - horizontalVelocity;
            Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;

            rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
        }
        else
        {
            if (horizontalVelocity.sqrMagnitude <= 0.0001f)
                return;

            if (instantStopWhenNoInput)
            {
                rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
            }
            else
            {
                Vector3 nextHorizontalVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    Vector3.zero,
                    deceleration * Time.fixedDeltaTime);

                Vector3 velocityDelta = nextHorizontalVelocity - horizontalVelocity;
                Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;

                rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
            }
        }
    }

    private void UpdateRotation()
    {
        if (!hasMoveInput || desiredMoveDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(nextRotation);
    }

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    public void SetMoveSpeed(float newMoveSpeed)
    {
        maxMoveSpeed = Mathf.Max(0f, newMoveSpeed);
    }
}