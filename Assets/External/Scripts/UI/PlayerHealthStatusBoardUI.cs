using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerHealthStatusBoardUI : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private List<PlayerHealthStatusEntryUI> entrySlots = new();
    [SerializeField] private bool includeInactiveHealthObjects = false;

    [Header("Fallback UI")]
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private string emptyStateMessage = "No Players";

    private readonly HashSet<PlayerHealth> registeredHealths = new();
    private readonly List<PlayerHealth> sortedHealthBuffer = new();

    private void Reset()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;

        CacheEntrySlotsFromChildrenIfNeeded();
    }

    private void Awake()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;

        CacheEntrySlotsFromChildrenIfNeeded();
        ApplyEmptyStateToAllSlots();
    }

    public override void OnEnable()
    {
        base.OnEnable();

        PlayerHealth.Registered += HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered += HandlePlayerHealthUnregistered;

        RebuildAllEntries();
    }

    public override void OnDisable()
    {
        PlayerHealth.Registered -= HandlePlayerHealthRegistered;
        PlayerHealth.Unregistered -= HandlePlayerHealthUnregistered;

        UnbindAllEntries();
        base.OnDisable();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshBoard();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshBoard();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        RefreshBoard();
    }

    private void HandlePlayerHealthRegistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        if (registeredHealths.Add(playerHealth))
            playerHealth.HealthChanged += HandleHealthChanged;

        RefreshBoard();
    }

    private void HandlePlayerHealthUnregistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        if (registeredHealths.Remove(playerHealth))
            playerHealth.HealthChanged -= HandleHealthChanged;

        RefreshBoard();
    }

    private void RebuildAllEntries()
    {
        UnbindAllEntries();

        PlayerHealth[] healthObjects = FindObjectsByType<PlayerHealth>(
            includeInactiveHealthObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < healthObjects.Length; i++)
        {
            PlayerHealth playerHealth = healthObjects[i];
            if (playerHealth == null || !registeredHealths.Add(playerHealth))
                continue;

            playerHealth.HealthChanged += HandleHealthChanged;
        }

        RefreshBoard();
    }

    private void UnbindAllEntries()
    {
        foreach (PlayerHealth playerHealth in registeredHealths)
        {
            if (playerHealth != null)
                playerHealth.HealthChanged -= HandleHealthChanged;
        }

        registeredHealths.Clear();
        ApplyEmptyStateToAllSlots();
    }

    private void HandleHealthChanged(PlayerHealth playerHealth, HealthChangedEventArgs args)
    {
        if (playerHealth == null)
            return;

        RefreshBoard();
    }

    private void RefreshBoard()
    {
        sortedHealthBuffer.Clear();

        foreach (PlayerHealth playerHealth in registeredHealths)
        {
            if (playerHealth != null)
                sortedHealthBuffer.Add(playerHealth);
        }

        sortedHealthBuffer.Sort(CompareHealthEntries);

        int slotCount = entrySlots != null ? entrySlots.Count : 0;

        for (int i = 0; i < slotCount; i++)
        {
            PlayerHealthStatusEntryUI entrySlot = entrySlots[i];
            if (entrySlot == null)
                continue;

            bool hasAssignedPlayer = i < sortedHealthBuffer.Count;
            entrySlot.gameObject.SetActive(hasAssignedPlayer);

            if (!hasAssignedPlayer)
            {
                entrySlot.SetData(string.Empty, 0f);
                continue;
            }

            PlayerHealth playerHealth = sortedHealthBuffer[i];
            entrySlot.transform.SetSiblingIndex(i);
            entrySlot.SetData(
                ResolvePlayerName(playerHealth),
                playerHealth.CurrentHealth);
        }

        if (emptyStateText != null)
        {
            bool hasEntries = sortedHealthBuffer.Count > 0;
            emptyStateText.gameObject.SetActive(!hasEntries);
            if (!hasEntries)
            {
                emptyStateText.text = emptyStateMessage;
            }
        }
    }

    private void CacheEntrySlotsFromChildrenIfNeeded()
    {
        if (entrySlots != null && entrySlots.Count > 0)
            return;

        entrySlots = new List<PlayerHealthStatusEntryUI>();
        if (contentRoot == null)
            return;

        PlayerHealthStatusEntryUI[] discoveredSlots = contentRoot.GetComponentsInChildren<PlayerHealthStatusEntryUI>(true);
        for (int i = 0; i < discoveredSlots.Length; i++)
        {
            PlayerHealthStatusEntryUI entrySlot = discoveredSlots[i];
            if (entrySlot == null)
                continue;

            entrySlots.Add(entrySlot);
        }
    }

    private void ApplyEmptyStateToAllSlots()
    {
        if (entrySlots == null)
            return;

        for (int i = 0; i < entrySlots.Count; i++)
        {
            PlayerHealthStatusEntryUI entrySlot = entrySlots[i];
            if (entrySlot == null)
                continue;

            entrySlot.SetData(string.Empty, 0f);
            entrySlot.gameObject.SetActive(false);
        }
    }

    private static int CompareHealthEntries(PlayerHealth left, PlayerHealth right)
    {
        if (left == right)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int actorCompare = left.OwnerActorNumber.CompareTo(right.OwnerActorNumber);
        if (actorCompare != 0)
            return actorCompare;

        return string.CompareOrdinal(ResolvePlayerName(left), ResolvePlayerName(right));
    }

    private static string ResolvePlayerName(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return string.Empty;

        PhotonView photonView = playerHealth.GetComponent<PhotonView>();
        if (photonView == null)
            photonView = playerHealth.GetComponentInParent<PhotonView>();

        if (photonView != null && photonView.Owner != null)
            return photonView.Owner.NickName;

        return playerHealth.name;
    }
}
