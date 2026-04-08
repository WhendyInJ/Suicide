using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ButtonHoldInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform buttonTarget;
    [SerializeField] private Canvas interactionCanvas;
    [SerializeField] private Image holdProgressImage;
    [SerializeField] private SpikePlatformGroupController controlledGroup;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Min(0.01f)] private float holdDuration = 1f;
    [SerializeField, Min(0.01f)] private float cancelReturnDuration = 0.2f;

    [Header("Button Motion")]
    [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, -0.05f, 0f);
    [SerializeField, Min(0.01f)] private float pressCycleDuration = 1f;

    private readonly HashSet<int> overlappingPlayerColliders = new();

    private Coroutine activeRoutine;
    private Vector3 defaultLocalPosition;
    private float currentFillAmount;

    private bool IsPlayerInRange => overlappingPlayerColliders.Count > 0;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        if (buttonTarget == null)
            buttonTarget = transform;

        defaultLocalPosition = buttonTarget.localPosition;
        ApplyStateImmediate(defaultLocalPosition, 0f);
        SetCanvasVisible(false);
    }

    private void Update()
    {
        if (!IsPlayerInRange)
            return;

        if (activeRoutine != null)
            return;

        if (controlledGroup != null && controlledGroup.IsTransitionRunning)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        activeRoutine = StartCoroutine(CoHoldAndPress());
    }

    private void OnTriggerEnter(Collider other)
    {
        TryEnterRange(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryEnterRange(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryResolvePlayerCollider(other, out int colliderId))
            return;

        if (!overlappingPlayerColliders.Remove(colliderId))
            return;

        if (!IsPlayerInRange)
        {
            CancelCurrentRoutine();
            StartReturnToIdle();
            SetCanvasVisible(false);
        }
    }

    private void OnDisable()
    {
        CancelCurrentRoutine();
        ApplyStateImmediate(defaultLocalPosition, 0f);
        SetCanvasVisible(false);
        overlappingPlayerColliders.Clear();
    }

    private void TryEnterRange(Collider other)
    {
        if (!TryResolvePlayerCollider(other, out int colliderId))
            return;

        if (!overlappingPlayerColliders.Add(colliderId))
            return;

        SetCanvasVisible(true);
        ApplyStateImmediate(buttonTarget.localPosition, currentFillAmount);
    }

    private bool TryResolvePlayerCollider(Collider other, out int colliderId)
    {
        colliderId = 0;

        if (other == null)
            return false;

        PlayerInputSource inputSource = other.GetComponentInParent<PlayerInputSource>();
        if (inputSource != null)
        {
            if (!inputSource.HasLocalAuthority)
                return false;

            colliderId = other.GetInstanceID();
            return true;
        }

        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        if (playerController == null || !playerController.HasLocalAuthority)
            return false;

        colliderId = other.GetInstanceID();
        return true;
    }

    private void StartReturnToIdle()
    {
        bool needsPositionReset = (buttonTarget.localPosition - defaultLocalPosition).sqrMagnitude > 0.000001f;
        bool needsFillReset = !Mathf.Approximately(currentFillAmount, 0f);

        if (!needsPositionReset && !needsFillReset)
            return;

        activeRoutine = StartCoroutine(CoAnimateToState(buttonTarget.localPosition, defaultLocalPosition, currentFillAmount, 0f, cancelReturnDuration));
    }

    private void CancelCurrentRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private IEnumerator CoHoldAndPress()
    {
        float startFill = currentFillAmount;
        float elapsed = 0f;
        Vector3 startPosition = buttonTarget.localPosition;
        Vector3 pressedPosition = defaultLocalPosition + pressedLocalOffset;

        while (elapsed < holdDuration)
        {
            if (!IsPlayerInRange || !Input.GetKey(interactKey))
            {
                yield return CoAnimateToState(buttonTarget.localPosition, defaultLocalPosition, currentFillAmount, 0f, cancelReturnDuration);
                activeRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            Vector3 position = Vector3.Lerp(startPosition, pressedPosition, t);
            float fill = Mathf.Lerp(startFill, 1f, t);
            ApplyStateImmediate(position, fill);
            yield return null;
        }

        ApplyStateImmediate(pressedPosition, 1f);

        if (controlledGroup != null)
            yield return controlledGroup.CoToggleState();

        yield return CoAnimateToState(pressedPosition, defaultLocalPosition, 1f, 0f, pressCycleDuration);
        activeRoutine = null;
    }

    private IEnumerator CoAnimateToState(
        Vector3 fromPosition,
        Vector3 toPosition,
        float fromFill,
        float toFill,
        float duration)
    {
        if (duration <= 0f)
        {
            ApplyStateImmediate(toPosition, toFill);
            activeRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(fromPosition, toPosition, t);
            float fill = Mathf.Lerp(fromFill, toFill, t);
            ApplyStateImmediate(position, fill);
            yield return null;
        }

        ApplyStateImmediate(toPosition, toFill);
    }

    private void ApplyStateImmediate(Vector3 localPosition, float fillAmount)
    {
        if (buttonTarget != null)
            buttonTarget.localPosition = localPosition;

        currentFillAmount = Mathf.Clamp01(fillAmount);

        if (holdProgressImage != null)
            holdProgressImage.fillAmount = currentFillAmount;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(visible);
    }
}
