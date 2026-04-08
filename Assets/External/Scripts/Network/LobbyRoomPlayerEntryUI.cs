using TMPro;
using UnityEngine;

public class LobbyRoomPlayerEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text roleText;

    [Header("Format")]
    [SerializeField] private string masterRoleLabel = "MASTER";
    [SerializeField] private string normalRoleLabel = string.Empty;
    [SerializeField] private Color defaultNameColor = Color.white;
    [SerializeField] private Color localPlayerNameColor = new Color(0.65f, 0.87f, 1f, 1f);
    [SerializeField] private Color masterRoleColor = new Color(1f, 0.84f, 0.45f, 0.95f);
    [SerializeField] private Color normalRoleColor = new Color(1f, 1f, 1f, 0.45f);

    public void SetData(string playerName, bool isMaster, bool isLocalPlayer)
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
            playerNameText.color = isLocalPlayer ? localPlayerNameColor : defaultNameColor;
        }

        if (roleText != null)
        {
            roleText.text = isMaster ? masterRoleLabel : normalRoleLabel;
            roleText.color = isMaster ? masterRoleColor : normalRoleColor;
        }
    }
}
