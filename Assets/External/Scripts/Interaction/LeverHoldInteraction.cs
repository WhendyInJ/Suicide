using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LeverHoldInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform lever;
    [SerializeField] private Canvas interactionCanvas;
    [SerializeField] private Image holdProgressImage;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField, Min(0.01f)] private float holdDuration = 1f;
    [SerializeField, Min(0.01f)] private float cancelReturnDuration = 0.2f;

    [Header("Lever X Rotation")]
    [SerializeField] private float inactiveXRotation = -45f;
    [SerializeField] private float activeXRotation = 45f;

    private readonly HashSet<int> localOverlapColliderIds = new();

    private Coroutine activeRoutine;
    private Vector3 leverBaseLocalEuler;
    private float currentProgress;
    private bool isActivated;

    private bool IsPlayerInRange => localOverlapColliderIds.Count > 0;
    private float StableProgress => isActivated ? 1f : 0f;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        if (lever == null)
            lever = transform;

        leverBaseLocalEuler = lever.localEulerAngles;
        ApplyVisualImmediate(StableProgress);
        SetCanvasVisible(false);
    }

    private void Update()
    {
        if (!IsPlayerInRange)
            return;

        if (activeRoutine != null)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        activeRoutine = StartCoroutine(CoHoldToggle());
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
        if (!TryResolveLocalPlayerCollider(other, out int colliderId))
            return;

        if (!localOverlapColliderIds.Remove(colliderId))
            return;

        if (!IsPlayerInRange)
        {
            CancelCurrentRoutine();
            StartReturnToStable();
            SetCanvasVisible(false);
        }
    }

    private void OnDisable()
    {
        CancelCurrentRoutine();
        ApplyVisualImmediate(StableProgress);
        SetCanvasVisible(false);
        localOverlapColliderIds.Clear();
    }

    private void TryEnterRange(Collider other)
    {
        if (!TryResolveLocalPlayerCollider(other, out int colliderId))
            return;

        if (!localOverlapColliderIds.Add(colliderId))
            return;

        SetCanvasVisible(true);
        ApplyVisualImmediate(currentProgress);
    }

    private bool TryResolveLocalPlayerCollider(Collider other, out int colliderId)
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

    private void StartReturnToStable()
    {
        if (Mathf.Approximately(currentProgress, StableProgress))
        {
            ApplyVisualImmediate(StableProgress);
            return;
        }

        activeRoutine = StartCoroutine(CoAnimateProgress(currentProgress, StableProgress, cancelReturnDuration));
    }

    private void CancelCurrentRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private IEnumerator CoHoldToggle()
    {
        float startProgress = currentProgress;
        float targetProgress = isActivated ? 0f : 1f;
        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            if (!IsPlayerInRange || !Input.GetKey(interactKey))
            {
                yield return CoAnimateProgress(currentProgress, StableProgress, cancelReturnDuration);
                activeRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / holdDuration);
            ApplyVisualImmediate(Mathf.Lerp(startProgress, targetProgress, t));
            yield return null;
        }

        isActivated = !isActivated;
        ApplyVisualImmediate(StableProgress);
        activeRoutine = null;
    }

    private IEnumerator CoAnimateProgress(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            ApplyVisualImmediate(to);
            activeRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyVisualImmediate(Mathf.Lerp(from, to, t));
            yield return null;
        }

        ApplyVisualImmediate(to);
        activeRoutine = null;
    }

    private void ApplyVisualImmediate(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);

        if (holdProgressImage != null)
            holdProgressImage.fillAmount = currentProgress;

        if (lever != null)
        {
            Vector3 euler = leverBaseLocalEuler;
            euler.x = Mathf.Lerp(inactiveXRotation, activeXRotation, currentProgress);
            lever.localRotation = Quaternion.Euler(euler);
        }
    }

    private void SetCanvasVisible(bool visible)
    {
        if (interactionCanvas != null)
            interactionCanvas.gameObject.SetActive(visible);
    }
}
