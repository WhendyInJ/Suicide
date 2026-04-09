using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PhotonView))]
public class WorldItemPickup : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    [Header("Pickup Data")]
    [SerializeField] private ItemDefinition itemDefinition;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private int owningSpawnerViewId;

    private bool isCollected;
    private bool isClaimPendingLocally;
    private int pendingReceiverViewId = -1;
    private float pendingClaimRestoreAt = float.NegativeInfinity;
    private Collider[] cachedColliders;
    private Renderer[] cachedRenderers;

    private bool HasHostPickupAuthority => !PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (!isClaimPendingLocally || isCollected)
            return;

        if (Time.time < pendingClaimRestoreAt)
            return;

        RestoreLocalPendingClaim();
    }

    public void ConfigurePickup(ItemDefinition definition, int pickupAmount, int spawnerViewId)
    {
        itemDefinition = definition;
        amount = Mathf.Max(1, pickupAmount);
        owningSpawnerViewId = Mathf.Max(0, spawnerViewId);
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] instantiationData = photonView != null ? photonView.InstantiationData : null;
        if (instantiationData == null || instantiationData.Length < 3)
            return;

        string itemId = instantiationData[0] as string;
        int configuredAmount = instantiationData[1] is int value ? value : amount;
        int spawnerViewId = instantiationData[2] is int viewId ? viewId : 0;

        ItemDefinition definition = string.IsNullOrWhiteSpace(itemId)
            ? itemDefinition
            : ItemDefinitionLookup.GetById(itemId);

        ConfigurePickup(definition, configuredAmount, spawnerViewId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || isClaimPendingLocally || itemDefinition == null)
            return;

        if (!TryResolvePickupReceiver(other, out IItemPickupReceiver receiver))
            return;

        if (!receiver.HasLocalPickupAuthority)
            return;

        if (!PhotonNetwork.InRoom)
        {
            CompleteOfflinePickup(receiver);
            return;
        }

        if (HasHostPickupAuthority)
        {
            TryFinalizeClaimOnHost(receiver.PickupReceiverViewId);
        }
        else
        {
            BeginLocalPendingClaim(receiver.PickupReceiverViewId);

            photonView.RPC(
                nameof(RPC_RequestClaimPickup),
                RpcTarget.MasterClient,
                receiver.PickupReceiverViewId);
        }
    }

    private void CompleteOfflinePickup(IItemPickupReceiver receiver)
    {
        if (isCollected)
            return;

        isCollected = true;
        receiver.ReceivePickup(itemDefinition, amount);
        Destroy(gameObject);
    }

    [PunRPC]
    private void RPC_RequestClaimPickup(int receiverViewId)
    {
        if (!HasHostPickupAuthority)
            return;

        TryFinalizeClaimOnHost(receiverViewId);
    }

    private void FinalizePickupNetwork(int receiverViewId)
    {
        if (isCollected)
            return;

        isCollected = true;
        isClaimPendingLocally = false;
        SetVisualState(false);
        NotifyOwningSpawnerPickupCollected();

        if (PhotonNetwork.InRoom)
        {
            photonView.RPC(nameof(RPC_FinalizePickup), RpcTarget.All, receiverViewId);
            PhotonNetwork.Destroy(gameObject);
            return;
        }

        GrantPickupToReceiver(receiverViewId);
        Destroy(gameObject);
    }

    [PunRPC]
    private void RPC_FinalizePickup(int receiverViewId)
    {
        isCollected = true;
        isClaimPendingLocally = false;
        pendingReceiverViewId = -1;
        pendingClaimRestoreAt = float.NegativeInfinity;
        SetVisualState(false);

        if (TryResolvePickupReceiver(receiverViewId, out IItemPickupReceiver receiver))
        {
            if (receiver.HasLocalPickupAuthority)
            {
                receiver.ReceivePickup(itemDefinition, amount);
            }
        }
    }

    [PunRPC]
    private void RPC_GrantPickup(int receiverViewId)
    {
        if (!TryResolvePickupReceiver(receiverViewId, out IItemPickupReceiver receiver))
            return;

        if (!receiver.HasLocalPickupAuthority)
            return;

        receiver.ReceivePickup(itemDefinition, amount);
    }

    [PunRPC]
    private void RPC_RejectClaim()
    {
        if (isCollected)
            return;

        RestoreLocalPendingClaim();
    }

    private bool TryResolvePickupReceiver(Collider other, out IItemPickupReceiver receiver)
    {
        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemPickupReceiver found)
            {
                receiver = found;
                return true;
            }
        }

        receiver = null;
        return false;
    }

    private bool TryResolvePickupReceiver(int receiverViewId, out IItemPickupReceiver receiver)
    {
        receiver = null;

        PhotonView targetView = PhotonView.Find(receiverViewId);
        if (targetView == null)
            return false;

        MonoBehaviour[] behaviours = targetView.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemPickupReceiver found)
            {
                receiver = found;
                return true;
            }
        }

        return false;
    }

    private void BeginLocalPendingClaim(int receiverViewId)
    {
        isClaimPendingLocally = true;
        pendingReceiverViewId = receiverViewId;
        pendingClaimRestoreAt = Time.time + 0.35f;

        SetVisualState(false);
    }

    private void RestoreLocalPendingClaim()
    {
        isClaimPendingLocally = false;
        pendingReceiverViewId = -1;
        pendingClaimRestoreAt = float.NegativeInfinity;

        SetVisualState(true);
    }

    private void SetVisualState(bool visible)
    {
        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider col = cachedColliders[i];
            if (col == null)
                continue;

            col.enabled = visible;
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = cachedRenderers[i];
            if (rendererComponent == null)
                continue;

            rendererComponent.enabled = visible;
        }
    }

    private void GrantPickupToReceiver(int receiverViewId)
    {
        if (!PhotonNetwork.InRoom)
        {
            if (TryResolvePickupReceiver(receiverViewId, out IItemPickupReceiver offlineReceiver))
            {
                offlineReceiver.ReceivePickup(itemDefinition, amount);
            }

            return;
        }

        PhotonView receiverView = PhotonView.Find(receiverViewId);
        if (receiverView == null || receiverView.Owner == null)
            return;

        Player targetPlayer = receiverView.Owner;

        if (targetPlayer.IsLocal)
        {
            if (TryResolvePickupReceiver(receiverViewId, out IItemPickupReceiver localReceiver))
            {
                localReceiver.ReceivePickup(itemDefinition, amount);
            }

            return;
        }

        photonView.RPC(nameof(RPC_GrantPickup), targetPlayer, receiverViewId);
    }

    private void SendClaimRejected(int receiverViewId)
    {
        PhotonView receiverView = PhotonView.Find(receiverViewId);
        if (receiverView == null || receiverView.Owner == null)
            return;

        Player targetPlayer = receiverView.Owner;
        if (targetPlayer.IsLocal)
            return;

        photonView.RPC(nameof(RPC_RejectClaim), targetPlayer);
    }

    private void TryFinalizeClaimOnHost(int receiverViewId)
    {
        if (!HasHostPickupAuthority)
            return;

        if (isCollected || itemDefinition == null)
        {
            SendClaimRejected(receiverViewId);
            return;
        }

        if (!TryResolvePickupReceiver(receiverViewId, out _))
        {
            SendClaimRejected(receiverViewId);
            return;
        }

        FinalizePickupNetwork(receiverViewId);
    }

    private void NotifyOwningSpawnerPickupCollected()
    {
        if (owningSpawnerViewId <= 0)
            return;

        PhotonView spawnerView = PhotonView.Find(owningSpawnerViewId);
        if (spawnerView == null)
            return;

        ItemBoxSpawner spawner = spawnerView.GetComponent<ItemBoxSpawner>();
        if (spawner == null)
            return;

        spawner.NotifyPickupCollected(photonView != null ? photonView.ViewID : 0);
    }
}
