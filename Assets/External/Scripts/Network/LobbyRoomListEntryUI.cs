using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRoomListEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text hostNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Image selectionHighlight;

    [Header("Format")]
    [SerializeField] private string roomNameFormat = "{0}";
    [SerializeField] private string hostNameFormat = "Host: {0}";
    [SerializeField] private string playerCountFormat = "{0}/{1}";

    private LobbyManager owner;
    private string roomName;

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(NotifySelected);
        }
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(NotifySelected);
        }
    }

    public void Bind(LobbyManager lobbyManager, string targetRoomName)
    {
        owner = lobbyManager;
        roomName = targetRoomName;
    }

    public void SetData(
        string targetRoomName,
        string hostName,
        int playerCount,
        int maxPlayers,
        bool isSelected,
        bool isInteractable)
    {
        roomName = targetRoomName;

        if (roomNameText != null)
            roomNameText.text = string.Format(roomNameFormat, targetRoomName);

        if (hostNameText != null)
            hostNameText.text = string.Format(hostNameFormat, hostName);

        if (playerCountText != null)
            playerCountText.text = string.Format(playerCountFormat, playerCount, maxPlayers);

        SetSelected(isSelected);
        SetInteractable(isInteractable);
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.enabled = isSelected;
        }
    }

    public void SetInteractable(bool isInteractable)
    {
        if (selectButton != null)
        {
            selectButton.interactable = isInteractable;
        }
    }

    private void NotifySelected()
    {
        if (owner == null || string.IsNullOrWhiteSpace(roomName))
            return;

        owner.SelectRoom(roomName);
    }
}
