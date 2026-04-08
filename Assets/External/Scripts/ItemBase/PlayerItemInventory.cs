using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PlayerItemInventory : MonoBehaviour, IItemPickupReceiver, IPunObservable
{
    private const int MaxItemsPerSlot = 1;

    [Serializable]
    public sealed class SlotData
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Min(0)] private int itemCount;

        public ItemDefinition ItemDefinition => itemDefinition;
        public int ItemCount => itemCount;
        public bool IsEmpty => itemDefinition == null || itemCount <= 0;

        public bool IsSameItem(ItemDefinition targetDefinition)
        {
            return itemDefinition == targetDefinition && targetDefinition != null;
        }

        public bool CanStack(ItemDefinition targetDefinition)
        {
            return false;
        }

        public int GetRemainingSpace()
        {
            if (IsEmpty)
                return 0;

            return Mathf.Max(0, MaxItemsPerSlot - itemCount);
        }

        public void SetSlot(ItemDefinition newDefinition, int newCount)
        {
            itemDefinition = newDefinition;
            itemCount = Mathf.Max(0, newCount);

            if (itemDefinition == null || itemCount <= 0)
            {
                ClearSlot();
            }
        }

        public int AddCount(int addCount)
        {
            if (IsEmpty || addCount <= 0)
                return 0;

            int applied = Mathf.Min(addCount, GetRemainingSpace());
            itemCount += applied;
            return applied;
        }

        public int RemoveCount(int removeCount)
        {
            if (IsEmpty || removeCount <= 0)
                return 0;

            int applied = Mathf.Min(removeCount, itemCount);
            itemCount -= applied;

            if (itemCount <= 0)
            {
                ClearSlot();
            }

            return applied;
        }

        public void ClearSlot()
        {
            itemDefinition = null;
            itemCount = 0;
        }
    }

    private const int FixedSlotCount = 2;

    private const int NoneSelectedIndex = -1;

    [Header("Runtime")]
    [SerializeField] private int selectedSlotIndex = NoneSelectedIndex;
    [SerializeField] private List<SlotData> slotList = new List<SlotData>(FixedSlotCount);

    public event Action OnInventoryChanged;
    public event Action<int> OnSelectedSlotChanged;

    public int PickupReceiverViewId => photonView != null ? photonView.ViewID : 0;
    public bool HasLocalPickupAuthority => !PhotonNetwork.InRoom || (photonView != null && photonView.IsMine);

    public int SelectedSlotIndex => selectedSlotIndex;
    public bool HasSelection => IsValidSelectedIndex(selectedSlotIndex);
    public int SlotCount => FixedSlotCount;

    private PhotonView photonView;

    private void Reset()
    {
        photonView = GetComponent<PhotonView>();
    }

    private void Awake()
    {
        if (photonView == null)
            photonView = GetComponent<PhotonView>();

        InitializeSlots();
        selectedSlotIndex = NoneSelectedIndex;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, NoneSelectedIndex, FixedSlotCount - 1);
    }
