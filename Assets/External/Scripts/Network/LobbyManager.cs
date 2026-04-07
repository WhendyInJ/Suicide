using Photon.Pun;
using UnityEngine;
using Photon.Realtime;
public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
[SerializeField]
private byte maxPlayersPerRoom = 4;
    string gameVersion = "1";

    void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    void Start()
    {
    }

    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log("Connected to RandomRoom");
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            PhotonNetwork.GameVersion = gameVersion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
{
    Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN");
}

public override void OnDisconnected(DisconnectCause cause)
{
    Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
}
public override void OnJoinRandomFailed(short returnCode, string message)
{
    Debug.Log("PUN Basics Tutorial/Launcher:OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

    // #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
    PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = maxPlayersPerRoom });
}

public override void OnJoinedRoom()
{
    Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
}

}
