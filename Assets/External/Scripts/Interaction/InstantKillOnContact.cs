using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class InstantKillOnContact : MonoBehaviourPunCallbacks, IInteractionTriggerTarget
{
    private const string StatePropertyPrefix = "instant_kill_state_";
    private const string TriggerPlayerPropertyPrefix = "instant_kill_trigger_player_";
    private const int OverlapBufferSize = 32;

    private enum InstantKillCycleState
    {
        Ready = 0,
        MovingDown = 1,
        Returning = 2
    }

    [Header("Activation")]
    [FormerlySerializedAs("startActive")]
    [SerializeField] private bool startReady = true;
    [SerializeField] private string stateSyncKeyOverride = string.Empty;

    [Header("Trap Motion")]
    [SerializeField] private Transform moveTarget;
    [SerializeField] private float activeTargetWorldY = 5.1f;
    [SerializeField, Min(0.01f)] private float targetMoveSpeed = 12f;

    [Header("Visual")]
    [SerializeField] private GameObject[] activeStateObjects;

    [Header("Runtime Debug")]
    [SerializeField] private InstantKillCycleState cycleState;
    [SerializeField] private int pendingTriggeringPlayerViewId = -1;
    [SerializeField] private int selectedTargetPlayerViewId = -1;

    private float inactiveTargetWorldY;
    private string cachedStatePropertyKey;
    private string cachedTriggerPlayerPropertyKey;
    private Collider triggerCollider;
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];
    private readonly System.Collections.Generic.Dictionary<int, int> activeColliderToPlayerViewId = new();
    private readonly System.Collections.Generic.Dictionary<int, int> activeColliderCountByPlayerViewId = new();
    private readonly System.Collections.Generic.Dictionary<int, PlayerHealth> currentlyDetectedPlayers = new();
    private readonly System.Collections.Generic.Dictionary<int, int> detectionOrderByPlayerViewId = new();
    private int nextDetectionOrder = 1;

    private void Awake()
    {
        if (moveTarget == null)
            moveTarget = transform;

        triggerCollider = GetComponent<Collider>();
        inactiveTargetWorldY = moveTarget.position.y;
        cachedStatePropertyKey = BuildStatePropertyKey();
        cachedTriggerPlayerPropertyKey = BuildTriggerPlayerPropertyKey();
        cycleState = startReady ? InstantKillCycleState.Ready : InstantKillCycleState.MovingDown;
        ApplyTrapStateImmediate(cycleState);
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        if (TryGetSyncedState(out InstantKillCycleState syncedState, out int syncedTriggeringPlayerViewId))
        {
            pendingTriggeringPlayerViewId = syncedTriggeringPlayerViewId;
            ApplyCycleStateLocal(syncedState);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
            PublishCycleState(cycleState, pendingTriggeringPlayerViewId);
    }

    private void Update()
    {
        UpdateMoveTarget();
        TickAuthoritativeCycle();
    }

    public void TriggerFromInteraction(NetworkHoldInteractionBase source, int triggeringPlayerViewId)
    {
        RequestActivate(triggeringPlayerViewId);
    }

    public void RequestActivate(int triggeringPlayerViewId)
    {
        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            BeginActivationCycle(triggeringPlayerViewId);
            return;
        }

        BeginActivationCycle(triggeringPlayerViewId);
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (!PhotonNetwork.InRoom ||
            string.IsNullOrEmpty(cachedStatePropertyKey) ||
            string.IsNullOrEmpty(cachedTriggerPlayerPropertyKey))
            return;

        bool stateChanged = propertiesThatChanged.ContainsKey(cachedStatePropertyKey);
        bool triggerPlayerChanged = propertiesThatChanged.ContainsKey(cachedTriggerPlayerPropertyKey);
        if (!stateChanged && !triggerPlayerChanged)
            return;

        if (!TryGetSyncedState(out InstantKillCycleState syncedState, out int syncedTriggeringPlayerViewId))
            return;

        pendingTriggeringPlayerViewId = syncedTriggeringPlayerViewId;
        ApplyCycleStateLocal(syncedState);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterCandidate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryRegisterCandidate(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
            return;

        int colliderId = other.GetInstanceID();
        if (!activeColliderToPlayerViewId.TryGetValue(colliderId, out int playerViewId))
            return;

        activeColliderToPlayerViewId.Remove(colliderId);

        if (!activeColliderCountByPlayerViewId.TryGetValue(playerViewId, out int count))
            return;

        count--;
        if (count > 0)
        {
            activeColliderCountByPlayerViewId[playerViewId] = count;
            return;
        }

        activeColliderCountByPlayerViewId.Remove(playerViewId);
        currentlyDetectedPlayers.Remove(playerViewId);
    }

    private void BeginActivationCycle(int triggeringPlayerViewId)
    {
        if (cycleState != InstantKillCycleState.Ready)
            return;

        pendingTriggeringPlayerViewId = triggeringPlayerViewId;
        selectedTargetPlayerViewId = -1;
        ClearDetectionState();
        ApplyCycleStateLocal(InstantKillCycleState.MovingDown);
        CaptureCurrentOverlaps();

        if (PhotonNetwork.InRoom)
            PublishCycleState(cycleState, pendingTriggeringPlayerViewId);
    }

    private void ApplyTrapStateImmediate(InstantKillCycleState targetState)
    {
        cycleState = targetState;
        ApplyActiveStateObjects(targetState != InstantKillCycleState.Ready);

        if (moveTarget == null)
            return;

        Vector3 position = moveTarget.position;
        position.y = targetState == InstantKillCycleState.MovingDown
            ? activeTargetWorldY
            : inactiveTargetWorldY;
        moveTarget.position = position;
    }

    private void ApplyCycleStateLocal(InstantKillCycleState targetState)
    {
        if (cycleState == targetState)
            return;

        cycleState = targetState;
        ApplyActiveStateObjects(targetState != InstantKillCycleState.Ready);

        if (targetState == InstantKillCycleState.Ready)
        {
            pendingTriggeringPlayerViewId = -1;
            selectedTargetPlayerViewId = -1;
            ClearDetectionState();
        }
        else if (targetState == InstantKillCycleState.MovingDown)
        {
            selectedTargetPlayerViewId = -1;
            ClearDetectionState();
        }
    }

    private void UpdateMoveTarget()
    {
        if (moveTarget == null)
            return;

        float targetY = cycleState == InstantKillCycleState.MovingDown
            ? activeTargetWorldY
            : inactiveTargetWorldY;
        Vector3 targetPosition = moveTarget.position;
        targetPosition.y = targetY;

        moveTarget.position = Vector3.MoveTowards(
            moveTarget.position,
            targetPosition,
            targetMoveSpeed * Time.deltaTime);
    }

    private void TickAuthoritativeCycle()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        if (moveTarget == null)
            return;

        switch (cycleState)
        {
            case InstantKillCycleState.MovingDown:
                if (HasReachedTargetY(activeTargetWorldY))
                    ResolveCycleTargetAndReturn();
                break;

            case InstantKillCycleState.Returning:
                if (HasReachedTargetY(inactiveTargetWorldY))
                {
                    ApplyCycleStateLocal(InstantKillCycleState.Ready);

                    if (PhotonNetwork.InRoom)
                        PublishCycleState(cycleState, pendingTriggeringPlayerViewId);
                }
                break;
        }
    }

    private bool HasReachedTargetY(float targetY)
    {
        return Mathf.Abs(moveTarget.position.y - targetY) <= 0.001f;
    }

    private void ResolveCycleTargetAndReturn()
    {
        selectedTargetPlayerViewId = SelectTargetPlayerViewId();

        if (selectedTargetPlayerViewId > 0 &&
            currentlyDetectedPlayers.TryGetValue(selectedTargetPlayerViewId, out PlayerHealth selectedHealth) &&
            selectedHealth != null &&
            selectedHealth.IsAlive)
        {
            selectedHealth.RequestKill(pendingTriggeringPlayerViewId);
        }

        ApplyCycleStateLocal(InstantKillCycleState.Returning);

        if (PhotonNetwork.InRoom)
            PublishCycleState(cycleState, pendingTriggeringPlayerViewId);
    }

    private int SelectTargetPlayerViewId()
    {
        if (pendingTriggeringPlayerViewId > 0 &&
            activeColliderCountByPlayerViewId.ContainsKey(pendingTriggeringPlayerViewId) &&
            currentlyDetectedPlayers.TryGetValue(pendingTriggeringPlayerViewId, out PlayerHealth triggeringHealth) &&
            triggeringHealth != null &&
            triggeringHealth.IsAlive)
        {
            return pendingTriggeringPlayerViewId;
        }

        int selectedPlayerViewId = -1;
        int selectedOrder = int.MaxValue;

        foreach (System.Collections.Generic.KeyValuePair<int, PlayerHealth> pair in currentlyDetectedPlayers)
        {
            int playerViewId = pair.Key;
            PlayerHealth health = pair.Value;
            if (health == null || !health.IsAlive)
                continue;

            if (!activeColliderCountByPlayerViewId.ContainsKey(playerViewId))
                continue;

            if (!detectionOrderByPlayerViewId.TryGetValue(playerViewId, out int order))
                continue;

            if (order >= selectedOrder)
                continue;

            selectedOrder = order;
            selectedPlayerViewId = playerViewId;
        }

        return selectedPlayerViewId;
    }

    private void TryRegisterCandidate(Collider other)
    {
        if (cycleState != InstantKillCycleState.MovingDown)
            return;

        if (!TryResolvePlayerCandidate(other, out int colliderId, out int playerViewId, out PlayerHealth health))
            return;

        if (activeColliderToPlayerViewId.ContainsKey(colliderId))
            return;

        activeColliderToPlayerViewId.Add(colliderId, playerViewId);

        activeColliderCountByPlayerViewId.TryGetValue(playerViewId, out int count);
        activeColliderCountByPlayerViewId[playerViewId] = count + 1;
        currentlyDetectedPlayers[playerViewId] = health;

        if (!detectionOrderByPlayerViewId.ContainsKey(playerViewId))
        {
            detectionOrderByPlayerViewId.Add(playerViewId, nextDetectionOrder++);
        }
    }

    private bool TryResolvePlayerCandidate(Collider other, out int colliderId, out int playerViewId, out PlayerHealth health)
    {
        colliderId = 0;
        playerViewId = -1;
        health = null;

        if (other == null)
            return false;

        colliderId = other.GetInstanceID();
        health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return false;

        PhotonView playerView = health.GetComponent<PhotonView>();
        if (playerView == null)
            playerView = health.GetComponentInParent<PhotonView>();

        if (playerView == null || playerView.ViewID <= 0)
            return false;

        playerViewId = playerView.ViewID;
        return true;
    }

    private void CaptureCurrentOverlaps()
    {
        if (triggerCollider == null)
            return;

        Bounds bounds = triggerCollider.bounds;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            overlapBuffer,
            Quaternion.identity,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider overlap = overlapBuffer[i];
            if (overlap == null || overlap == triggerCollider)
                continue;

            TryRegisterCandidate(overlap);
        }
    }

    private void ClearDetectionState()
    {
        activeColliderToPlayerViewId.Clear();
        activeColliderCountByPlayerViewId.Clear();
        currentlyDetectedPlayers.Clear();
        detectionOrderByPlayerViewId.Clear();
        nextDetectionOrder = 1;
    }

    private void ApplyActiveStateObjects(bool active)
    {
        if (activeStateObjects == null)
            return;

        for (int i = 0; i < activeStateObjects.Length; i++)
        {
            GameObject target = activeStateObjects[i];
            if (target == null || target.activeSelf == active)
                continue;

            target.SetActive(active);
        }
    }

    private void PublishCycleState(InstantKillCycleState targetState, int triggeringPlayerViewId)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return;

        ExitGames.Client.Photon.Hashtable changedProperties = new ExitGames.Client.Photon.Hashtable
        {
            { cachedStatePropertyKey, (int)targetState },
            { cachedTriggerPlayerPropertyKey, triggeringPlayerViewId }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changedProperties);
    }

    private bool TryGetSyncedState(out InstantKillCycleState syncedState, out int triggeringPlayerViewId)
    {
        syncedState = startReady ? InstantKillCycleState.Ready : InstantKillCycleState.MovingDown;
        triggeringPlayerViewId = -1;

        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom == null ||
            string.IsNullOrEmpty(cachedStatePropertyKey) ||
            string.IsNullOrEmpty(cachedTriggerPlayerPropertyKey))
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(cachedStatePropertyKey, out object rawStateValue))
            return false;

        syncedState = ConvertPropertyValueToState(rawStateValue, syncedState);

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(cachedTriggerPlayerPropertyKey, out object rawTriggerPlayerValue))
            triggeringPlayerViewId = ConvertPropertyValueToInt(rawTriggerPlayerValue, -1);

        return true;
    }

    private InstantKillCycleState ConvertPropertyValueToState(object rawValue, InstantKillCycleState fallbackValue)
    {
        return rawValue switch
        {
            byte byteValue when System.Enum.IsDefined(typeof(InstantKillCycleState), (int)byteValue)
                => (InstantKillCycleState)byteValue,
            int intValue when System.Enum.IsDefined(typeof(InstantKillCycleState), intValue)
                => (InstantKillCycleState)intValue,
            _ => fallbackValue
        };
    }

    private int ConvertPropertyValueToInt(object rawValue, int fallbackValue)
    {
        return rawValue switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
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

    private string BuildTriggerPlayerPropertyKey()
    {
        if (!string.IsNullOrWhiteSpace(stateSyncKeyOverride))
            return TriggerPlayerPropertyPrefix + stateSyncKeyOverride.Trim();

        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : SceneManager.GetActiveScene().name;
        string hierarchyPath = GetHierarchyPath(transform);
        int hash = Animator.StringToHash(sceneName + "/" + hierarchyPath);
        return TriggerPlayerPropertyPrefix + hash;
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
