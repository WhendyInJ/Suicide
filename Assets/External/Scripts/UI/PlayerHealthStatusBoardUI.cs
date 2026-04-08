using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class PlayerHealthStatusBoardUI : MonoBehaviourPunCallbacks
{
    private sealed class EntryBinding
    {
        public PlayerHealth Health;
        public PlayerHealthStatusEntryUI EntryUI;
    }

    [Header("References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private PlayerHealthStatusEntryUI entryPrefab;
    [SerializeField] private bool includeInactiveHealthObjects = false;

    [Header("Fallback UI")]
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private string emptyStateMessage = "No Players";

    private readonly Dictionary<PlayerHealth, EntryBinding> bindingsByHealth = new();
    private readonly List<PlayerHealth> sortedHealthBuffer = new();

    private void Reset()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;
    }

    private void Awake()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;
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
        RefreshDisplayOrder();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshDisplayOrder();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        RefreshAllEntryTexts();
    }

    private void HandlePlayerHealthRegistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        EnsureEntryBound(playerHealth);
        RefreshDisplayOrder();
    }

    private void HandlePlayerHealthUnregistered(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        UnbindEntry(playerHealth);
        RefreshDisplayOrder();
    }

    private void RebuildAllEntries()
    {
        UnbindAllEntries();

        PlayerHealth[] healthObjects = FindObjectsByType<PlayerHealth>(
            includeInactiveHealthObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < healthObjects.Length; i++)
        {
            EnsureEntryBound(healthObjects[i]);
        }

        RefreshDisplayOrder();
    }

    private void EnsureEntryBound(PlayerHealth playerHealth)
    {
        if (playerHealth == null || bindingsByHealth.ContainsKey(playerHealth))
            return;

        if (contentRoot == null || entryPrefab == null)
            return;

        PlayerHealthStatusEntryUI entryUI = Instantiate(entryPrefab, contentRoot);
        entryUI.name = $"{entryPrefab.name}_{ResolvePlayerName(playerHealth)}";

        EntryBinding binding = new EntryBinding
        {
            Health = playerHealth,
            EntryUI = entryUI
        };

        bindingsByHealth.Add(playerHealth, binding);
        playerHealth.HealthChanged += HandleHealthChanged;

        RefreshEntry(binding);
    }

    private void UnbindEntry(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return;

        if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
            return;

        playerHealth.HealthChanged -= HandleHealthChanged;
        bindingsByHealth.Remove(playerHealth);

        if (binding.EntryUI != null)
            Destroy(binding.EntryUI.gameObject);
    }

    private void UnbindAllEntries()
    {
        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            if (pair.Key != null)
            {
                pair.Key.HealthChanged -= HandleHealthChanged;
            }

            if (pair.Value != null && pair.Value.EntryUI != null)
            {
                Destroy(pair.Value.EntryUI.gameObject);
            }
        }

        bindingsByHealth.Clear();
    }

    private void HandleHealthChanged(PlayerHealth playerHealth, HealthChangedEventArgs args)
    {
        if (playerHealth == null)
            return;

        if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
            return;

        RefreshEntry(binding);
    }

    private void RefreshEntry(EntryBinding binding)
    {
        if (binding == null || binding.Health == null || binding.EntryUI == null)
            return;

        binding.EntryUI.SetData(
            ResolvePlayerName(binding.Health),
            binding.Health.CurrentHealth);
    }

    private void RefreshAllEntryTexts()
    {
        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            RefreshEntry(pair.Value);
        }
    }

    private void RefreshDisplayOrder()
    {
        sortedHealthBuffer.Clear();

        foreach (KeyValuePair<PlayerHealth, EntryBinding> pair in bindingsByHealth)
        {
            if (pair.Key != null)
            {
                sortedHealthBuffer.Add(pair.Key);
            }
        }

        sortedHealthBuffer.Sort(CompareHealthEntries);

        for (int i = 0; i < sortedHealthBuffer.Count; i++)
        {
            PlayerHealth playerHealth = sortedHealthBuffer[i];
            if (!bindingsByHealth.TryGetValue(playerHealth, out EntryBinding binding))
                continue;

            if (binding.EntryUI != null)
            {
                binding.EntryUI.transform.SetSiblingIndex(i);
            }

            RefreshEntry(binding);
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
