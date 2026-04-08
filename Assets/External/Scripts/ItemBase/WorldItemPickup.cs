using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PhotonView))]
public class WorldItemPickup : MonoBehaviourPun
{
    [Header("Pickup Data")]
    [SerializeField] private ItemDefinition itemDefinition;
    [SerializeField, Min(1)] private int amount = 1;

    private bool isCollected;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected || itemDefinition == null)
            return;

        if (!TryResolvePickupReceiver(other, out IItemPickupReceiver receiver))
            return;

        if (!receiver.HasLocalPickupAuthority)
            return;

        if (!receiver.CanReceivePickup(itemDefinition, amount))
            return;

        if (!PhotonNetwork.InRoom)
        {
            CompleteOfflinePickup(receiver);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            FinalizePickupNetwork(receiver.PickupReceiverViewId);
        }
        else
        {
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

        bool received = receiver.ReceivePickup(itemDefinition, amount);
        if (!received)
            return;

        isCollected = true;
        Destroy(gameObject);
    }

    [PunRPC]
    private void RPC_RequestClaimPickup(int receiverViewId)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (isCollected)
            return;

        FinalizePickupNetwork(receiverViewId);
    }

    private void FinalizePickupNetwork(int receiverViewId)
    {
        if (isCollected)
            return;

        isCollected = true;

        photonView.RPC(
            nameof(RPC_FinalizePickup),
            RpcTarget.AllBufferedViaServer,
            receiverViewId);
    }

    [PunRPC]
    private void RPC_FinalizePickup(int receiverViewId)
    {
        if (isCollected == false)
        {
            isCollected = true;
        }

        if (TryResolvePickupReceiver(receiverViewId, out IItemPickupReceiver receiver))
        {
            if (receiver.HasLocalPickupAuthority)
            {
                receiver.ReceivePickup(itemDefinition, amount);
            }
        }

        Destroy(gameObject);
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
}