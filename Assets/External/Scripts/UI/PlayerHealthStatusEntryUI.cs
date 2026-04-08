using TMPro;
using UnityEngine;

public class PlayerHealthStatusEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text healthValueText;

    [Header("Format")]
    [SerializeField] private string fallbackPlayerName = "Unknown";
    [SerializeField] private string healthFormat = "{0}";

    public void SetPlayerName(string playerName)
    {
        if (playerNameText == null)
            return;

        playerNameText.text = string.IsNullOrWhiteSpace(playerName)
            ? fallbackPlayerName
            : playerName;
    }

    public void SetHealth(float currentHealth)
    {
        if (healthValueText == null)
            return;

        int displayedHealth = Mathf.CeilToInt(Mathf.Max(0f, currentHealth));
        healthValueText.text = string.Format(healthFormat, displayedHealth);
    }

    public void SetData(string playerName, float currentHealth)
    {
        SetPlayerName(playerName);
        SetHealth(currentHealth);
    }
}
