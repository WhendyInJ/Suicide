using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

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

    [Header("Base Move")]
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6f;
    [SerializeField, Min(0.01f)] private float timeToMaxSpeed = 0.08f;
    [SerializeField, Min(0.01f)] private float deceleration = 80f;
    [SerializeField] private bool instantStopWhenNoInput = true;
    [SerializeField, Min(0f)] private float inputDeadZone = 0.05f;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationSpeed = 1080f;
    [SerializeField] private bool rotateTowardForcedMotion = true;

    [Header("Forced Motion")]
    [SerializeField, Min(0.01f)] private float forcedMotionAcceleration = 120f;
    [SerializeField] private bool instantStopOnHardLock = true;

    [Header("Impulse Knockback")]
    [SerializeField] private bool clearHorizontalVelocityBeforeImpulse = true;
    [SerializeField] private bool preserveVerticalVelocityOnImpulse = false;

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
    private bool hasMoveInput;

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
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        if (inputSource == null)
            inputSource = GetComponent<PlayerInputSource>();

        if (groundSensor == null)
            groundSensor = GetComponent<PlayerGroundSensor>();

        if (cameraTransform == null && useCameraMainIfMissing && Camera.main != null)
            cameraTransform = Camera.main.transform;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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

        TickMovementCommands(Time.fixedDeltaTime);
        TickImpulseControl(Time.fixedDeltaTime);

        if (impulseControl.IsActive)
        {
            UpdateImpulseLockRotation();
            return;
        }

        RuntimeMovementCommand activeCommand = GetHighestPriorityCommand();

        if (activeCommand != null)
        {
            UpdateForcedMovement(activeCommand);
            UpdateForcedRotation(activeCommand);
            return;
        }

        UpdateBaseMovement();
        UpdateBaseRotation();
    }

    private void ReadInput()
    {
        if (!HasLocalAuthority)
        {
            moveInput = Vector2.zero;
            desiredMoveDirection = Vector3.zero;
            hasMoveInput = false;
            return;
        }

        if (IsInputBlocked)
        {
            moveInput = Vector2.zero;
            desiredMoveDirection = Vector3.zero;
            hasMoveInput = false;
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
        desiredMoveDirection = CalculateMoveDirection(moveInput);
    }

    private Vector3 CalculateMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0f)
            return Vector3.zero;

        if (cameraTransform == null)
            return new Vector3(input.x, 0f, input.y).normalized;

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

    private void UpdateBaseMovement()
    {
        Vector3 targetHorizontalVelocity = hasMoveInput
            ? desiredMoveDirection * maxMoveSpeed
            : Vector3.zero;

        float acceleration = maxMoveSpeed / Mathf.Max(0.0001f, timeToMaxSpeed);

        ApplyTargetHorizontalVelocity(
            targetHorizontalVelocity,
            hasMoveInput ? acceleration : deceleration,
            !hasMoveInput && instantStopWhenNoInput);
    }

    private void UpdateBaseRotation()
    {
        if (!hasMoveInput || desiredMoveDirection.sqrMagnitude <= 0.0001f)
            return;

        RotateToward(desiredMoveDirection);
    }

    private void UpdateForcedMovement(RuntimeMovementCommand command)
    {
        switch (command.Type)
        {
            case MovementCommandType.LockMovement:
                ApplyTargetHorizontalVelocity(Vector3.zero, forcedMotionAcceleration, instantStopOnHardLock);
                break;

            case MovementCommandType.PullToPoint:
            case MovementCommandType.FollowTransform:
                if (!TryGetCommandTargetPoint(command, out Vector3 targetPoint))
                {
                    RemoveCommand(command.Id);
                    ApplyTargetHorizontalVelocity(Vector3.zero, forcedMotionAcceleration, true);
                    return;
                }

                Vector3 toTarget = targetPoint - rb.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude <= command.StopDistance * command.StopDistance)
                {
                    ApplyTargetHorizontalVelocity(Vector3.zero, forcedMotionAcceleration, true);
                }
                else
                {
                    ApplyTargetHorizontalVelocity(
                        toTarget.normalized * command.Speed,
                        forcedMotionAcceleration,
                        false);
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
                if (IsGrounded && impulseControl.HasBeenAirborneSinceStart)
                {
                    ClearImpulseControl();
                }
                break;

            case ImpulseControlReleaseMode.UntilGroundedOrTimeout:
                impulseControl.RemainingTimeout -= deltaTime;

                if (IsGrounded && impulseControl.HasBeenAirborneSinceStart)
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

    private void ApplyTargetHorizontalVelocity(
        Vector3 targetHorizontalVelocity,
        float acceleration,
        bool snapToZeroImmediately)
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

        if (targetHorizontalVelocity.sqrMagnitude <= 0.0001f && snapToZeroImmediately)
        {
            rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
            return;
        }

        Vector3 nextHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);

        Vector3 velocityDelta = nextHorizontalVelocity - currentHorizontalVelocity;
        Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;

        rb.AddForce(requiredAcceleration, ForceMode.Acceleration);
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
        bool faceDirection = true)
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
        bool faceDirection = true)
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
                    faceDirection);

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
        bool faceDirection = true)
    {
        return ApplyEffect(
            MovementEffectType.PullToPoint,
            worldPoint,
            speed,
            duration,
            stopDistance,
            priority,
            faceDirection);
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