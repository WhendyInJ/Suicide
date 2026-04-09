using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LeverHoldInteraction : NetworkHoldInteractionBase
{
    [Header("References")]
    [SerializeField] private Transform lever;
    [SerializeField] private MonoBehaviour[] triggerTargets;

    [Header("Lever X Rotation")]
    [SerializeField] private float inactiveXRotation = -45f;
    [SerializeField] private float activeXRotation = 45f;

    [Header("Runtime Debug")]
    [SerializeField] private bool isActivated;
    [SerializeField] private float currentVisualProgress;
    [SerializeField] private bool isReturnAnimating;

    private Vector3 leverBaseLocalEuler;
    private double returnAnimationStartTime;
    private float returnAnimationFromProgress;

    protected override void Awake()
    {
        if (lever == null)
            lever = transform;

        leverBaseLocalEuler = lever.localEulerAngles;
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
        if (stopReason == HoldInteractionStopReason.Completed)
        {
            isActivated = !isActivated;
            isReturnAnimating = false;
            ApplyLeverProgress(GetStableProgress());
            TriggerAssignedTargets(playerViewId);
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

    private void TriggerAssignedTargets(int triggeringPlayerViewId)
    {
        if (!CanExecuteAuthoritativeTrigger || triggerTargets == null)
            return;

        HashSet<MonoBehaviour> invokedTargets = null;

        for (int i = 0; i < triggerTargets.Length; i++)
        {
            InvokeTriggerTarget(triggerTargets[i], triggeringPlayerViewId, ref invokedTargets);
        }
    }

    private void InvokeTriggerTarget(MonoBehaviour target, int triggeringPlayerViewId, ref HashSet<MonoBehaviour> invokedTargets)
    {
        if (target == null)
            return;

        invokedTargets ??= new HashSet<MonoBehaviour>();
        if (!invokedTargets.Add(target))
            return;

        if (target is IInteractionTriggerTarget triggerTarget)
        {
            triggerTarget.TriggerFromInteraction(this, triggeringPlayerViewId);
            return;
        }

        Debug.LogWarning(
            $"[{nameof(LeverHoldInteraction)}:{name}] {target.GetType().Name} does not implement {nameof(IInteractionTriggerTarget)}.",
            target);
    }
}
