using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public enum SpikeFloorGroupSide
{
    First = 0,
    Second = 1
}

[DisallowMultipleComponent]
public class SpikePlatformGroupController : MonoBehaviourPunCallbacks, IInteractionTriggerTarget
{
    private const string StatePropertyPrefix = "spike_state_";

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
    [SerializeField] private string stateSyncKeyOverride = string.Empty;

    private MaterialPropertyBlock propertyBlock;
    private Vector3[] firstSpikeRaisedLocalPositions;
    private Vector3[] secondSpikeRaisedLocalPositions;
    private bool isFirstGroupActive;
    private bool isTransitionRunning;
    private string cachedStatePropertyKey;

    public event System.Action<bool> StateChanged;

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
        cachedStatePropertyKey = BuildStatePropertyKey();
        ApplyImmediate(isFirstGroupActive);
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        if (TryGetSyncedState(out bool syncedState))
        {
            StartCoroutine(CoSetFirstGroupActive(syncedState));
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            PublishState(isFirstGroupActive);
        }
    }

    public void TriggerFromInteraction(NetworkHoldInteractionBase source, int triggeringPlayerViewId)
    {
        RequestToggleState();
    }

    public void RequestToggleState()
    {
        RequestSetFirstGroupActive(!isFirstGroupActive);
    }

    public void RequestSetFirstGroupActive(bool targetFirstGroupActive)
    {
        if (isTransitionRunning || targetFirstGroupActive == isFirstGroupActive)
            return;

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;
            PublishState(targetFirstGroupActive);
            return;
        }

        StartCoroutine(CoSetFirstGroupActive(targetFirstGroupActive));
    }

    public IEnumerator CoToggleState()
    {
        yield return CoSetFirstGroupActive(!isFirstGroupActive);
    }

    public IEnumerator CoSetFirstGroupActive(bool targetFirstGroupActive)
    {
        if (isTransitionRunning)
            yield break;

        if (targetFirstGroupActive == isFirstGroupActive)
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
            NotifyStateChanged();
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
        NotifyStateChanged();
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

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(isFirstGroupActive);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (!PhotonNetwork.InRoom || string.IsNullOrEmpty(cachedStatePropertyKey))
            return;

        if (!propertiesThatChanged.TryGetValue(cachedStatePropertyKey, out object rawValue))
            return;

        bool targetState = ConvertPropertyValueToState(rawValue, isFirstGroupActive);
        if (targetState == isFirstGroupActive && !isTransitionRunning)
            return;

        StartCoroutine(CoSetFirstGroupActive(targetState));
    }

    private void PublishState(bool firstGroupActive)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable changedProperties = new ExitGames.Client.Photon.Hashtable
        {
            { cachedStatePropertyKey, firstGroupActive ? 1 : 0 }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changedProperties);
    }

    private bool TryGetSyncedState(out bool firstGroupActive)
    {
        firstGroupActive = startWithFirstGroupActive;

        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || string.IsNullOrEmpty(cachedStatePropertyKey))
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(cachedStatePropertyKey, out object rawValue))
            return false;

        firstGroupActive = ConvertPropertyValueToState(rawValue, startWithFirstGroupActive);
        return true;
    }

    private bool ConvertPropertyValueToState(object rawValue, bool fallbackValue)
    {
        return rawValue switch
        {
            bool boolValue => boolValue,
            byte byteValue => byteValue != 0,
            int intValue => intValue != 0,
            _ => fallbackValue
        };
    }

    private string BuildStatePropertyKey()
    {
        if (!string.IsNullOrWhiteSpace(stateSyncKeyOverride))
            return StatePropertyPrefix + stateSyncKeyOverride.Trim();

        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
        string hierarchyPath = GetHierarchyPath(transform);
        int hash = Animator.StringToHash(sceneName + "/" + hierarchyPath);
        return StatePropertyPrefix + hash;
    }

    private string GetHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
