using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class ItemBoxSpawner : MonoBehaviourPunCallbacks
{
    [System.Serializable]
    private sealed class SpawnEntry
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField, Min(1)] private int weight = 1;

        public ItemDefinition ItemDefinition => itemDefinition;
        public int Amount => Mathf.Max(1, amount);
        public int Weight => Mathf.Max(1, weight);
        public bool IsValid => itemDefinition != null;
    }

    [Header("References")]
    [SerializeField] private PhotonView targetPhotonView;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject pickupPrefab;

    [Header("Pickup Data")]
    [SerializeField] private SpawnEntry[] spawnEntries;

    [Header("Respawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool enableRespawn = true;
    [SerializeField, Min(0f)] private float respawnDelay = 10f;

    [Header("Runtime")]
    [SerializeField] private bool isPickupActive;
    [SerializeField] private int spawnedPickupViewId;
    [SerializeField] private double respawnAtServerTime = -1d;

    private PhotonView PhotonView => targetPhotonView != null ? targetPhotonView : photonView;

    private void Reset()
    {
        targetPhotonView = GetComponent<PhotonView>();
        spawnPoint = transform;
    }

    private void Awake()
    {
        if (targetPhotonView == null)
            targetPhotonView = GetComponent<PhotonView>();

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            if (spawnOnStart)
            {
                TrySpawnPickup();
            }

            return;
        }

        RefreshStateFromRoomProperties();

        if (PhotonNetwork.IsMasterClient)
        {
            TrySpawnPickup();
        }
    }

    private void Update()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        TickSpawner();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshStateFromRoomProperties();

        if (newMasterClient == null || !newMasterClient.IsLocal)
            return;

        TickSpawner();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!ContainsSpawnerState(propertiesThatChanged))
            return;

        RefreshStateFromRoomProperties();
    }

    public void ForceSpawnNow()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        ClearSpawnState();
        SaveStateToRoomProperties();
        TrySpawnPickup();
    }

    public void ClearSpawnedPickup()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
            return;

        if (TryResolveSpawnedPickup(out PhotonView pickupView))
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.Destroy(pickupView.gameObject);
            }
            else
            {
                Destroy(pickupView.gameObject);
            }
        }

        ClearSpawnState();
        SaveStateToRoomProperties();
    }

    private void TickSpawner()
    {
        if (pickupPrefab == null || !HasValidSpawnEntries())
            return;

        if (isPickupActive)
        {
            if (!TryResolveSpawnedPickup(out _))
            {
                isPickupActive = false;
                spawnedPickupViewId = 0;

                if (enableRespawn)
                {
                    respawnAtServerTime = GetCurrentServerTime() + respawnDelay;
                }

                SaveStateToRoomProperties();
            }

            return;
        }

        if (!enableRespawn && respawnAtServerTime >= 0d)
            return;

        if (respawnAtServerTime >= 0d && GetCurrentServerTime() < respawnAtServerTime)
            return;

        if (!spawnOnStart && respawnAtServerTime < 0d)
            return;

        TrySpawnPickup();
    }

    private void TrySpawnPickup()
    {
        if (pickupPrefab == null)
            return;

        if (!TrySelectSpawnEntry(out SpawnEntry selectedEntry))
            return;

        if (spawnPoint == null)
            spawnPoint = transform;

        if (PhotonNetwork.InRoom)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            GameObject spawnedObject = PhotonNetwork.InstantiateRoomObject(
                pickupPrefab.name,
                spawnPoint.position,
                spawnPoint.rotation,
                0,
                new object[]
                {
                    selectedEntry.ItemDefinition.ItemId,
                    selectedEntry.Amount,
                    PhotonView != null ? PhotonView.ViewID : 0
                });

            if (spawnedObject == null)
                return;

            PhotonView pickupView = spawnedObject.GetComponent<PhotonView>();
            spawnedPickupViewId = pickupView != null ? pickupView.ViewID : 0;
            isPickupActive = true;
            respawnAtServerTime = -1d;
            SaveStateToRoomProperties();
            return;
        }

        GameObject offlineSpawned = Instantiate(
            pickupPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        WorldItemPickup pickup = offlineSpawned.GetComponent<WorldItemPickup>();
        if (pickup != null)
        {
            pickup.ConfigurePickup(
                selectedEntry.ItemDefinition,
                selectedEntry.Amount,
                PhotonView != null ? PhotonView.ViewID : 0);
        }

        PhotonView offlineView = offlineSpawned.GetComponent<PhotonView>();
        spawnedPickupViewId = offlineView != null ? offlineView.ViewID : 0;
        isPickupActive = true;
        respawnAtServerTime = -1d;
    }

    private bool TryResolveSpawnedPickup(out PhotonView pickupView)
    {
        pickupView = null;

        if (spawnedPickupViewId <= 0)
            return false;

        pickupView = PhotonView.Find(spawnedPickupViewId);
        return pickupView != null;
    }

    private void ClearSpawnState()
    {
        isPickupActive = false;
        spawnedPickupViewId = 0;
        respawnAtServerTime = -1d;
    }

    private void SaveStateToRoomProperties()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null || PhotonView == null)
            return;

        Hashtable changedProperties = new Hashtable
        {
            { GetActiveStateKey(), isPickupActive },
            { GetPickupViewIdKey(), spawnedPickupViewId },
            { GetRespawnTimeKey(), respawnAtServerTime }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changedProperties);
    }

    private void RefreshStateFromRoomProperties()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonView == null)
            return;

        Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (roomProperties.TryGetValue(GetActiveStateKey(), out object activeValue) && activeValue is bool active)
        {
            isPickupActive = active;
        }

        if (roomProperties.TryGetValue(GetPickupViewIdKey(), out object viewIdValue) && viewIdValue is int pickupViewId)
        {
            spawnedPickupViewId = pickupViewId;
        }

        if (roomProperties.TryGetValue(GetRespawnTimeKey(), out object respawnValue))
        {
            if (respawnValue is double respawnTime)
            {
                respawnAtServerTime = respawnTime;
            }
            else if (respawnValue is float respawnFloat)
            {
                respawnAtServerTime = respawnFloat;
            }
        }
    }

    private bool ContainsSpawnerState(Hashtable changedProperties)
    {
        if (changedProperties == null || PhotonView == null)
            return false;

        return changedProperties.ContainsKey(GetActiveStateKey()) ||
               changedProperties.ContainsKey(GetPickupViewIdKey()) ||
               changedProperties.ContainsKey(GetRespawnTimeKey());
    }

    private string GetActiveStateKey()
    {
        return $"ibox_{PhotonView.ViewID}_active";
    }

    private string GetPickupViewIdKey()
    {
        return $"ibox_{PhotonView.ViewID}_pickup";
    }

    private string GetRespawnTimeKey()
    {
        return $"ibox_{PhotonView.ViewID}_respawn";
    }

    private static double GetCurrentServerTime()
    {
        return PhotonNetwork.InRoom ? PhotonNetwork.Time : Time.timeAsDouble;
    }

    private bool HasValidSpawnEntries()
    {
        if (spawnEntries == null || spawnEntries.Length == 0)
            return false;

        for (int i = 0; i < spawnEntries.Length; i++)
        {
            if (spawnEntries[i] != null && spawnEntries[i].IsValid)
                return true;
        }

        return false;
    }

    private bool TrySelectSpawnEntry(out SpawnEntry selectedEntry)
    {
        selectedEntry = null;

        if (!HasValidSpawnEntries())
            return false;

        int totalWeight = 0;
        for (int i = 0; i < spawnEntries.Length; i++)
        {
            SpawnEntry entry = spawnEntries[i];
            if (entry == null || !entry.IsValid)
                continue;

            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0)
            return false;

        int randomValue = Random.Range(0, totalWeight);
        int accumulatedWeight = 0;

        for (int i = 0; i < spawnEntries.Length; i++)
        {
            SpawnEntry entry = spawnEntries[i];
            if (entry == null || !entry.IsValid)
                continue;

            accumulatedWeight += entry.Weight;
            if (randomValue < accumulatedWeight)
            {
                selectedEntry = entry;
                return true;
            }
        }

        return false;
    }
}