#endif

    public void InitializeSlots()
    {
        if (slotList == null)
        {
            slotList = new List<SlotData>(FixedSlotCount);
        }

        while (slotList.Count < FixedSlotCount)
        {
            slotList.Add(new SlotData());
        }

        if (slotList.Count > FixedSlotCount)
        {
            slotList.RemoveRange(FixedSlotCount, slotList.Count - FixedSlotCount);
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, NoneSelectedIndex, FixedSlotCount - 1);
        if (selectedSlotIndex != NoneSelectedIndex && slotList[selectedSlotIndex].IsEmpty)
        {
            selectedSlotIndex = NoneSelectedIndex;
        }

        RaiseInventoryChanged();
        RaiseSelectedSlotChanged();
    }

    public bool CanReceivePickup(ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0)
            return false;

        return GetAddableCount(definition) > 0;
    }

    public bool ReceivePickup(ItemDefinition definition, int amount)
    {
        if (!HasLocalPickupAuthority)
            return false;

        return AddItem(definition, 1) > 0;
    }

    public int AddItem(ItemDefinition definition, int addCount)
    {
        if (definition == null || addCount <= 0)
            return 0;

        InitializeSlotsIfNeeded();
        ClearEmptySelection();

        int remaining = Mathf.Min(MaxItemsPerSlot, addCount);
        int totalAdded = 0;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].IsEmpty)
            {
                int placeCount = Mathf.Min(MaxItemsPerSlot, remaining);
                slotList[i].SetSlot(definition, placeCount);

                remaining -= placeCount;
                totalAdded += placeCount;

                if (remaining <= 0)
                {
                    RaiseInventoryChanged();
                    return totalAdded;
                }
            }
        }

        if (totalAdded > 0)
        {
            RaiseInventoryChanged();
        }

        return totalAdded;
    }

    public int GetAddableCount(ItemDefinition definition)
    {
        if (definition == null)
            return 0;

        InitializeSlotsIfNeeded();

        int addable = 0;

        for (int i = 0; i < slotList.Count; i++)
        {
            SlotData slot = slotList[i];

            if (slot.IsEmpty)
            {
                addable += MaxItemsPerSlot;
            }
        }

        return addable;
    }

    public bool TryConsumeSelectedItem(int count = 1)
    {
        return TryConsumeSlot(selectedSlotIndex, count);
    }

    public bool TryConsumeSlot(int slotIndex, int count = 1)
    {
        if (count <= 0 || !IsValidIndex(slotIndex))
            return false;

        SlotData slot = slotList[slotIndex];
        if (slot.IsEmpty || slot.ItemCount < count)
            return false;

        slot.RemoveCount(count);
        RaiseInventoryChanged();
        return true;
    }

    public bool TrySelectSlot(int slotIndex)
    {
        if (!IsValidIndex(slotIndex))
            return false;

        SlotData slot = slotList[slotIndex];
        if (slot == null || slot.IsEmpty)
            return false;

        if (selectedSlotIndex == slotIndex)
            return true;

        selectedSlotIndex = slotIndex;
        RaiseSelectedSlotChanged();
        RaiseInventoryChanged();
        return true;
    }

    public void ClearSelection()
    {
        if (selectedSlotIndex == NoneSelectedIndex)
            return;

        selectedSlotIndex = NoneSelectedIndex;
        RaiseSelectedSlotChanged();
        RaiseInventoryChanged();
    }

    public bool TryGetSelectedItem(out ItemDefinition definition, out int count)
    {
        return TryGetSlotInfo(selectedSlotIndex, out definition, out count);
    }

    public bool TryGetSlotInfo(int slotIndex, out ItemDefinition definition, out int count)
    {
        definition = null;
        count = 0;

        if (!IsValidIndex(slotIndex))
            return false;

        SlotData slot = slotList[slotIndex];
        if (slot.IsEmpty)
            return false;

        definition = slot.ItemDefinition;
        count = slot.ItemCount;
        return true;
    }

    public int GetTotalItemCount(ItemDefinition definition)
    {
        if (definition == null)
            return 0;

        int total = 0;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].IsSameItem(definition))
            {
                total += slotList[i].ItemCount;
            }
        }

        return total;
    }

    public bool HasItem(ItemDefinition definition, int requiredCount = 1)
    {
        return GetTotalItemCount(definition) >= requiredCount;
    }

    public void ClearAllSlots()
    {
        InitializeSlotsIfNeeded();

        for (int i = 0; i < slotList.Count; i++)
        {
            slotList[i].ClearSlot();
        }

        ClearSelection();
        RaiseInventoryChanged();
    }

    private void InitializeSlotsIfNeeded()
    {
        if (slotList == null || slotList.Count != FixedSlotCount)
        {
            InitializeSlots();
        }
    }

    private void ClearEmptySelection()
    {
        if (selectedSlotIndex == NoneSelectedIndex)
            return;

        if (IsValidIndex(selectedSlotIndex) && !slotList[selectedSlotIndex].IsEmpty)
            return;

        selectedSlotIndex = NoneSelectedIndex;
        RaiseSelectedSlotChanged();
        RaiseInventoryChanged();
    }

    private bool IsValidIndex(int index)
    {
        return slotList != null && index >= 0 && index < slotList.Count;
    }

    private bool IsValidSelectedIndex(int index)
    {
        return IsValidIndex(index);
    }

    private void RaiseInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void RaiseSelectedSlotChanged()
    {
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        InitializeSlotsIfNeeded();

        if (stream.IsWriting)
        {
            stream.SendNext(selectedSlotIndex);

            for (int i = 0; i < FixedSlotCount; i++)
            {
                string itemId = slotList[i].IsEmpty ? string.Empty : slotList[i].ItemDefinition.ItemId;
                stream.SendNext(itemId);
                stream.SendNext(slotList[i].ItemCount);
            }
        }
        else
        {
            bool inventoryChanged = false;
            bool selectionChanged = false;

            int nextSelectedIndex = (int)stream.ReceiveNext();
            nextSelectedIndex = Mathf.Clamp(nextSelectedIndex, NoneSelectedIndex, FixedSlotCount - 1);

            if (selectedSlotIndex != nextSelectedIndex)
            {
                selectedSlotIndex = nextSelectedIndex;
                selectionChanged = true;
            }

            for (int i = 0; i < FixedSlotCount; i++)
            {
                string itemId = (string)stream.ReceiveNext();
                int itemCount = (int)stream.ReceiveNext();

                ItemDefinition nextDefinition = string.IsNullOrEmpty(itemId)
                    ? null
                    : ItemDefinitionLookup.GetById(itemId);

                SlotData slot = slotList[i];

                bool changed =
                    slot.ItemDefinition != nextDefinition ||
                    slot.ItemCount != itemCount;

                if (changed)
                {
                    slot.SetSlot(nextDefinition, itemCount);
                    inventoryChanged = true;
                }
            }

            if (selectionChanged)
            {
                RaiseSelectedSlotChanged();
            }

            if (inventoryChanged || selectionChanged)
            {
                RaiseInventoryChanged();
            }
        }
    }
}
