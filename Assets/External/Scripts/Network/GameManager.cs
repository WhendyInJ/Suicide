using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class GameManager : MonoBehaviourPunCallbacks
{
    private const string NextSpawnSequenceKey = "gm_spawn_next";
    private const string SpawnAssignmentKeyPrefix = "gm_spawn_actor_";

    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool autoSpawnLocalPlayer = true;

    private GameObject localPlayerInstance;
    private bool spawnRequestSent;
    private int cachedNextSpawnSequence;
    private bool hasCachedNextSpawnSequence;

    private void Awake()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("GameManager is missing a player prefab reference.", this);
        }

        if (GetComponent<GameOverManager>() == null)
        {
            gameObject.AddComponent<GameOverManager>();
        }
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
            return;

        RefreshSpawnSequenceCache();
        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
        TryRequestLocalPlayerSpawn();
    }

    public override void OnJoinedRoom()
    {
        spawnRequestSent = false;
        localPlayerInstance = null;
        RefreshSpawnSequenceCache();

        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
        TryRequestLocalPlayerSpawn();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!autoSpawnLocalPlayer || !PhotonNetwork.InRoom)
            return;

        string localAssignmentKey = GetSpawnAssignmentKey(PhotonNetwork.LocalPlayer.ActorNumber);
        if (!propertiesThatChanged.ContainsKey(localAssignmentKey) &&
            !propertiesThatChanged.ContainsKey(NextSpawnSequenceKey))
        {
            return;
        }

        RefreshSpawnSequenceCache();
        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        spawnRequestSent = false;

        if (newMasterClient != null && newMasterClient.IsLocal)
        {
            RefreshSpawnSequenceCache();
            EnsureSpawnSequenceInitialized();
        }
        else
        {
            RefreshSpawnSequenceCache();
        }

        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
        TryRequestLocalPlayerSpawn();
    }

    public void TryRequestLocalPlayerSpawn()
    {
        if (!autoSpawnLocalPlayer || !PhotonNetwork.InRoom)
            return;

        if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        if (HasSpawnedLocalPlayer())
            return;

        if (TryGetAssignedSpawnIndex(PhotonNetwork.LocalPlayer.ActorNumber, out _))
        {
            TrySpawnLocalPlayerFromAssignment();
            TryAttachSceneCameraToLocalPlayer();
            return;
        }

        if (spawnRequestSent)
            return;

        spawnRequestSent = true;

        if (PhotonNetwork.IsMasterClient)
        {
            EnsureSpawnAssignment(PhotonNetwork.LocalPlayer.ActorNumber);
            TrySpawnLocalPlayerFromAssignment();
            return;
        }

        photonView.RPC(
            nameof(RPC_RequestSpawnAssignment),
            RpcTarget.MasterClient,
            PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RPC_RequestSpawnAssignment(int actorNumber, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (info.Sender == null || info.Sender.ActorNumber != actorNumber)
            return;

        EnsureSpawnAssignment(actorNumber);
    }

    private void TrySpawnLocalPlayerFromAssignment()
    {
        if (!autoSpawnLocalPlayer || !PhotonNetwork.InRoom)
            return;

        if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        if (HasSpawnedLocalPlayer())
            return;

        if (!TryGetAssignedSpawnIndex(PhotonNetwork.LocalPlayer.ActorNumber, out int spawnIndex))
            return;

        Transform spawnPoint = GetSpawnPoint(spawnIndex);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"Spawn point index {spawnIndex} is invalid.", this);
            return;
        }

        localPlayerInstance = PhotonNetwork.Instantiate(
            playerPrefab.name,
            spawnPoint.position,
            spawnPoint.rotation,
            0);

        TryAttachSceneCameraToLocalPlayer();
    }

    private bool HasSpawnedLocalPlayer()
    {
        if (localPlayerInstance != null)
            return true;

        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        for (int i = 0; i < views.Length; i++)
        {
            PhotonView view = views[i];
            if (view == null || !view.IsMine)
                continue;

            if (playerPrefab != null && view.gameObject.name.StartsWith(playerPrefab.name))
            {
                localPlayerInstance = view.gameObject;
                TryAttachSceneCameraToLocalPlayer();
                return true;
            }
        }

        return false;
    }

    private void TryAttachSceneCameraToLocalPlayer()
    {
        if (localPlayerInstance == null)
            return;

        CameraController[] cameraControllers = FindObjectsByType<CameraController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameraControllers.Length; i++)
        {
            CameraController cameraController = cameraControllers[i];
            if (cameraController == null)
                continue;

            cameraController.SetFollowTarget(localPlayerInstance.transform);
        }
    }

    private int EnsureSpawnAssignment(int actorNumber)
    {
        if (TryGetAssignedSpawnIndex(actorNumber, out int existingIndex))
            return existingIndex;

        EnsureSpawnSequenceInitialized();

        bool isCurrentMasterActor =
            PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.GetPlayer(actorNumber)?.IsMasterClient == true;

        int assignedIndex;
        Hashtable changedProperties = new Hashtable();

        if (isCurrentMasterActor)
        {
            assignedIndex = 0;

            int nextSequence = Mathf.Max(1, GetNextSpawnSequence());
            cachedNextSpawnSequence = nextSequence;
            hasCachedNextSpawnSequence = true;
            changedProperties[NextSpawnSequenceKey] = cachedNextSpawnSequence;
        }
        else
        {
            int nextSequence = Mathf.Max(1, GetNextSpawnSequence());
            assignedIndex = GetClientSpawnIndex(nextSequence);
            cachedNextSpawnSequence = nextSequence + 1;
            hasCachedNextSpawnSequence = true;
            changedProperties[NextSpawnSequenceKey] = cachedNextSpawnSequence;
        }

        changedProperties[GetSpawnAssignmentKey(actorNumber)] = assignedIndex;

        PhotonNetwork.CurrentRoom?.SetCustomProperties(changedProperties);
        return assignedIndex;
    }

    private void EnsureSpawnSequenceInitialized()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(NextSpawnSequenceKey))
        {
            RefreshSpawnSequenceCache();
            return;
        }

        Hashtable initProperties = new Hashtable
        {
            { NextSpawnSequenceKey, 1 }
        };

        cachedNextSpawnSequence = 1;
        hasCachedNextSpawnSequence = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(initProperties);
    }

    private bool TryGetAssignedSpawnIndex(int actorNumber, out int spawnIndex)
    {
        spawnIndex = -1;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        string key = GetSpawnAssignmentKey(actorNumber);
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object rawValue))
            return false;

        if (rawValue is not int storedIndex)
            return false;

        spawnIndex = storedIndex;
        return spawnIndex >= 0 && spawnIndex < spawnPoints.Length;
    }

    private int GetRoomInt(string key, int fallbackValue)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return fallbackValue;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object rawValue))
            return fallbackValue;

        return rawValue is int intValue ? intValue : fallbackValue;
    }

    private int GetNextSpawnSequence()
    {
        if (hasCachedNextSpawnSequence)
            return cachedNextSpawnSequence;

        RefreshSpawnSequenceCache();
        return cachedNextSpawnSequence;
    }

    private void RefreshSpawnSequenceCache()
    {
        cachedNextSpawnSequence = GetRoomInt(NextSpawnSequenceKey, 1);
        hasCachedNextSpawnSequence = true;
    }

    private Transform GetSpawnPoint(int spawnIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
            return null;

        return spawnPoints[spawnIndex];
    }

    private int GetClientSpawnIndex(int sequence)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return 0;

        if (spawnPoints.Length == 1)
            return 0;

        int nonHostSpawnCount = spawnPoints.Length - 1;
        return 1 + ((Mathf.Max(1, sequence) - 1) % nonHostSpawnCount);
    }

    private static string GetSpawnAssignmentKey(int actorNumber)
    {
        return $"{SpawnAssignmentKeyPrefix}{actorNumber}";
    }
}
