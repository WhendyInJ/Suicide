using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ButtonHoldInteraction : MonoBehaviour
{
    private static readonly Dictionary<string, bool> SharedFirstGroupActiveStates = new();

    [Header("References")]
    [SerializeField] private Transform buttonTarget;
    [SerializeField] private Canvas interactionCanvas;
    [SerializeField] private Image holdProgressImage;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField, Min(0.01f)] private float holdDuration = 1f;
    [SerializeField, Min(0.01f)] private float cancelReturnDuration = 0.2f;

    [Header("Button Motion")]
    [SerializeField] private Vector3 pressedLocalOffset = new Vector3(0f, -0.05f, 0f);
    [SerializeField, Min(0.01f)] private float pressCycleDuration = 1f;

    [Header("Controlled Objects - Group A")]
    [SerializeField] private Transform[] firstSpikeObjects;
    [SerializeField] private Renderer[] firstPlatformRenderers;

    [Header("Controlled Objects - Group B")]
    [SerializeField] private Transform[] secondSpikeObjects;
    [SerializeField] private Renderer[] secondPlatformRenderers;

    [Header("Controlled Objects - Shared Settings")]
    [SerializeField, Min(0f)] private float spikeMoveDistance = 2f;
    [SerializeField, Min(0.01f)] private float controlledObjectTransitionDuration = 0.35f;
    [SerializeField] private string platformColorPropertyName = "_BaseColor";
    [SerializeField] private Color activePlatformColor = Color.green;
    [SerializeField] private Color inactivePlatformColor = new Color32(0xD6, 0x5A, 0x5D, 0xFF);
    [SerializeField] private bool startWithFirstGroupActive = true;
    [SerializeField] private string sharedToggleGroupId;

    private readonly HashSet<int> overlappingPlayerColliders = new();
    private MaterialPropertyBlock propertyBlock;

    private Coroutine activeRoutine;
    private Vector3 defaultLocalPosition;
    private Vector3[] firstSpikeRaisedLocalPositions;
    private Vector3[] secondSpikeRaisedLocalPositions;
    private float currentFillAmount;
    private bool isFirstGroupActive = true;

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

        propertyBlock = new MaterialPropertyBlock();
        defaultLocalPosition = buttonTarget.localPosition;
        CacheSpikePositions();
        isFirstGroupActive = GetCurrentFirstGroupActiveState();
        ApplyControlledObjectsImmediate(isFirstGroupActive);
        ApplyStateImmediate(defaultLocalPosition, 0f);
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
            ApplyStateImmediate(buttonTarget.localPosition, Mathf.Lerp(startFill, 1f, t));
            yield return null;
        }

        ApplyStateImmediate(buttonTarget.localPosition, 1f);

        Vector3 pressedPosition = defaultLocalPosition + pressedLocalOffset;
        float downDuration = pressCycleDuration * 0.5f;
        float upDuration = pressCycleDuration * 0.5f;

        yield return CoAnimateToState(defaultLocalPosition, pressedPosition, 1f, 1f, downDuration);
        yield return CoAnimateControlledObjects(!GetCurrentFirstGroupActiveState(), controlledObjectTransitionDuration);
        yield return CoAnimateToState(pressedPosition, defaultLocalPosition, 1f, 0f, upDuration);

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

    private void CacheSpikePositions()
    {
        firstSpikeRaisedLocalPositions = CacheSpikePositions(firstSpikeObjects);
        secondSpikeRaisedLocalPositions = CacheSpikePositions(secondSpikeObjects);
    }

    private Vector3[] CacheSpikePositions(Transform[] spikes)
    {
        if (spikes == null)
            return System.Array.Empty<Vector3>();

        Vector3[] cachedPositions = new Vector3[spikes.Length];

        for (int i = 0; i < spikes.Length; i++)
        {
            if (spikes[i] != null)
                cachedPositions[i] = spikes[i].localPosition;
        }

        return cachedPositions;
    }

    private bool GetCurrentFirstGroupActiveState()
    {
        if (string.IsNullOrWhiteSpace(sharedToggleGroupId))
            return startWithFirstGroupActive;

        if (!SharedFirstGroupActiveStates.TryGetValue(sharedToggleGroupId, out bool state))
        {
            state = startWithFirstGroupActive;
            SharedFirstGroupActiveStates[sharedToggleGroupId] = state;
        }

        return state;
    }

    private void SetCurrentFirstGroupActiveState(bool state)
    {
        isFirstGroupActive = state;

        if (string.IsNullOrWhiteSpace(sharedToggleGroupId))
            return;

        SharedFirstGroupActiveStates[sharedToggleGroupId] = state;
    }

    private IEnumerator CoAnimateControlledObjects(bool targetFirstGroupActiveState, float duration)
    {
        bool fromFirstGroupActiveState = GetCurrentFirstGroupActiveState();

        if (duration <= 0f)
        {
            ApplyControlledObjectsImmediate(targetFirstGroupActiveState);
            SetCurrentFirstGroupActiveState(targetFirstGroupActiveState);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float firstGroupActiveProgress = fromFirstGroupActiveState == targetFirstGroupActiveState
                ? (targetFirstGroupActiveState ? 1f : 0f)
                : (targetFirstGroupActiveState ? t : 1f - t);

            ApplyControlledObjectsByProgress(firstGroupActiveProgress);
            yield return null;
        }

        ApplyControlledObjectsImmediate(targetFirstGroupActiveState);
        SetCurrentFirstGroupActiveState(targetFirstGroupActiveState);
    }

    private void ApplyControlledObjectsImmediate(bool firstGroupActive)
    {
        ApplyControlledObjectsByProgress(firstGroupActive ? 1f : 0f);
    }

    private void ApplyControlledObjectsByProgress(float firstGroupActiveProgress)
    {
        firstGroupActiveProgress = Mathf.Clamp01(firstGroupActiveProgress);

        ApplySpikeGroupProgress(firstSpikeObjects, firstSpikeRaisedLocalPositions, firstGroupActiveProgress);
        ApplySpikeGroupProgress(secondSpikeObjects, secondSpikeRaisedLocalPositions, 1f - firstGroupActiveProgress);

        Color firstPlatformColor = Color.Lerp(inactivePlatformColor, activePlatformColor, firstGroupActiveProgress);
        Color secondPlatformColor = Color.Lerp(activePlatformColor, inactivePlatformColor, firstGroupActiveProgress);

        ApplyPlatformColor(firstPlatformRenderers, firstPlatformColor);
        ApplyPlatformColor(secondPlatformRenderers, secondPlatformColor);
    }

    private void ApplySpikeGroupProgress(Transform[] spikes, Vector3[] raisedPositions, float activeProgress)
    {
        if (spikes == null || raisedPositions == null)
            return;

        for (int i = 0; i < spikes.Length && i < raisedPositions.Length; i++)
        {
            Transform spike = spikes[i];
            if (spike == null)
                continue;

            Vector3 raisedPosition = raisedPositions[i];
            Vector3 loweredPosition = raisedPosition + Vector3.down * spikeMoveDistance;
            spike.localPosition = Vector3.Lerp(raisedPosition, loweredPosition, activeProgress);
        }
    }

    private void ApplyPlatformColor(Renderer[] renderers, Color color)
    {
        if (renderers == null || propertyBlock == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);

            Material sharedMaterial = renderer.sharedMaterial;
            bool applied = false;

            if (!string.IsNullOrWhiteSpace(platformColorPropertyName) &&
                sharedMaterial != null &&
                sharedMaterial.HasProperty(platformColorPropertyName))
            {
                propertyBlock.SetColor(platformColorPropertyName, color);
                applied = true;
            }

            if (!applied && sharedMaterial != null && sharedMaterial.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", color);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
