using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class GameManager : MonoBehaviourPunCallbacks
{
    [Header("Player Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool autoSpawnLocalPlayer = true;

    private GameObject localPlayerInstance;

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

        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
    }

    public override void OnJoinedRoom()
    {
        localPlayerInstance = null;

        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!autoSpawnLocalPlayer || !PhotonNetwork.InRoom)
            return;

        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TrySpawnLocalPlayerFromAssignment();
        TryAttachSceneCameraToLocalPlayer();
    }

    private void TrySpawnLocalPlayerFromAssignment()
    {
        if (!autoSpawnLocalPlayer || !PhotonNetwork.InRoom)
            return;

        if (playerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            return;

        if (HasSpawnedLocalPlayer())
            return;

        int spawnIndex = GetDeterministicSpawnIndexForActor(PhotonNetwork.LocalPlayer.ActorNumber);
        if (spawnIndex < 0)
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

    private int GetDeterministicSpawnIndexForActor(int actorNumber)
    {
        if (PhotonNetwork.CurrentRoom == null || spawnPoints == null || spawnPoints.Length == 0)
            return -1;

        Player[] orderedPlayers = PhotonNetwork.PlayerList;
        if (orderedPlayers == null || orderedPlayers.Length == 0)
            return -1;

        System.Array.Sort(orderedPlayers, ComparePlayersForSpawnOrder);

        int orderedIndex = -1;
        for (int i = 0; i < orderedPlayers.Length; i++)
        {
            if (orderedPlayers[i] != null && orderedPlayers[i].ActorNumber == actorNumber)
            {
                orderedIndex = i;
                break;
            }
        }

        if (orderedIndex < 0)
            return -1;

        if (spawnPoints.Length == 1)
            return 0;

        if (orderedIndex >= spawnPoints.Length)
        {
            Debug.LogWarning(
                $"GameManager has fewer spawn points ({spawnPoints.Length}) than joined players ({orderedPlayers.Length}). Spawn overlap may occur.",
                this);
        }

        return Mathf.Clamp(orderedIndex, 0, spawnPoints.Length - 1);
    }

    private static int ComparePlayersForSpawnOrder(Player left, Player right)
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        if (left.IsMasterClient && !right.IsMasterClient)
            return -1;

        if (!left.IsMasterClient && right.IsMasterClient)
            return 1;

        return left.ActorNumber.CompareTo(right.ActorNumber);
    }

    private Transform GetSpawnPoint(int spawnIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return null;

        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
            return null;

        return spawnPoints[spawnIndex];
    }
}
