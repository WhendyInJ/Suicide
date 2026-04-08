using System.Collections;
using UnityEngine;

[System.Serializable]
public enum SpikeFloorGroupSide
{
    First = 0,
    Second = 1
}

[DisallowMultipleComponent]
public class SpikePlatformGroupController : MonoBehaviour
{
    [Header("Controlled Objects - Group A")]
    [SerializeField] private Transform[] firstSpikeObjects;
    [SerializeField] private Renderer[] firstPlatformRenderers;
    [SerializeField] private GameObject[] firstHealObjects;

    [Header("Controlled Objects - Group B")]
    [SerializeField] private Transform[] secondSpikeObjects;
    [SerializeField] private Renderer[] secondPlatformRenderers;
    [SerializeField] private GameObject[] secondHealObjects;

    [Header("Shared Settings")]
    [SerializeField, Min(0f)] private float spikeMoveDistance = 2f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.35f;
    [SerializeField] private string platformColorPropertyName = "_BaseColor";
    [SerializeField] private Color activePlatformColor = Color.green;
    [SerializeField] private Color inactivePlatformColor = new Color32(0xD6, 0x5A, 0x5D, 0xFF);
    [SerializeField] private bool startWithFirstGroupActive = true;
    [SerializeField] private bool enableDebugLogs = false;

    private MaterialPropertyBlock propertyBlock;
    private Vector3[] firstSpikeRaisedLocalPositions;
    private Vector3[] secondSpikeRaisedLocalPositions;
    private bool isFirstGroupActive;
    private bool isTransitionRunning;

    public bool IsFirstGroupActive => isFirstGroupActive;
    public bool IsTransitionRunning => isTransitionRunning;
    public float TransitionDuration => transitionDuration;

    public bool IsGroupInHealState(SpikeFloorGroupSide groupSide)
    {
        return groupSide == SpikeFloorGroupSide.First
            ? isFirstGroupActive
            : !isFirstGroupActive;
    }

    public bool IsGroupInDamageState(SpikeFloorGroupSide groupSide)
    {
        return !IsGroupInHealState(groupSide);
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        firstSpikeRaisedLocalPositions = CacheSpikePositions(firstSpikeObjects);
        secondSpikeRaisedLocalPositions = CacheSpikePositions(secondSpikeObjects);
        isFirstGroupActive = startWithFirstGroupActive;
        ApplyImmediate(isFirstGroupActive);
    }

    public IEnumerator CoToggleState()
    {
        yield return CoSetFirstGroupActive(!isFirstGroupActive);
    }

    public IEnumerator CoSetFirstGroupActive(bool targetFirstGroupActive)
    {
        if (isTransitionRunning)
            yield break;

        isTransitionRunning = true;

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SpikePlatformGroupController:{name}] from={isFirstGroupActive}, to={targetFirstGroupActive}",
                this);
        }

        bool fromFirstGroupActive = isFirstGroupActive;

        if (transitionDuration <= 0f)
        {
            ApplyImmediate(targetFirstGroupActive);
            isFirstGroupActive = targetFirstGroupActive;
            isTransitionRunning = false;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float firstGroupActiveProgress = fromFirstGroupActive == targetFirstGroupActive
                ? (targetFirstGroupActive ? 1f : 0f)
                : (targetFirstGroupActive ? t : 1f - t);

            ApplyByProgress(firstGroupActiveProgress);
            yield return null;
        }

        ApplyImmediate(targetFirstGroupActive);
        isFirstGroupActive = targetFirstGroupActive;
        isTransitionRunning = false;
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

    private void ApplyImmediate(bool firstGroupActive)
    {
        ApplyByProgress(firstGroupActive ? 1f : 0f);
    }

    private void ApplyByProgress(float firstGroupActiveProgress)
    {
        firstGroupActiveProgress = Mathf.Clamp01(firstGroupActiveProgress);

        ApplySpikeGroupProgress(firstSpikeObjects, firstSpikeRaisedLocalPositions, firstGroupActiveProgress);
        ApplySpikeGroupProgress(secondSpikeObjects, secondSpikeRaisedLocalPositions, 1f - firstGroupActiveProgress);

        Color firstPlatformColor = Color.Lerp(inactivePlatformColor, activePlatformColor, firstGroupActiveProgress);
        Color secondPlatformColor = Color.Lerp(activePlatformColor, inactivePlatformColor, firstGroupActiveProgress);

        ApplyPlatformColor(firstPlatformRenderers, firstPlatformColor);
        ApplyPlatformColor(secondPlatformRenderers, secondPlatformColor);

        bool firstHealActive = firstGroupActiveProgress >= 0.5f;
        bool secondHealActive = !firstHealActive;

        SetObjectsActive(firstHealObjects, firstHealActive);
        SetObjectsActive(secondHealObjects, secondHealActive);
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

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject target = objects[i];
            if (target == null || target.activeSelf == active)
                continue;

            target.SetActive(active);
        }
    }
}
