using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;

public class RoomManager : MonoBehaviourPunCallbacks
{
    private const string HostNameRoomPropertyKey = "hostName";

    public void GameStart()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogError("PhotonNetwork : Trying to Load a level but we are not the master Client");
            return;
        }

        PhotonNetwork.LoadLevel("Map_Test_1_rla");
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
}
