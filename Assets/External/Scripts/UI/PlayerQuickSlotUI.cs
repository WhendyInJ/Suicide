using System;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerQuickSlotUI : MonoBehaviour
{
    [Serializable]
    private sealed class SlotUI
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image frameImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI countText;

        public RectTransform Root => root;
        public Image FrameImage => frameImage;
        public Image IconImage => iconImage;
        public TextMeshProUGUI CountText => countText;
    }

    [Header("References")]
    [SerializeField] private PlayerItemInventory inventory;
    [SerializeField] private PlayerItemRunner itemRunner;
    [SerializeField] private PlayerInputSource inputSource;
    [SerializeField] private bool autoResolveLocalPlayerReferences = true;
    [SerializeField, Min(0.1f)] private float resolveRetryInterval = 0.5f;

    [Header("Slots")]
    [SerializeField] private SlotUI slot1 = new SlotUI();
    [SerializeField] private SlotUI slot2 = new SlotUI();

    [Header("Selection Visual")]
    [SerializeField] private Vector3 selectedScale = new Vector3(1.15f, 1.15f, 1f);
    [SerializeField] private Vector3 unselectedScale = new Vector3(0.88f, 0.88f, 1f);
    [SerializeField] private Vector3 aimingScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField, Min(0.01f)] private float scaleLerpSpeed = 12f;

    [Header("Colors")]
    [SerializeField] private Color selectedFrameColor = Color.white;
    [SerializeField] private Color unselectedFrameColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color aimingFrameColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Display")]
    [SerializeField] private bool hideIconWhenEmpty = true;
    [SerializeField] private bool showCountText = true;
    [SerializeField] private bool hideCountWhenOne = true;
    [SerializeField] private bool requireLocalAuthority = true;

    private SlotUI[] slots;
    private float nextResolveTime;

    private void Reset()
    {
        ResolveReferences(true);
        CacheSlots();
    }

    private void Awake()
    {
        ResolveReferences(true);
        CacheSlots();
        RefreshAllUI();
        SnapSlotScalesImmediately();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        RefreshAllUI();
        SnapSlotScalesImmediately();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        TryResolveLocalPlayerReferencesIfNeeded();
        AnimateSlotScales();
    }

    public void RefreshAllUI()
    {
        CacheSlots();

        if (!CanRenderUI())
        {
            ClearAllUI();
            return;
        }

        int selectedSlotIndex = inventory != null ? inventory.SelectedSlotIndex : -1;
        bool isAimingSelectedItem = itemRunner != null && itemRunner.IsAimingItem;

        RefreshSlot(slot1, 0, selectedSlotIndex == 0, isAimingSelectedItem && selectedSlotIndex == 0);
        RefreshSlot(slot2, 1, selectedSlotIndex == 1, isAimingSelectedItem && selectedSlotIndex == 1);
    }

    private void ResolveReferences(bool allowAutoResolve)
    {
        if (allowAutoResolve && autoResolveLocalPlayerReferences && TryResolveLocalPlayerReferences())
            return;

        if (inventory == null)
            inventory = GetComponentInParent<PlayerItemInventory>();

        if (itemRunner == null)
            itemRunner = GetComponentInParent<PlayerItemRunner>();

        if (inputSource == null)
            inputSource = GetComponentInParent<PlayerInputSource>();
    }

    private void CacheSlots()
    {
        if (slots != null && slots.Length == 2)
            return;

        slots = new[] { slot1, slot2 };
    }

    private void SubscribeEvents()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += HandleInventoryChanged;
            inventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
        }

        if (itemRunner != null)
        {
            itemRunner.OnAimStateChanged += HandleAimStateChanged;
        }
    }

    private void UnsubscribeEvents()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= HandleInventoryChanged;
            inventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
        }

        if (itemRunner != null)
        {
            itemRunner.OnAimStateChanged -= HandleAimStateChanged;
        }
    }

    private void HandleInventoryChanged()
    {
        RefreshAllUI();
    }

    private void HandleSelectedSlotChanged(int _)
    {
        RefreshAllUI();
    }

    private void HandleAimStateChanged(bool _)
    {
        RefreshAllUI();
    }

    private void TryResolveLocalPlayerReferencesIfNeeded()
    {
        if (!autoResolveLocalPlayerReferences)
            return;

        if (HasBoundLocalPlayerReferences())
            return;

        if (Time.unscaledTime < nextResolveTime)
            return;

        nextResolveTime = Time.unscaledTime + resolveRetryInterval;

        if (!TryResolveLocalPlayerReferences())
            return;

        RefreshAllUI();
        SnapSlotScalesImmediately();
    }

    private void RefreshSlot(SlotUI slot, int slotIndex, bool isSelected, bool isAiming)
    {
        if (slot == null)
            return;

        if (!TryGetSlotInfo(slotIndex, out ItemDefinition definition, out int count))
        {
            ApplyEmptySlot(slot, isSelected, isAiming);
            return;
        }

        ApplyFilledSlot(slot, definition, count, isSelected, isAiming);
    }

    private bool TryGetSlotInfo(int slotIndex, out ItemDefinition definition, out int count)
    {
        definition = null;
        count = 0;

        if (inventory == null)
            return false;

        if (slotIndex < 0 || slotIndex >= Mathf.Min(2, inventory.SlotCount))
            return false;

        return inventory.TryGetSlotInfo(slotIndex, out definition, out count) &&
               definition != null &&
               count > 0;
    }

    private void ApplyFilledSlot(SlotUI slot, ItemDefinition definition, int count, bool isSelected, bool isAiming)
    {
        Image frameImage = slot.FrameImage;
        if (frameImage != null)
        {
            frameImage.enabled = true;
            frameImage.color = GetFrameColor(isSelected, isAiming);
        }

        ApplySlotIcon(slot.IconImage, definition.Icon, true);
        ApplyCountText(slot.CountText, count);
    }

    private void ApplyEmptySlot(SlotUI slot, bool isSelected, bool isAiming)
    {
        Image frameImage = slot.FrameImage;
        if (frameImage != null)
        {
            frameImage.enabled = true;
            frameImage.color = GetFrameColor(isSelected, isAiming);
        }

        ApplySlotIcon(slot.IconImage, null, false);
        ApplyCountText(slot.CountText, 0);
    }

    private void ApplySlotIcon(Image iconImage, Sprite iconSprite, bool hasItem)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = iconSprite;
        iconImage.overrideSprite = iconSprite;
        iconImage.color = hasItem ? iconColor : emptyIconColor;
        iconImage.enabled = hasItem
            ? (iconSprite != null || !hideIconWhenEmpty)
            : !hideIconWhenEmpty;
    }

    private void ApplyCountText(TextMeshProUGUI countText, int count)
    {
        if (countText == null)
            return;

        if (!showCountText || count <= 0 || (hideCountWhenOne && count <= 1))
        {
            countText.text = string.Empty;
            countText.enabled = false;
            return;
        }

        countText.text = count.ToString();
        countText.enabled = true;
    }

    private Color GetFrameColor(bool isSelected, bool isAiming)
    {
        if (isAiming)
            return aimingFrameColor;

        return isSelected ? selectedFrameColor : unselectedFrameColor;
    }

    private Vector3 GetTargetScale(int slotIndex)
    {
        if (inventory == null)
            return unselectedScale;

        bool isSelected = inventory.SelectedSlotIndex == slotIndex;
        bool isAiming = isSelected && itemRunner != null && itemRunner.IsAimingItem;

        if (isAiming)
            return aimingScale;

        return isSelected ? selectedScale : unselectedScale;
    }

    private void AnimateSlotScales()
    {
        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            AnimateSingleSlotScale(slots[i], GetTargetScale(i));
        }
    }

    private void AnimateSingleSlotScale(SlotUI slot, Vector3 targetScale)
    {
        if (slot == null || slot.Root == null)
            return;

        slot.Root.localScale = Vector3.Lerp(
            slot.Root.localScale,
            targetScale,
            Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    private void SnapSlotScalesImmediately()
    {
        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].Root == null)
                continue;

            slots[i].Root.localScale = GetTargetScale(i);
        }
    }

    private void ClearAllUI()
    {
        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            ApplyEmptySlot(slots[i], false, false);

            if (slots[i] != null && slots[i].Root != null)
            {
                slots[i].Root.localScale = unselectedScale;
            }
        }
    }

    private bool CanRenderUI()
    {
        if (inventory == null)
            return false;

        if (!requireLocalAuthority)
            return true;

        if (inputSource == null)
            return true;

        return inputSource.HasLocalAuthority;
    }

    private bool TryResolveLocalPlayerReferences()
    {
        PlayerItemRunner resolvedRunner = FindLocalOwnedComponent<PlayerItemRunner>();
        if (resolvedRunner == null)
            return false;

        PlayerItemInventory resolvedInventory = FindPlayerScopedComponent<PlayerItemInventory>(resolvedRunner);
        if (resolvedInventory == null)
            return false;

        PlayerInputSource resolvedInputSource = FindPlayerScopedComponent<PlayerInputSource>(resolvedRunner);
        AssignReferences(resolvedInventory, resolvedRunner, resolvedInputSource);
        return true;
    }

    private void AssignReferences(
        PlayerItemInventory newInventory,
        PlayerItemRunner newItemRunner,
        PlayerInputSource newInputSource)
    {
        bool changed =
            inventory != newInventory ||
            itemRunner != newItemRunner ||
            inputSource != newInputSource;

        if (!changed)
            return;

        UnsubscribeEvents();

        inventory = newInventory;
        itemRunner = newItemRunner;
        inputSource = newInputSource;

        if (isActiveAndEnabled)
        {
            SubscribeEvents();
        }
    }

    private bool HasBoundLocalPlayerReferences()
    {
        if (inventory == null || itemRunner == null)
            return false;

        if (!PhotonNetwork.InRoom)
            return true;

        PhotonView runnerView = itemRunner.GetComponent<PhotonView>();
        if (runnerView == null)
            runnerView = itemRunner.GetComponentInParent<PhotonView>();

        if (runnerView == null || !runnerView.IsMine)
            return false;

        if (inputSource == null)
            return true;

        return inputSource.HasLocalAuthority;
    }

    private static T FindLocalOwnedComponent<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (!IsSceneInstance(candidate))
                continue;

            PhotonView photonView = candidate.GetComponent<PhotonView>();
            if (photonView == null)
                photonView = candidate.GetComponentInParent<PhotonView>();

            if (PhotonNetwork.InRoom)
            {
                if (photonView != null && photonView.IsMine)
                    return candidate;

                continue;
            }

            return candidate;
        }

        return null;
    }

    private static T FindPlayerScopedComponent<T>(Component anchor) where T : Component
    {
        if (anchor == null)
            return null;

        T component = anchor.GetComponent<T>();
        if (component != null)
            return component;

        component = anchor.GetComponentInParent<T>();
        if (component != null)
            return component;

        return anchor.GetComponentInChildren<T>(true);
    }

    private static bool IsSceneInstance(Component component)
    {
        if (component == null)
            return false;

        if (!component.gameObject.scene.IsValid())
            return false;

        return (component.hideFlags & HideFlags.NotEditable) == 0;
    }
}
