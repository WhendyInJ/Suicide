using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;

public class RoomManager : MonoBehaviourPunCallbacks
{
    private const string HostNameRoomPropertyKey = "hostName";
    private const string GameStartedRoomPropertyKey = "gameStarted";

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Map_Test_1_rla";

    public void GameStart()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogError("PhotonNetwork : Trying to Load a level but we are not the master Client");
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("RoomManager: gameSceneName is empty.", this);
            return;
        }

        LockRoomForGameStart();
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public override void OnJoinedRoom()
    {
        UpdateHostRoomPropertyIfMaster();
    }

    public override void OnPlayerEnteredRoom(Player other)
    {
        Debug.LogFormat("OnPlayerEnteredRoom() {0}", other.NickName); // not seen if you're the player connecting

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom
        }
    }

    public override void OnPlayerLeftRoom(Player other)
    {
        Debug.LogFormat("OnPlayerLeftRoom() {0}", other.NickName); // seen when other disconnects

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.LogFormat("OnPlayerLeftRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdateHostRoomPropertyIfMaster();
    }

    private void UpdateHostRoomPropertyIfMaster()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable changedProperties = new Hashtable
        {
            { HostNameRoomPropertyKey, PhotonNetwork.NickName }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changedProperties);
    }

    private void LockRoomForGameStart()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;

        Hashtable changedProperties = new Hashtable
        {
            { GameStartedRoomPropertyKey, true }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changedProperties);
    }
}
