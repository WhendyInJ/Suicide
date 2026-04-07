using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviourPunCallbacks
{
    [Tooltip("The prefab to use for representing the player")]
public GameObject playerPrefab;

    void Start()
    {
        if (playerPrefab == null)
{
    Debug.LogError("<Color=Red><a>Missing</a></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'",this);
}
else
{
    Debug.LogFormat("We are Instantiating LocalPlayer from {0}", SceneManager.GetActiveScene().name);
    // we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
    PhotonNetwork.Instantiate(this.playerPrefab.name, new Vector3(0f,5f,0f), Quaternion.identity, 0);
}
    }
}
