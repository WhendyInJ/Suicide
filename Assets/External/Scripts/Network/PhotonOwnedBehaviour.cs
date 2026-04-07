using Photon.Pun;
using UnityEngine;

public abstract class PhotonOwnedBehaviour : MonoBehaviour
{
    protected PhotonView CachedPhotonView { get; private set; }

    protected bool HasLocalAuthority
    {
        get
        {
            if (!PhotonNetwork.InRoom)
                return true;

            return CachedPhotonView != null && CachedPhotonView.IsMine;
        }
    }

    protected virtual void Awake()
    {
        CachedPhotonView = GetComponent<PhotonView>();

        if (CachedPhotonView == null)
            CachedPhotonView = GetComponentInParent<PhotonView>();
    }
}