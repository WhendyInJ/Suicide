using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    private const string HostNameRoomPropertyKey = "hostName";

    [Header("Connection")]
    [SerializeField] private string gameVersion = "1";
    [SerializeField] private byte maxPlayersPerRoom = 4;

    [Header("UI")]
    [SerializeField] private GameObject connectPanel;
    [SerializeField] private GameObject roomListPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private TMP_Text roomTitleText;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button joinSelectedRoomButton;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button roomStartButton;
    [SerializeField] private Button roomQuitButton;
    [SerializeField] private RectTransform roomListContentRoot;
    [SerializeField] private RectTransform roomPlayerListContentRoot;
    [SerializeField] private LobbyRoomListEntryUI roomListEntryPrefab;
    [SerializeField] private LobbyRoomPlayerEntryUI roomPlayerEntryPrefab;
    [SerializeField] private TMP_Text emptyRoomListText;
    [SerializeField] private TMP_Text emptyRoomPlayerListText;
    [SerializeField] private string emptyRoomListMessage = "No Rooms";
    [SerializeField] private string emptyRoomPlayerListMessage = "No Players";
    [SerializeField] private string connectedStatusMessage = "Connected";
    [SerializeField] private string joiningLobbyStatusMessage = "Joining Lobby...";
    [SerializeField] private string joiningRoomStatusMessage = "Joining Room...";
    [SerializeField] private string creatingRoomStatusMessage = "Creating Room...";

    private readonly Dictionary<string, RoomInfo> roomInfoByName = new();
    private readonly Dictionary<string, LobbyRoomListEntryUI> entryByRoomName = new();
    private readonly Dictionary<int, LobbyRoomPlayerEntryUI> roomPlayerEntryByActorNumber = new();

    private string selectedRoomName;
    private bool isConnecting;
    private bool isRefreshingRoomList;
    private bool uiListenersRegistered;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = $"Player_{Random.Range(1000, 9999)}";
        }

        SyncNicknameInputField();
        RegisterUiListenersIfNeeded();
        RefreshViewState();
        RefreshButtonState();
        UpdateStatus("Disconnected");
    }

    private void OnDestroy()
    {
        UnregisterUiListeners();
    }

    public void Connect()
    {
        if (PhotonNetwork.InRoom)
            return;

        ApplyNicknameFromInput();

        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InLobby)
            {
                RefreshViewState();
                RefreshRoomListUI();
                return;
            }

            UpdateStatus(joiningLobbyStatusMessage);
            PhotonNetwork.JoinLobby();
            return;
        }

        isConnecting = true;
        PhotonNetwork.GameVersion = gameVersion;
        UpdateStatus("Connecting...");
        RefreshButtonState();
        PhotonNetwork.ConnectUsingSettings();
    }

    public void RefreshRooms()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Connect();
            return;
        }

        if (PhotonNetwork.InLobby)
        {
            selectedRoomName = null;
            isRefreshingRoomList = true;
            UpdateStatus("Refreshing Rooms...");
            RefreshButtonState();
            PhotonNetwork.LeaveLobby();
            return;
        }

        selectedRoomName = null;
        UpdateStatus(joiningLobbyStatusMessage);
        isRefreshingRoomList = true;
        RefreshButtonState();
        PhotonNetwork.JoinLobby();
    }

    public void CreateRoom()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
            return;

        string requestedRoomName = roomNameInputField != null
            ? roomNameInputField.text
            : string.Empty;

        string roomName = string.IsNullOrWhiteSpace(requestedRoomName)
            ? $"Room_{System.Guid.NewGuid():N}".Substring(0, 13)
            : requestedRoomName.Trim();

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            CustomRoomProperties = new ExitGames.Client.Photon.Hashtable
            {
                { HostNameRoomPropertyKey, PhotonNetwork.NickName }
            },
            CustomRoomPropertiesForLobby = new[] { HostNameRoomPropertyKey }
        };

        UpdateStatus(creatingRoomStatusMessage);
        RefreshButtonState();
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinSelectedRoom()
    {
        if (string.IsNullOrWhiteSpace(selectedRoomName))
            return;

        if (!roomInfoByName.TryGetValue(selectedRoomName, out RoomInfo roomInfo))
            return;

        if (roomInfo.RemovedFromList || !roomInfo.IsOpen || !roomInfo.IsVisible || roomInfo.PlayerCount >= roomInfo.MaxPlayers)
            return;

        UpdateStatus(joiningRoomStatusMessage);
        RefreshButtonState();
        PhotonNetwork.JoinRoom(selectedRoomName);
    }

    public void SelectRoom(string roomName)
    {
        selectedRoomName = roomName;
        RefreshRoomSelectionState();
        RefreshButtonState();
    }

    public void LeaveCurrentRoom()
    {
        if (!PhotonNetwork.InRoom)
            return;

        UpdateStatus("Leaving Room...");
        RefreshButtonState();
        PhotonNetwork.LeaveRoom();
    }

    public void StartRoomGame()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        RoomManager roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager != null)
        {
            roomManager.GameStart();
            return;
        }

        PhotonNetwork.LoadLevel("Map_Test_1_rla");
    }

    public override void OnConnectedToMaster()
    {
        isConnecting = false;
        UpdateStatus(joiningLobbyStatusMessage);
        RefreshButtonState();
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        bool wasRefreshingRoomList = isRefreshingRoomList;
        isRefreshingRoomList = false;

        if (wasRefreshingRoomList)
        {
            ClearCachedRooms();
        }

        RefreshViewState();
        UpdateStatus(connectedStatusMessage);
        RefreshButtonState();
        RefreshRoomListUI();
    }

    public override void OnLeftLobby()
    {
        if (isRefreshingRoomList && PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom)
        {
            UpdateStatus(joiningLobbyStatusMessage);
            PhotonNetwork.JoinLobby();
            return;
        }

        RefreshViewState();
        RefreshButtonState();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false;
        isRefreshingRoomList = false;
        selectedRoomName = null;
        ClearCachedRooms();
        ClearRoomPlayerEntries();
        RefreshViewState();
        UpdateStatus($"Disconnected: {cause}");
        RefreshButtonState();
    }

    public override void OnCreatedRoom()
    {
        UpdateStatus("Room Created");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Create Failed: {message}");
        RefreshButtonState();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        UpdateStatus($"Join Failed: {message}");
        RefreshButtonState();
        RefreshRoomListUI();
    }

    public override void OnJoinedRoom()
    {
        UpdateStatus($"Joined Room: {PhotonNetwork.CurrentRoom?.Name}");
        RefreshCurrentRoomUi();
        RefreshViewState();
        RefreshButtonState();
    }

    public override void OnLeftRoom()
    {
        ClearRoomPlayerEntries();
        RefreshViewState();
        RefreshButtonState();

        if (!PhotonNetwork.IsConnectedAndReady)
            return;

        if (PhotonNetwork.InLobby)
        {
            UpdateStatus(connectedStatusMessage);
            RefreshRoomListUI();
            return;
        }

        UpdateStatus(joiningLobbyStatusMessage);
        PhotonNetwork.JoinLobby();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshCurrentRoomUi();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshCurrentRoomUi();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        RefreshCurrentRoomUi();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshCurrentRoomUi();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        for (int i = 0; i < roomList.Count; i++)
        {
            RoomInfo roomInfo = roomList[i];
            if (roomInfo == null)
                continue;

            if (roomInfo.RemovedFromList || !roomInfo.IsVisible)
            {
                roomInfoByName.Remove(roomInfo.Name);
                continue;
            }

            roomInfoByName[roomInfo.Name] = roomInfo;
        }

        if (!string.IsNullOrWhiteSpace(selectedRoomName) &&
            !roomInfoByName.ContainsKey(selectedRoomName))
        {
            selectedRoomName = null;
        }

        RefreshRoomListUI();
    }

    private void RefreshRoomListUI()
    {
        if (roomListContentRoot == null || roomListEntryPrefab == null)
        {
            RefreshButtonState();
            return;
        }

        List<RoomInfo> visibleRooms = new List<RoomInfo>(roomInfoByName.Values);
        visibleRooms.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        HashSet<string> activeRoomNames = new HashSet<string>();

        for (int i = 0; i < visibleRooms.Count; i++)
        {
            RoomInfo roomInfo = visibleRooms[i];
            activeRoomNames.Add(roomInfo.Name);

            if (!entryByRoomName.TryGetValue(roomInfo.Name, out LobbyRoomListEntryUI entry) || entry == null)
            {
                entry = Instantiate(roomListEntryPrefab, roomListContentRoot);
                entryByRoomName[roomInfo.Name] = entry;
            }

            entry.gameObject.SetActive(true);
            entry.transform.SetSiblingIndex(i);
            entry.Bind(this, roomInfo.Name);
            entry.SetData(
                roomInfo.Name,
                ResolveHostName(roomInfo),
                roomInfo.PlayerCount,
                roomInfo.MaxPlayers,
                selectedRoomName == roomInfo.Name,
                roomInfo.IsOpen && roomInfo.IsVisible && roomInfo.PlayerCount < roomInfo.MaxPlayers);
        }

        List<string> staleEntries = new List<string>();
        foreach (KeyValuePair<string, LobbyRoomListEntryUI> pair in entryByRoomName)
        {
            if (!activeRoomNames.Contains(pair.Key))
            {
                if (pair.Value != null)
                    Destroy(pair.Value.gameObject);

                staleEntries.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleEntries.Count; i++)
        {
            entryByRoomName.Remove(staleEntries[i]);
        }

        if (emptyRoomListText != null)
        {
            bool hasRooms = visibleRooms.Count > 0;
            emptyRoomListText.gameObject.SetActive(!hasRooms);
            if (!hasRooms)
            {
                emptyRoomListText.text = emptyRoomListMessage;
            }
        }

        RefreshRoomSelectionState();
        RefreshButtonState();
    }

    private void RefreshCurrentRoomUi()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            ClearRoomPlayerEntries();
            RefreshButtonState();
            return;
        }

        if (roomTitleText != null)
        {
            roomTitleText.text = $"{PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})";
        }

        if (roomPlayerListContentRoot == null || roomPlayerEntryPrefab == null)
        {
            RefreshButtonState();
            return;
        }

        List<Player> players = new List<Player>(PhotonNetwork.CurrentRoom.Players.Values);
        players.Sort((left, right) => left.ActorNumber.CompareTo(right.ActorNumber));

        HashSet<int> activeActorNumbers = new HashSet<int>();

        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            activeActorNumbers.Add(player.ActorNumber);

            if (!roomPlayerEntryByActorNumber.TryGetValue(player.ActorNumber, out LobbyRoomPlayerEntryUI entry) || entry == null)
            {
                entry = Instantiate(roomPlayerEntryPrefab, roomPlayerListContentRoot);
                roomPlayerEntryByActorNumber[player.ActorNumber] = entry;
            }

            entry.gameObject.SetActive(true);
            entry.transform.SetSiblingIndex(i);
            entry.SetData(
                string.IsNullOrWhiteSpace(player.NickName) ? $"Player {player.ActorNumber}" : player.NickName,
                player.IsMasterClient,
                player == PhotonNetwork.LocalPlayer);
        }

        List<int> staleActorNumbers = new List<int>();
        foreach (KeyValuePair<int, LobbyRoomPlayerEntryUI> pair in roomPlayerEntryByActorNumber)
        {
            if (activeActorNumbers.Contains(pair.Key))
                continue;

            if (pair.Value != null)
                Destroy(pair.Value.gameObject);

            staleActorNumbers.Add(pair.Key);
        }

        for (int i = 0; i < staleActorNumbers.Count; i++)
        {
            roomPlayerEntryByActorNumber.Remove(staleActorNumbers[i]);
        }

        if (emptyRoomPlayerListText != null)
        {
            bool hasPlayers = players.Count > 0;
            emptyRoomPlayerListText.gameObject.SetActive(!hasPlayers);
            if (!hasPlayers)
            {
                emptyRoomPlayerListText.text = emptyRoomPlayerListMessage;
            }
        }

        RefreshButtonState();
    }

    private void RefreshRoomSelectionState()
    {
        foreach (KeyValuePair<string, LobbyRoomListEntryUI> pair in entryByRoomName)
        {
            if (pair.Value == null)
                continue;

            bool isSelected = pair.Key == selectedRoomName;
            bool canJoin = roomInfoByName.TryGetValue(pair.Key, out RoomInfo roomInfo) &&
                           roomInfo.IsOpen &&
                           roomInfo.IsVisible &&
                           roomInfo.PlayerCount < roomInfo.MaxPlayers;

            pair.Value.SetSelected(isSelected);
            pair.Value.SetInteractable(canJoin);
        }
    }

    private void RefreshButtonState()
    {
        bool canConnect = !PhotonNetwork.InRoom && !isConnecting && !PhotonNetwork.IsConnectedAndReady;
        bool canShowLobby = PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom;
        bool hasSelectedRoom = !string.IsNullOrWhiteSpace(selectedRoomName) &&
                               roomInfoByName.TryGetValue(selectedRoomName, out RoomInfo selectedRoom) &&
                               selectedRoom.IsOpen &&
                               selectedRoom.IsVisible &&
                               selectedRoom.PlayerCount < selectedRoom.MaxPlayers;

        if (connectButton != null)
            connectButton.interactable = canConnect || canShowLobby;

        if (joinSelectedRoomButton != null)
            joinSelectedRoomButton.interactable = PhotonNetwork.InLobby && hasSelectedRoom;

        if (createRoomButton != null)
            createRoomButton.interactable = PhotonNetwork.InLobby;

        if (refreshButton != null)
            refreshButton.interactable = !PhotonNetwork.InRoom && !isRefreshingRoomList;

        if (roomStartButton != null)
            roomStartButton.interactable = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;

        if (roomQuitButton != null)
            roomQuitButton.interactable = PhotonNetwork.InRoom;

        if (nicknameInputField != null)
            nicknameInputField.interactable = !PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom && !isConnecting;
    }

    private void RefreshViewState()
    {
        if (connectPanel != null)
            connectPanel.SetActive(
                !PhotonNetwork.IsConnectedAndReady ||
                (!PhotonNetwork.InLobby && !PhotonNetwork.InRoom && !isRefreshingRoomList));

        if (roomListPanel != null)
            roomListPanel.SetActive(
                PhotonNetwork.IsConnectedAndReady &&
                !PhotonNetwork.InRoom &&
                (PhotonNetwork.InLobby || isRefreshingRoomList));

        if (roomPanel != null)
            roomPanel.SetActive(PhotonNetwork.InRoom);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ClearCachedRooms()
    {
        roomInfoByName.Clear();
        selectedRoomName = null;

        foreach (KeyValuePair<string, LobbyRoomListEntryUI> pair in entryByRoomName)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        entryByRoomName.Clear();
        RefreshButtonState();
    }

    private void ClearRoomPlayerEntries()
    {
        foreach (KeyValuePair<int, LobbyRoomPlayerEntryUI> pair in roomPlayerEntryByActorNumber)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        roomPlayerEntryByActorNumber.Clear();

        if (emptyRoomPlayerListText != null)
        {
            emptyRoomPlayerListText.gameObject.SetActive(true);
            emptyRoomPlayerListText.text = emptyRoomPlayerListMessage;
        }

        if (roomTitleText != null)
        {
            roomTitleText.text = "Room";
        }
    }

    private void ApplyNicknameFromInput()
    {
        string requestedNickname = nicknameInputField != null
            ? nicknameInputField.text
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(requestedNickname))
        {
            PhotonNetwork.NickName = requestedNickname.Trim();
        }
        else if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = $"Player_{Random.Range(1000, 9999)}";
        }

        SyncNicknameInputField();
    }

    private void SyncNicknameInputField()
    {
        if (nicknameInputField == null)
            return;

        nicknameInputField.text = PhotonNetwork.NickName;
    }

    private static string ResolveHostName(RoomInfo roomInfo)
    {
        if (roomInfo == null)
            return string.Empty;

        if (roomInfo.CustomProperties != null &&
            roomInfo.CustomProperties.TryGetValue(HostNameRoomPropertyKey, out object hostNameValue) &&
            hostNameValue is string hostName &&
            !string.IsNullOrWhiteSpace(hostName))
        {
            return hostName;
        }

        return "Unknown Host";
    }

    private void RegisterUiListenersIfNeeded()
    {
        if (uiListenersRegistered)
            return;

        RegisterButton(connectButton, Connect);
        RegisterButton(joinSelectedRoomButton, JoinSelectedRoom);
        RegisterButton(createRoomButton, CreateRoom);
        RegisterButton(refreshButton, RefreshRooms);
        RegisterButton(roomStartButton, StartRoomGame);
        RegisterButton(roomQuitButton, LeaveCurrentRoom);

        uiListenersRegistered = true;
    }

    private void UnregisterUiListeners()
    {
        if (!uiListenersRegistered)
            return;

        UnregisterButton(connectButton, Connect);
        UnregisterButton(joinSelectedRoomButton, JoinSelectedRoom);
        UnregisterButton(createRoomButton, CreateRoom);
        UnregisterButton(refreshButton, RefreshRooms);
        UnregisterButton(roomStartButton, StartRoomGame);
        UnregisterButton(roomQuitButton, LeaveCurrentRoom);

        uiListenersRegistered = false;
    }

    private void RegisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.AddListener(action);
    }

    private void UnregisterButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
    }
}
