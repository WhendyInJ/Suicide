using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ButtonHoldInteraction : NetworkHoldInteractionBase
{
    [Header("References")]
    [SerializeField] private Transform buttonTarget;
    [SerializeField] private MonoBehaviour[] triggerTargets;

    [Header("Legacy Target")]
    [SerializeField] private SpikePlatformGroupController controlledGroup;

    [Header("Button Motion")]
    [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, -0.05f, 0f);
    [SerializeField, Min(0.01f)] private float pressCycleDuration = 1f;

    [Header("Runtime Debug")]
    [SerializeField] private float currentVisualProgress;
    [SerializeField] private bool isReturnAnimating;
    [SerializeField] private bool isCompletionAnimating;

    private Vector3 defaultLocalPosition;
    private double animationStartTime;
    private float animationFromProgress;

    protected override void Awake()
    {
        if (buttonTarget == null)
            buttonTarget = transform;

        defaultLocalPosition = buttonTarget.localPosition;
        base.Awake();
        ApplyIdleVisualImmediate();
    }

    protected override float GetPostInteractionAnimationDuration()
    {
        return pressCycleDuration;
    }

    protected override void ApplyIdleVisualImmediate()
    {
        isReturnAnimating = false;
        isCompletionAnimating = false;
        ApplyButtonProgress(0f);
    }

    protected override void UpdateVisualState(float holdProgress, bool isInteracting, double networkTime)
    {
        if (isInteracting)
        {
            isReturnAnimating = false;
            isCompletionAnimating = false;
            ApplyButtonProgress(holdProgress);
            return;
        }

        if (isCompletionAnimating)
        {
            float t = pressCycleDuration > 0f
                ? Mathf.Clamp01((float)((networkTime - animationStartTime) / pressCycleDuration))
                : 1f;

            ApplyButtonProgress(1f - t);

            if (t >= 1f)
            {
                isCompletionAnimating = false;
                ApplyButtonProgress(0f);
            }

            return;
        }

        if (isReturnAnimating)
        {
            float t = CancelReturnDuration > 0f
                ? Mathf.Clamp01((float)((networkTime - animationStartTime) / CancelReturnDuration))
                : 1f;

            ApplyButtonProgress(Mathf.Lerp(animationFromProgress, 0f, t));

            if (t >= 1f)
            {
                isReturnAnimating = false;
                ApplyButtonProgress(0f);
            }

            return;
        }

        ApplyButtonProgress(0f);
    }

    protected override void OnInteractionStartedReplicated(int playerViewId, double startServerTime)
    {
        isReturnAnimating = false;
        isCompletionAnimating = false;
    }

    protected override void OnInteractionStoppedReplicated(int playerViewId, HoldInteractionStopReason stopReason, double stopServerTime)
    {
        if (stopReason == HoldInteractionStopReason.Completed)
        {
            isReturnAnimating = false;
            isCompletionAnimating = true;
            animationStartTime = stopServerTime;
            animationFromProgress = 1f;
            TriggerAssignedTargets();

            return;
        }

        if (currentVisualProgress <= 0.0001f)
        {
            ApplyIdleVisualImmediate();
            return;
        }

        isCompletionAnimating = false;
        isReturnAnimating = true;
        animationStartTime = stopServerTime;
        animationFromProgress = currentVisualProgress;
    }

    private void ApplyButtonProgress(float progress)
    {
        currentVisualProgress = Mathf.Clamp01(progress);

        if (buttonTarget != null)
        {
            buttonTarget.localPosition = Vector3.Lerp(
                defaultLocalPosition,
                defaultLocalPosition + pressedLocalOffset,
                currentVisualProgress);
        }
    }

    private void TriggerAssignedTargets()
    {
        if (!CanExecuteAuthoritativeTrigger)
            return;

        HashSet<MonoBehaviour> invokedTargets = null;

        InvokeTriggerTarget(controlledGroup, ref invokedTargets);

        if (triggerTargets == null)
            return;

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
            $"[{nameof(ButtonHoldInteraction)}:{name}] {target.GetType().Name} does not implement {nameof(IInteractionTriggerTarget)}.",
            target);
    }
}
