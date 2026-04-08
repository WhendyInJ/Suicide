using Photon.Pun;
using UnityEngine;

public class NetworkEffectAutoDestroy : MonoBehaviour
{
    [SerializeField, Min(0f)] private float lifeTime = 1.5f;

    private PhotonView photonView;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
    }

    private void OnEnable()
    {
        CancelInvoke(nameof(DestroySelf));

        if (lifeTime <= 0f)
        {
            DestroySelf();
            return;
        }

        Invoke(nameof(DestroySelf), lifeTime);
    }

    private void DestroySelf()
    {
        if (IsPhotonManagedRuntimeObject())
        {
            if (photonView.IsMine || (photonView.IsRoomView && PhotonNetwork.IsMasterClient))
            {
                PhotonNetwork.Destroy(gameObject);
            }

            return;
        }

        Destroy(gameObject);
    }

    private bool IsPhotonManagedRuntimeObject()
    {
        if (!PhotonNetwork.InRoom || photonView == null || photonView.ViewID <= 0)
            return false;

        return photonView.InstantiationId > 0 || photonView.IsRoomView;
    }
}
