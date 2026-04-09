using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LeverHoldInteraction : NetworkHoldInteractionBase
{
    [Header("References")]
    [SerializeField] private Transform lever;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private MonoBehaviour[] triggerTargets;
    [SerializeField] private HoldInteractionKillPriorityRegistry killPriorityRegistry;

    [Header("Lever X Rotation")]
    [SerializeField] private float inactiveXRotation = -45f;
    [SerializeField] private float activeXRotation = 45f;

    [Header("Target Motion")]
    [SerializeField] private float activatedTargetWorldY = 5.1f;
    [SerializeField, Min(0.01f)] private float targetMoveSpeed = 12f;
    [SerializeField, Min(0f)] private float targetHoldDuration = 1f;

    [Header("Runtime Debug")]
    [SerializeField] private bool isActivated;
    [SerializeField] private float currentVisualProgress;
    [SerializeField] private bool isReturnAnimating;

    private Vector3 leverBaseLocalEuler;
    private float initialTargetWorldY;
    private double returnAnimationStartTime;
    private float returnAnimationFromProgress;
    private TargetMovePhase targetMovePhase;
    private double targetHoldStartTime;

    private enum TargetMovePhase
    {
        Idle = 0,
        MovingToActivated = 1,
        Holding = 2,
        Returning = 3
    }

    protected override void Awake()
    {
        if (lever == null)
            lever = transform;

        leverBaseLocalEuler = lever.localEulerAngles;
        if (moveTarget != null)
            initialTargetWorldY = moveTarget.position.y;

        base.Awake();
        ApplyIdleVisualImmediate();
    }

    protected override void ApplyIdleVisualImmediate()
    {
        isReturnAnimating = false;
        ApplyLeverProgress(GetStableProgress());
    }

    protected override void UpdateVisualState(float holdProgress, bool isInteracting, double networkTime)
    {
        UpdateMoveTargetPosition();

        if (isInteracting)
        {
            isReturnAnimating = false;

            float from = GetStableProgress();
            float to = isActivated ? 0f : 1f;
            ApplyLeverProgress(Mathf.Lerp(from, to, holdProgress));
            return;
        }

        if (isReturnAnimating)
        {
            float t = CancelReturnDuration > 0f
                ? Mathf.Clamp01((float)((networkTime - returnAnimationStartTime) / CancelReturnDuration))
                : 1f;

            ApplyLeverProgress(Mathf.Lerp(returnAnimationFromProgress, GetStableProgress(), t));

            if (t >= 1f)
            {
                isReturnAnimating = false;
                ApplyLeverProgress(GetStableProgress());
            }

            return;
        }

        ApplyLeverProgress(GetStableProgress());
    }

    protected override void OnInteractionStartedReplicated(int playerViewId, double startServerTime)
    {
        isReturnAnimating = false;
    }

    protected override void OnInteractionStoppedReplicated(int playerViewId, HoldInteractionStopReason stopReason, double stopServerTime)
    {
        if (stopReason == HoldInteractionStopReason.Completed &&
            killPriorityRegistry != null &&
            playerViewId > 0)
        {
            killPriorityRegistry.SetCompletingPlayerViewId(playerViewId);
        }

        if (stopReason == HoldInteractionStopReason.Completed)
        {
            isActivated = !isActivated;
            isReturnAnimating = false;
            BeginTargetMoveCycle();
            ApplyLeverProgress(GetStableProgress());
            TriggerAssignedTargets();
            return;
        }

        isReturnAnimating = true;
        returnAnimationStartTime = stopServerTime;
        returnAnimationFromProgress = currentVisualProgress;
    }

    private float GetStableProgress()
    {
        return isActivated ? 1f : 0f;
    }

    private void ApplyLeverProgress(float progress)
    {
        currentVisualProgress = Mathf.Clamp01(progress);

        if (lever == null)
            return;

        Vector3 euler = leverBaseLocalEuler;
        euler.x = Mathf.Lerp(inactiveXRotation, activeXRotation, currentVisualProgress);
        lever.localRotation = Quaternion.Euler(euler);
    }

    private void UpdateMoveTargetPosition()
    {
        if (moveTarget == null)
            return;

        switch (targetMovePhase)
        {
            case TargetMovePhase.Idle:
                MoveTargetTowardsY(initialTargetWorldY);
                break;

            case TargetMovePhase.MovingToActivated:
                if (MoveTargetTowardsY(activatedTargetWorldY))
                {
                    targetMovePhase = TargetMovePhase.Holding;
                    targetHoldStartTime = NetworkTime;
                }
                break;

            case TargetMovePhase.Holding:
                SnapTargetY(activatedTargetWorldY);
                if (NetworkTime >= targetHoldStartTime + targetHoldDuration)
                    targetMovePhase = TargetMovePhase.Returning;
                break;

            case TargetMovePhase.Returning:
                if (MoveTargetTowardsY(initialTargetWorldY))
                    targetMovePhase = TargetMovePhase.Idle;
                break;
        }
    }

    private void BeginTargetMoveCycle()
    {
        if (moveTarget == null)
            return;

        targetMovePhase = TargetMovePhase.MovingToActivated;
    }

    private bool MoveTargetTowardsY(float targetY)
    {
        Vector3 targetPosition = moveTarget.position;
        targetPosition.y = targetY;

        moveTarget.position = Vector3.MoveTowards(
            moveTarget.position,
            targetPosition,
            targetMoveSpeed * Time.deltaTime);

        return Mathf.Abs(moveTarget.position.y - targetY) <= 0.001f;
    }

    private void SnapTargetY(float targetY)
    {
        Vector3 position = moveTarget.position;
        position.y = targetY;
        moveTarget.position = position;
    }

    private void TriggerAssignedTargets()
    {
        if (!CanExecuteAuthoritativeTrigger || triggerTargets == null)
            return;

        HashSet<MonoBehaviour> invokedTargets = null;

        for (int i = 0; i < triggerTargets.Length; i++)
        {
            InvokeTriggerTarget(triggerTargets[i], ref invokedTargets);
        }
    }

    private void InvokeTriggerTarget(MonoBehaviour target, ref HashSet<MonoBehaviour> invokedTargets)
    {
        if (target == null)
            return;

        invokedTargets ??= new HashSet<MonoBehaviour>();
        if (!invokedTargets.Add(target))
            return;

        if (target is IInteractionTriggerTarget triggerTarget)
        {
            triggerTarget.TriggerFromInteraction(this);
            return;
        }

        Debug.LogWarning(
            $"[{nameof(LeverHoldInteraction)}:{name}] {target.GetType().Name} does not implement {nameof(IInteractionTriggerTarget)}.",
            target);
    }
}
