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
        Invoke(nameof(DestroySelf), lifeTime);
    }

    private void DestroySelf()
    {
        if (PhotonNetwork.InRoom && photonView != null)
        {
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}