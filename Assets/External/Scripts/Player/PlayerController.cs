using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

public enum MovementEffectType
{
    PullToPoint = 0,
    MovementLock = 1
}

public enum ImpulseControlReleaseMode
{
    DurationOnly = 0,
    UntilGrounded = 1,
    UntilGroundedOrTimeout = 2
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class PlayerController : MonoBehaviour
{
    private const float StepObstacleMaxUpDot = 0.2f;
    private const float StepObstacleMinFacingDot = 0.35f;
    private const float MinDetectedStepHeight = 0.02f;

    private enum MovementCommandType
    {
        PullToPoint,
        FollowTransform,
        LockMovement
    }

    private sealed class RuntimeMovementCommand
    {
        public int Id;
        public int Sequence;
        public int Priority;
        public float RemainingTime;
        public float Speed;
        public float StopDistance;
        public bool FaceDirection;
        public bool AllowVerticalMovement;
        public MovementCommandType Type;
        public Vector3 TargetPoint;
        public Transform FollowTarget;
        public Vector3 FollowOffset;
    }

    private sealed class RuntimeInputBlock
    {
        public int Id;
        public int Priority;
        public float EndTime;
    }

    private struct ImpulseControlState
    {
        public bool IsActive;
        public ImpulseControlReleaseMode ReleaseMode;
        public float RemainingTimeout;
        public float RemainingReleaseCheckDelay;
        public bool HasBeenAirborneSinceStart;
        public bool HasFacingDirection;
        public Vector3 FacingDirection;
    }

    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PhotonView photonView;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerInputSource inputSource;
    [SerializeField] private PlayerGroundSensor groundSensor;
    [Tooltip("Used to lock body yaw to camera while aiming heal sprayer (FPS-style). Auto-filled if empty.")]
    [SerializeField] private PlayerItemRunner itemRunner;

    [Header("Base Move")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6f;
    [SerializeField, Min(0.01f)] private float timeToMaxSpeed = 0.08f;
    [SerializeField, Min(0.01f)] private float deceleration = 80f;
    [SerializeField] private bool instantStopWhenNoInput = true;
    [SerializeField, Min(0f)] private float inputDeadZone = 0.05f;

    [Header("Ground Adaptation")]
    [SerializeField] private bool projectMovementOnGround = true;
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 60f;
    [SerializeField, Range(0f, 89f)] private float maxStairAngle = 75f;
    [SerializeField, Min(0.01f)] private float groundSnapProbeDistance = 0.6f;
    [SerializeField, Min(0f)] private float maxGroundSnapSpeed = 8f;
    [SerializeField, Min(0f)] private float groundSnapProbeStartOffset = 0.15f;
    [SerializeField, Min(0f)] private float groundSnapDisableDurationAfterJump = 0.15f;
    [SerializeField] private LayerMask groundSnapMask = ~0;
    [SerializeField] private LayerMask stairMask = 0;

    [Header("Step Climb")]
    [SerializeField] private bool enableStepClimb = true;
    [SerializeField, Min(0f)] private float maxStepHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float stepDetectionDistance = 0.3f;
    [SerializeField, Min(0f)] private float stepLowerProbeHeight = 0.05f;
    [SerializeField, Min(0f)] private float stepUpperProbeClearance = 0.05f;
    [SerializeField, Min(0f)] private float stepSurfaceSearchHeight = 0.1f;
    [SerializeField] private LayerMask stepDetectionMask = ~0;
    [SerializeField] private bool drawStepClimbGizmos = true;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;
    [SerializeField] private bool rotateTowardForcedMotion = true;

    [Header("Forced Motion")]
    [SerializeField, Min(0.01f)] private float forcedMotionAcceleration = 120f;
    [SerializeField] private bool instantStopOnHardLock = true;

    [Header("Jump")]
    [FormerlySerializedAs("allowJump")]
    [SerializeField] private bool enableJump = true;
    [SerializeField, Min(0f)] private float jumpVelocity = 7f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.12f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
    [SerializeField, Min(0f)] private float airborneDragMultiplier = 1f;
    [SerializeField, Min(0f)] private float fallSpeedMultiplier = 1f;
    [SerializeField] private bool clearDownwardVelocityOnJump = true;

    [Header("Impulse Knockback")]
    [SerializeField] private bool clearHorizontalVelocityBeforeImpulse = true;
    [SerializeField] private bool preserveVerticalVelocityOnImpulse = false;
    [SerializeField, Min(0f)] private float groundedImpulseReleaseMinLockTime = 0.08f;
    [SerializeField, Min(0f)] private float groundedImpulseReleaseHorizontalSpeed = 0.35f;
    [SerializeField, Min(0f)] private float groundedImpulseReleaseVerticalSpeed = 0.2f;

    [Header("Input Block")]
    [SerializeField] private bool debugInputBlocked;

    [Header("Options")]
    [SerializeField] private bool useCameraMainIfMissing = true;

    public Vector3 MoveDirectionWorld => desiredMoveDirection;
    public bool IsMoving => hasMoveInput;

    public bool HasLocalAuthority
    {
        get
        {
            if (!PhotonNetwork.InRoom)
                return true;

            return photonView != null && photonView.IsMine;
        }
    }

    public bool IsGrounded => groundSensor != null && groundSensor.IsGrounded;
    public bool IsAirborne => !IsGrounded;
    public bool JustLanded => groundSensor != null && groundSensor.JustLanded;
    public bool JustLeftGround => groundSensor != null && groundSensor.JustLeftGround;
    public Vector3 GroundNormal => groundSensor != null ? groundSensor.GroundNormal : Vector3.up;

    public bool IsInputBlocked
    {
        get
        {
            CleanupExpiredInputBlocks();
            return activeInputBlocks.Count > 0;
        }
    }

    private Vector2 moveInput;
    private Vector3 desiredMoveDirection;
    private Vector3 movementSurfaceNormal = Vector3.up;
    private bool hasMoveInput;
    private bool hasBufferedJumpRequest;
    private float jumpRequestExpireTime = float.NegativeInfinity;
    private bool jumpConsumedSinceLastGrounded;
    private float lastJumpTime = float.NegativeInfinity;
    private float minGroundDotProduct;
    private float minStairDotProduct;
    private Collider movementCollider;

    private readonly List<RuntimeMovementCommand> activeCommands = new();
    private int nextCommandId = 1;
    private int nextCommandSequence = 1;

    private readonly List<RuntimeInputBlock> activeInputBlocks = new();
    private int nextInputBlockId = 1;

    private ImpulseControlState impulseControl;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        photonView = GetComponent<PhotonView>();
        inputSource = GetComponent<PlayerInputSource>();
        groundSensor = GetComponent<PlayerGroundSensor>();
        movementCollider = GetComponent<CapsuleCollider>();

        if (movementCollider == null)
            movementCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        CacheGroundThresholds();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        if (inputSource == null)
            inputSource = GetComponent<PlayerInputSource>();

        if (groundSensor == null)
            groundSensor = GetComponent<PlayerGroundSensor>();

        if (movementCollider == null)
            movementCollider = GetComponent<CapsuleCollider>();

        if (movementCollider == null)
            movementCollider = GetComponent<Collider>();

        if (cameraTransform == null && useCameraMainIfMissing && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (itemRunner == null)
            itemRunner = GetComponent<PlayerItemRunner>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezeRotationY;
    }

    private void OnValidate()
    {
        CacheGroundThresholds();
    }

    private void Update()
    {
        CleanupExpiredInputBlocks();
        ReadInput();
    }

    private void FixedUpdate()
    {
        if (!HasLocalAuthority)
            return;

        if (groundSensor != null)
        {
            groundSensor.RefreshGrounding(Time.fixedDeltaTime);
        }

        RefreshMovementSurfaceState();
        RefreshJumpState();
        TickMovementCommands(Time.fixedDeltaTime);
        TickImpulseControl(Time.fixedDeltaTime);

        RuntimeMovementCommand activeCommand = GetHighestPriorityCommand();
        TryConsumeJumpRequest(activeCommand);
        ApplyJumpPhysicsModifiers(Time.fixedDeltaTime, activeCommand);

        if (impulseControl.IsActive)
        {
            UpdateImpulseLockRotation();
            return;
        }

        if (activeCommand != null)
        {
            UpdateForcedMovement(activeCommand);
            UpdateForcedRotation(activeCommand);
            return;
        }

        UpdateBaseMovement();
        TryStepClimb();

        if (ShouldLockFacingToCameraForHealSprayerAim())
            UpdateFacingToCameraHorizontal();
        else
            UpdateBaseRotation();
    }

    private void ReadInput()
    {
        if (!HasLocalAuthority)
        {
            moveInput = Vector2.zero;
            desiredMoveDirection = Vector3.zero;
            hasMoveInput = false;
            ClearBufferedJumpRequest();
            return;
        }

        if (IsInputBlocked)
        {
            moveInput = Vector2.zero;
            desiredMoveDirection = Vector3.zero;
            hasMoveInput = false;
            ClearBufferedJumpRequest();
            return;
        }

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
        desiredMoveDirection = CalculateMoveDirection(moveInput, movementSurfaceNormal);

        BufferJumpRequestIfPressed(ReadJumpPressedThisFrame());
    }

    private Vector3 CalculateMoveDirection(Vector2 input, Vector3 planeNormal)
    {
        if (input.sqrMagnitude <= 0f)
            return Vector3.zero;

        Vector3 movementPlaneNormal = GetMovementPlaneNormal(planeNormal);
        Vector3 forwardReference = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 rightReference = cameraTransform != null ? cameraTransform.right : Vector3.right;

        Vector3 forward = ProjectDirectionOnPlane(forwardReference, movementPlaneNormal);
        Vector3 right = ProjectDirectionOnPlane(rightReference, movementPlaneNormal);

        if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 direction = forward * input.y + right * input.x;
        return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
    }

    private void UpdateBaseMovement()
    {
        Vector3 movePlaneNormal = GetMovementPlaneNormal(movementSurfaceNormal);
        Vector3 targetHorizontalVelocity = hasMoveInput
            ? desiredMoveDirection * maxMoveSpeed
            : Vector3.zero;

        float acceleration = maxMoveSpeed / Mathf.Max(0.0001f, timeToMaxSpeed);
        float appliedDeceleration = deceleration;

        if (IsAirborne)
        {
            appliedDeceleration *= Mathf.Max(0f, airborneDragMultiplier);
        }

        ApplyTargetPlanarVelocity(
            targetHorizontalVelocity,
            movePlaneNormal,
            hasMoveInput ? acceleration : appliedDeceleration,
            !hasMoveInput && instantStopWhenNoInput);
    }

    private void UpdateBaseRotation()
    {
        if (!hasMoveInput || desiredMoveDirection.sqrMagnitude <= 0.0001f)
            return;

        RotateToward(desiredMoveDirection);
    }

    private bool ShouldLockFacingToCameraForHealSprayerAim()
    {
        return itemRunner != null && itemRunner.IsAimingHealSprayer();
    }

    private void UpdateFacingToCameraHorizontal()
    {
        Transform cam = cameraTransform;
        if (cam == null && useCameraMainIfMissing && Camera.main != null)
            cam = Camera.main.transform;

        if (cam == null)
            return;

        Vector3 forward = cam.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        RotateToward(forward.normalized);
    }

    private void TryStepClimb()
    {
        if (!CanAttemptStepClimb())
            return;

        if (!TryGetStepClimbProbeData(out StepClimbProbeData probeData))
            return;

        if (!Physics.Raycast(
                probeData.LowerOrigin,
                probeData.MoveDirection,
                out RaycastHit lowerHit,
                stepDetectionDistance,
                stepDetectionMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (IsSelfCollider(lowerHit.collider))
            return;

        Vector3 lowerHitNormal = lowerHit.normal.normalized;
        float lowerHitUpDot = Vector3.Dot(lowerHitNormal, Vector3.up);
        if (lowerHitUpDot >= GetMinGroundDot(lowerHit.collider.gameObject.layer))
            return;

        if (lowerHitUpDot > StepObstacleMaxUpDot)
            return;

        float obstacleFacingDot = Vector3.Dot(-probeData.MoveDirection, lowerHitNormal);
        if (obstacleFacingDot < StepObstacleMinFacingDot)
            return;

        if (Physics.Raycast(
                probeData.UpperOrigin,
                probeData.MoveDirection,
                stepDetectionDistance,
                stepDetectionMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (!Physics.Raycast(
                probeData.SurfaceProbeOrigin(lowerHit.distance),
                Vector3.down,
                out RaycastHit topHit,
                maxStepHeight + stepSurfaceSearchHeight + stepUpperProbeClearance,
                stepDetectionMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (IsSelfCollider(topHit.collider))
            return;

        float topSurfaceDot = Vector3.Dot(topHit.normal.normalized, Vector3.up);
        if (topSurfaceDot < GetMinGroundDot(topHit.collider.gameObject.layer))
            return;

        float stepHeightDelta = topHit.point.y - probeData.Bounds.min.y;
        if (stepHeightDelta <= MinDetectedStepHeight || stepHeightDelta > maxStepHeight)
            return;

        if (stepHeightDelta <= lowerHit.point.y - probeData.Bounds.min.y)
            return;

        Vector3 position = rb.position;
        position.y += stepHeightDelta + 0.01f;
        rb.MovePosition(position);
    }

    private bool ReadJumpPressedThisFrame()
    {
        if (inputSource != null)
            return inputSource.JumpPressedThisFrame;

        return Input.GetKeyDown(KeyCode.Space);
    }

    private void BufferJumpRequestIfPressed(bool jumpPressedThisFrame)
    {
        if (!enableJump || !jumpPressedThisFrame)
            return;

        hasBufferedJumpRequest = true;
        jumpRequestExpireTime = Time.time + jumpBufferTime;
    }

    private void RefreshJumpState()
    {
        ExpireBufferedJumpRequest();

        if (!enableJump || rb == null)
            return;

        if (JustLanded)
        {
            jumpConsumedSinceLastGrounded = false;
        }
    }

    private void ExpireBufferedJumpRequest()
    {
        if (!hasBufferedJumpRequest)
            return;

        if (Time.time <= jumpRequestExpireTime)
            return;

        ClearBufferedJumpRequest();
    }

    private void ClearBufferedJumpRequest()
    {
        hasBufferedJumpRequest = false;
        jumpRequestExpireTime = float.NegativeInfinity;
    }

    private void TryConsumeJumpRequest(RuntimeMovementCommand activeCommand)
    {
        if (!hasBufferedJumpRequest)
            return;

        if (!CanExecuteJump(activeCommand))
            return;

        ExecuteJump();
    }

    private bool CanExecuteJump(RuntimeMovementCommand activeCommand)
    {
        if (!enableJump || rb == null)
            return false;

        if (impulseControl.IsActive || activeCommand != null)
            return false;

        if (IsGrounded)
            return true;

        if (jumpConsumedSinceLastGrounded || groundSensor == null)
            return false;

        return groundSensor.TimeSinceGrounded <= coyoteTime;
    }

    private void ExecuteJump()
    {
        Vector3 velocity = rb.linearVelocity;

        if (clearDownwardVelocityOnJump && velocity.y < 0f)
        {
            velocity.y = 0f;
        }

        velocity.y = Mathf.Max(velocity.y, jumpVelocity);
        rb.linearVelocity = velocity;

        jumpConsumedSinceLastGrounded = true;
        lastJumpTime = Time.time;
        ClearBufferedJumpRequest();
    }

    private void ApplyJumpPhysicsModifiers(float deltaTime, RuntimeMovementCommand activeCommand)
    {
        if (!enableJump || rb == null || IsGrounded || !jumpConsumedSinceLastGrounded)
            return;

        if (impulseControl.IsActive || activeCommand != null)
            return;

        if (Mathf.Approximately(fallSpeedMultiplier, 1f))
            return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y >= 0f)
            return;

        velocity += Physics.gravity * (fallSpeedMultiplier - 1f) * deltaTime;
        rb.linearVelocity = velocity;
    }

    private void RefreshMovementSurfaceState()
    {
        movementSurfaceNormal = GroundNormal;

        if (movementSurfaceNormal.sqrMagnitude > 0.0001f)
        {
            movementSurfaceNormal.Normalize();
        }
        else
        {
            movementSurfaceNormal = Vector3.up;
        }

        if (IsGrounded)
        {
            desiredMoveDirection = CalculateMoveDirection(moveInput, movementSurfaceNormal);
            return;
        }

        if (TrySnapToGround(out Vector3 snappedGroundNormal))
        {
            movementSurfaceNormal = snappedGroundNormal;
        }
        else
        {
            movementSurfaceNormal = Vector3.up;
        }

        desiredMoveDirection = CalculateMoveDirection(moveInput, movementSurfaceNormal);
    }

    private bool TrySnapToGround(out Vector3 snappedGroundNormal)
    {
        snappedGroundNormal = Vector3.up;

        if (rb == null || IsGrounded || groundSensor == null)
            return false;

        if (groundSensor.TimeSinceGrounded > Time.fixedDeltaTime * 2f)
            return false;

        if (Time.time < lastJumpTime + groundSnapDisableDurationAfterJump)
            return false;

        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (planarVelocity.magnitude > maxGroundSnapSpeed)
            return false;

        Vector3 rayOrigin = rb.worldCenterOfMass + Vector3.up * groundSnapProbeStartOffset;
        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundSnapProbeDistance + groundSnapProbeStartOffset,
                groundSnapMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        Vector3 hitNormal = hit.normal.normalized;
        if (Vector3.Dot(hitNormal, Vector3.up) < GetMinGroundDot(hit.collider.gameObject.layer))
            return false;

        float separatingSpeed = Vector3.Dot(velocity, hitNormal);
        if (separatingSpeed > 0f)
        {
            rb.linearVelocity = velocity - hitNormal * separatingSpeed;
        }

        snappedGroundNormal = hitNormal;
        return true;
    }

    private void UpdateForcedMovement(RuntimeMovementCommand command)
    {
        Vector3 movePlaneNormal = GetMovementPlaneNormal(movementSurfaceNormal);

        switch (command.Type)
        {
            case MovementCommandType.LockMovement:
                ApplyTargetPlanarVelocity(Vector3.zero, movePlaneNormal, forcedMotionAcceleration, instantStopOnHardLock);
                break;

            case MovementCommandType.PullToPoint:
            case MovementCommandType.FollowTransform:
                if (!TryGetCommandTargetPoint(command, out Vector3 targetPoint))
                {
                    RemoveCommand(command.Id);
                    ApplyTargetPlanarVelocity(Vector3.zero, movePlaneNormal, forcedMotionAcceleration, true);
                    return;
                }

                Vector3 toTarget = targetPoint - rb.position;
                bool shouldUseHorizontalStopDistanceOnly =
                    command.AllowVerticalMovement &&
                    IsGrounded &&
                    targetPoint.y <= rb.position.y;

                Vector3 stopDistanceVector = shouldUseHorizontalStopDistanceOnly
                    ? Vector3.ProjectOnPlane(toTarget, Vector3.up)
                    : toTarget;

                if (!command.AllowVerticalMovement)
                    toTarget = Vector3.ProjectOnPlane(toTarget, movePlaneNormal);

                if (stopDistanceVector.sqrMagnitude <= command.StopDistance * command.StopDistance)
                {
                    if (command.AllowVerticalMovement)
                        ApplyTargetVelocity(Vector3.zero, forcedMotionAcceleration, true);
                    else
                        ApplyTargetPlanarVelocity(Vector3.zero, movePlaneNormal, forcedMotionAcceleration, true);
                }
                else
                {
                    if (command.AllowVerticalMovement)
                    {
                        ApplyTargetVelocity(
                            toTarget.normalized * command.Speed,
                            forcedMotionAcceleration,
                            false);
                    }
                    else
                    {
                        ApplyTargetPlanarVelocity(
                            toTarget.normalized * command.Speed,
                            movePlaneNormal,
                            forcedMotionAcceleration,
                            false);
                    }
                }
                break;
        }
    }

    private void UpdateForcedRotation(RuntimeMovementCommand command)
    {
        if (!rotateTowardForcedMotion || !command.FaceDirection)
            return;

        Vector3 direction = GetCommandFacingDirection(command);
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        RotateToward(direction);
    }

    private void TickImpulseControl(float deltaTime)
    {
        if (!impulseControl.IsActive)
            return;

        if (IsAirborne)
        {
            impulseControl.HasBeenAirborneSinceStart = true;
        }

        if (impulseControl.RemainingReleaseCheckDelay > 0f)
        {
            impulseControl.RemainingReleaseCheckDelay -= deltaTime;
        }

        switch (impulseControl.ReleaseMode)
        {
            case ImpulseControlReleaseMode.DurationOnly:
                impulseControl.RemainingTimeout -= deltaTime;
                if (impulseControl.RemainingTimeout <= 0f)
                {
                    ClearImpulseControl();
                }
                break;

            case ImpulseControlReleaseMode.UntilGrounded:
                if (CanReleaseGroundedImpulseControl())
                {
                    ClearImpulseControl();
                }
                break;

            case ImpulseControlReleaseMode.UntilGroundedOrTimeout:
                impulseControl.RemainingTimeout -= deltaTime;

                if (CanReleaseGroundedImpulseControl())
                {
                    ClearImpulseControl();
                }
                else if (impulseControl.RemainingTimeout <= 0f)
                {
                    ClearImpulseControl();
                }
                break;
        }
    }

    private bool CanReleaseGroundedImpulseControl()
    {
        if (!IsGrounded)
            return false;

        if (impulseControl.RemainingReleaseCheckDelay > 0f)
            return false;

        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector2 horizontalVelocity = new Vector2(velocity.x, velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;
        float verticalSpeed = Mathf.Abs(velocity.y);

        bool wasEffectivelyKnockedIntoAir = impulseControl.HasBeenAirborneSinceStart;
        bool horizontalSettled = horizontalSpeed <= groundedImpulseReleaseHorizontalSpeed;
        bool verticalSettled = verticalSpeed <= groundedImpulseReleaseVerticalSpeed;

        if (wasEffectivelyKnockedIntoAir)
            return horizontalSettled && verticalSettled;

        return horizontalSettled;
    }

    private void UpdateImpulseLockRotation()
    {
        if (!rotateTowardForcedMotion || !impulseControl.HasFacingDirection)
            return;

        RotateToward(impulseControl.FacingDirection);
    }

    private void ClearImpulseControl()
    {
        impulseControl = default;
    }

    private Vector3 GetCommandFacingDirection(RuntimeMovementCommand command)
    {
        switch (command.Type)
        {
            case MovementCommandType.PullToPoint:
            case MovementCommandType.FollowTransform:
                if (!TryGetCommandTargetPoint(command, out Vector3 targetPoint))
                    return Vector3.zero;

                Vector3 dir = targetPoint - rb.position;
                dir.y = 0f;
                return dir;

            default:
                return Vector3.zero;
        }
    }

    private void RotateToward(Vector3 worldDirection)
    {
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(nextRotation);
    }

    private void ApplyTargetPlanarVelocity(
        Vector3 targetPlanarVelocity,
        Vector3 planeNormal,
        float acceleration,
        bool snapToZeroImmediately)
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 effectivePlaneNormal = GetMovementPlaneNormal(planeNormal);
        Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(velocity, effectivePlaneNormal);

        if (targetPlanarVelocity.sqrMagnitude <= 0.0001f && snapToZeroImmediately)
        {
            rb.linearVelocity = velocity - currentPlanarVelocity;
            return;
        }

        Vector3 nextPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            targetPlanarVelocity,
            Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);

        Vector3 velocityDelta = nextPlanarVelocity - currentPlanarVelocity;
        Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;

        rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
    }

    private void ApplyTargetVelocity(
        Vector3 targetVelocity,
        float acceleration,
        bool snapToZeroImmediately)
    {
        Vector3 velocity = rb.linearVelocity;

        if (targetVelocity.sqrMagnitude <= 0.0001f && snapToZeroImmediately)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 nextVelocity = Vector3.MoveTowards(
            velocity,
            targetVelocity,
            Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);

        Vector3 velocityDelta = nextVelocity - velocity;
        Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;

        rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
    }

    private Vector3 GetMovementPlaneNormal(Vector3 planeNormal)
    {
        if (!projectMovementOnGround)
            return Vector3.up;

        if (planeNormal.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return planeNormal.normalized;
    }

    private Vector3 ProjectDirectionOnPlane(Vector3 direction, Vector3 normal)
    {
        Vector3 projected = direction - normal * Vector3.Dot(direction, normal);
        if (projected.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return projected.normalized;
    }

    private bool CanAttemptStepClimb()
    {
        if (!enableStepClimb || rb == null || movementCollider == null)
            return false;

        if (!IsGrounded || !hasMoveInput || desiredMoveDirection.sqrMagnitude <= 0.0001f)
            return false;

        if (Time.time < lastJumpTime + groundSnapDisableDurationAfterJump)
            return false;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        return planarVelocity.sqrMagnitude > 0.01f;
    }

    private bool IsSelfCollider(Collider targetCollider)
    {
        if (targetCollider == null)
            return false;

        if (targetCollider == movementCollider)
            return true;

        if (targetCollider.attachedRigidbody != null && targetCollider.attachedRigidbody == rb)
            return true;

        return targetCollider.transform.IsChildOf(transform) || transform.IsChildOf(targetCollider.transform);
    }

    private bool TryGetStepClimbProbeData(out StepClimbProbeData probeData)
    {
        probeData = default;

        if (movementCollider == null)
            return false;

        Vector3 moveDirection = Vector3.ProjectOnPlane(desiredMoveDirection, Vector3.up);
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return false;

        moveDirection.Normalize();

        Bounds bounds = movementCollider.bounds;
        float lowerProbeY = bounds.min.y + Mathf.Max(0f, stepLowerProbeHeight);
        Vector3 lowerOrigin = new Vector3(bounds.center.x, lowerProbeY, bounds.center.z);
        Vector3 upperOrigin = lowerOrigin + Vector3.up * (maxStepHeight + stepUpperProbeClearance);
        float surfaceProbeVerticalOffset = maxStepHeight + stepSurfaceSearchHeight;
        float surfaceProbeDistance = maxStepHeight + stepSurfaceSearchHeight + stepUpperProbeClearance;

        probeData = new StepClimbProbeData(
            bounds,
            moveDirection,
            lowerOrigin,
            upperOrigin,
            surfaceProbeVerticalOffset,
            surfaceProbeDistance);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawStepClimbGizmos || !enableStepClimb)
            return;

        Collider gizmoCollider = movementCollider != null ? movementCollider : GetComponent<Collider>();
        if (gizmoCollider == null)
            return;

        Collider cachedCollider = movementCollider;
        movementCollider = gizmoCollider;

        Vector3 cachedDesiredMoveDirection = desiredMoveDirection;
        if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
        {
            desiredMoveDirection = transform.forward;
        }

        if (!TryGetStepClimbProbeData(out StepClimbProbeData probeData))
        {
            desiredMoveDirection = cachedDesiredMoveDirection;
            movementCollider = cachedCollider;
            return;
        }

        float forwardDistance = stepDetectionDistance;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(probeData.LowerOrigin, probeData.LowerOrigin + probeData.MoveDirection * forwardDistance);
        Gizmos.DrawWireSphere(probeData.LowerOrigin, 0.03f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(probeData.UpperOrigin, probeData.UpperOrigin + probeData.MoveDirection * forwardDistance);
        Gizmos.DrawWireSphere(probeData.UpperOrigin, 0.03f);

        Gizmos.color = Color.yellow;
        Vector3 surfaceProbeOrigin = probeData.SurfaceProbeOrigin(stepDetectionDistance * 0.5f);
        Gizmos.DrawLine(surfaceProbeOrigin, surfaceProbeOrigin + Vector3.down * probeData.SurfaceProbeDistance);
        Gizmos.DrawWireSphere(surfaceProbeOrigin, 0.03f);

        desiredMoveDirection = cachedDesiredMoveDirection;
        movementCollider = cachedCollider;
    }

    private readonly struct StepClimbProbeData
    {
        public readonly Bounds Bounds;
        public readonly Vector3 MoveDirection;
        public readonly Vector3 LowerOrigin;
        public readonly Vector3 UpperOrigin;
        public readonly float SurfaceProbeVerticalOffset;
        public readonly float SurfaceProbeDistance;

        public StepClimbProbeData(
            Bounds bounds,
            Vector3 moveDirection,
            Vector3 lowerOrigin,
            Vector3 upperOrigin,
            float surfaceProbeVerticalOffset,
            float surfaceProbeDistance)
        {
            Bounds = bounds;
            MoveDirection = moveDirection;
            LowerOrigin = lowerOrigin;
            UpperOrigin = upperOrigin;
            SurfaceProbeVerticalOffset = surfaceProbeVerticalOffset;
            SurfaceProbeDistance = surfaceProbeDistance;
        }

        public Vector3 SurfaceProbeOrigin(float lowerHitDistance)
        {
            return LowerOrigin +
                   MoveDirection * Mathf.Max(lowerHitDistance + 0.02f, 0f) +
                   Vector3.up * SurfaceProbeVerticalOffset;
        }
    }

    private float GetMinGroundDot(int layer)
    {
        return (stairMask.value & (1 << layer)) != 0
            ? minStairDotProduct
            : minGroundDotProduct;
    }

    private void CacheGroundThresholds()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        minStairDotProduct = Mathf.Cos(maxStairAngle * Mathf.Deg2Rad);
    }

    private void TickMovementCommands(float deltaTime)
    {
        for (int i = activeCommands.Count - 1; i >= 0; i--)
        {
            RuntimeMovementCommand command = activeCommands[i];
            command.RemainingTime -= deltaTime;

            bool invalidFollowTarget =
                command.Type == MovementCommandType.FollowTransform &&
                command.FollowTarget == null;

            bool expired = command.RemainingTime <= 0f;

            if (invalidFollowTarget || expired)
            {
                activeCommands.RemoveAt(i);
            }
        }
    }

    private RuntimeMovementCommand GetHighestPriorityCommand()
    {
        RuntimeMovementCommand selected = null;

        for (int i = 0; i < activeCommands.Count; i++)
        {
            RuntimeMovementCommand candidate = activeCommands[i];

            if (selected == null ||
                candidate.Priority > selected.Priority ||
                (candidate.Priority == selected.Priority && candidate.Sequence > selected.Sequence))
            {
                selected = candidate;
            }
        }

        return selected;
    }

    private bool TryGetCommandTargetPoint(RuntimeMovementCommand command, out Vector3 targetPoint)
    {
        switch (command.Type)
        {
            case MovementCommandType.PullToPoint:
                targetPoint = command.TargetPoint;
                return true;

            case MovementCommandType.FollowTransform:
                if (command.FollowTarget != null)
                {
                    targetPoint = command.FollowTarget.position + command.FollowOffset;
                    return true;
                }
                break;
        }

        targetPoint = Vector3.zero;
        return false;
    }

    private int AddCommand(RuntimeMovementCommand command)
    {
        command.Id = nextCommandId++;
        command.Sequence = nextCommandSequence++;
        activeCommands.Add(command);
        return command.Id;
    }

    private int AddPullToPointInternal(
        Vector3 worldPoint,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 150,
        bool faceDirection = true,
        bool allowVerticalMovement = false)
    {
        if (speed <= 0f || duration <= 0f)
            return -1;

        return AddCommand(new RuntimeMovementCommand
        {
            Type = MovementCommandType.PullToPoint,
            TargetPoint = worldPoint,
            Speed = speed,
            RemainingTime = duration,
            StopDistance = Mathf.Max(0f, stopDistance),
            Priority = priority,
            FaceDirection = faceDirection,
            AllowVerticalMovement = allowVerticalMovement,
        });
    }

    private int AddMovementLockInternal(
        float duration,
        int priority = 300)
    {
        if (duration <= 0f)
            return -1;

        return AddCommand(new RuntimeMovementCommand
        {
            Type = MovementCommandType.LockMovement,
            RemainingTime = duration,
            Priority = priority,
            FaceDirection = false,
        });
    }

    public void ApplyImpulseKnockback(
        Vector3 worldImpulse,
        ImpulseControlReleaseMode releaseMode,
        float releaseTimeout = 0f,
        bool faceDirection = true,
        bool clearExistingCommands = true)
    {
        if (worldImpulse.sqrMagnitude <= 0.0001f)
            return;

        if (clearExistingCommands)
        {
            ClearCommands();
        }

        Vector3 velocity = rb.linearVelocity;
        bool shouldWriteVelocity = false;

        if (clearHorizontalVelocityBeforeImpulse)
        {
            velocity.x = 0f;
            velocity.z = 0f;
            shouldWriteVelocity = true;
        }

        if (!preserveVerticalVelocityOnImpulse)
        {
            velocity.y = 0f;
            shouldWriteVelocity = true;
        }

        if (shouldWriteVelocity)
        {
            rb.linearVelocity = velocity;
        }

        rb.AddForce(worldImpulse, ForceMode.Impulse);

        Vector3 horizontalImpulse = new Vector3(worldImpulse.x, 0f, worldImpulse.z);

        impulseControl = new ImpulseControlState
        {
            IsActive = true,
            ReleaseMode = releaseMode,
            RemainingTimeout = Mathf.Max(0f, releaseTimeout),
            RemainingReleaseCheckDelay = groundedImpulseReleaseMinLockTime,
            HasBeenAirborneSinceStart = IsAirborne,
            HasFacingDirection = faceDirection && horizontalImpulse.sqrMagnitude > 0.0001f,
            FacingDirection = horizontalImpulse.sqrMagnitude > 0.0001f
                ? horizontalImpulse.normalized
                : Vector3.zero
        };
    }

    public int AddInputBlock(float duration, int priority = 300)
    {
        if (duration <= 0f)
            return -1;

        RuntimeInputBlock block = new RuntimeInputBlock
        {
            Id = nextInputBlockId++,
            Priority = priority,
            EndTime = Time.time + duration
        };

        activeInputBlocks.Add(block);
        RefreshInputBlockedState();
        return block.Id;
    }

    public bool RemoveInputBlock(int blockId)
    {
        for (int i = 0; i < activeInputBlocks.Count; i++)
        {
            if (activeInputBlocks[i].Id == blockId)
            {
                activeInputBlocks.RemoveAt(i);
                RefreshInputBlockedState();
                return true;
            }
        }

        return false;
    }

    public void ClearInputBlocks()
    {
        if (activeInputBlocks.Count == 0)
            return;

        activeInputBlocks.Clear();
        RefreshInputBlockedState();
    }

    private void CleanupExpiredInputBlocks()
    {
        bool removed = false;

        for (int i = activeInputBlocks.Count - 1; i >= 0; i--)
        {
            if (Time.time >= activeInputBlocks[i].EndTime)
            {
                activeInputBlocks.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            RefreshInputBlockedState();
        }
    }

    private void RefreshInputBlockedState()
    {
        debugInputBlocked = activeInputBlocks.Count > 0;
    }

    public void ApplyStun(float duration, int priority = 300)
    {
        if (duration <= 0f)
            return;

        AddInputBlock(duration, priority);
    }

    public int ApplyEffect(
        MovementEffectType effectType,
        Vector3 vectorValue,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 100,
        bool faceDirection = true,
        bool allowVerticalMovement = false)
    {
        switch (effectType)
        {
            case MovementEffectType.PullToPoint:
                return AddPullToPointInternal(
                    vectorValue,
                    speed,
                    duration,
                    stopDistance,
                    priority,
                    faceDirection,
                    allowVerticalMovement);

            case MovementEffectType.MovementLock:
                return AddMovementLockInternal(
                    duration,
                    priority);

            default:
                return -1;
        }
    }

    public int ApplyPullToPoint(
        Vector3 worldPoint,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 150,
        bool faceDirection = true,
        bool allowVerticalMovement = false)
    {
        return ApplyEffect(
            MovementEffectType.PullToPoint,
            worldPoint,
            speed,
            duration,
            stopDistance,
            priority,
            faceDirection,
            allowVerticalMovement);
    }

    public int ApplyMovementLock(
        float duration,
        int priority = 300)
    {
        return ApplyEffect(
            MovementEffectType.MovementLock,
            Vector3.zero,
            0f,
            duration,
            0f,
            priority,
            false);
    }

    public int ApplyFollowTransformLocal(
        Transform followTarget,
        Vector3 followOffset,
        float speed,
        float duration,
        float stopDistance = 0.1f,
        int priority = 200,
        bool faceDirection = true)
    {
        if (followTarget == null || speed <= 0f || duration <= 0f)
            return -1;

        return AddCommand(new RuntimeMovementCommand
        {
            Type = MovementCommandType.FollowTransform,
            FollowTarget = followTarget,
            FollowOffset = followOffset,
            Speed = speed,
            RemainingTime = duration,
            StopDistance = Mathf.Max(0f, stopDistance),
            Priority = priority,
            FaceDirection = faceDirection,
        });
    }

    public bool RemoveCommand(int commandId)
    {
        for (int i = 0; i < activeCommands.Count; i++)
        {
            if (activeCommands[i].Id == commandId)
            {
                activeCommands.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void ClearCommands()
    {
        activeCommands.Clear();
    }

    public void ClearAllRestrictions()
    {
        ClearCommands();
        ClearInputBlocks();
        ClearImpulseControl();
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
