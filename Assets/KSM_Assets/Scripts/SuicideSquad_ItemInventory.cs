using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀵슬롯형 인벤토리의 실제 데이터를 관리하는 스크립트.
/// - 아이템 종류 저장
/// - 아이템 수량 저장
/// - 같은 아이템 스택
/// - 선택 슬롯 이동
/// - 선택 아이템 정보 반환
/// 을 담당한다.
/// </summary>
public class SuicideSquad_ItemInventory : MonoBehaviour
{
    /// <summary>
    /// 인벤토리 한 칸의 실제 데이터를 담는 내부 클래스.
    /// 별도 스크립트로 분리하지 않고 이 안에 포함시켜 스크립트 수를 줄였다.
    /// </summary>
    [Serializable]
    public class SlotData
    {
        [Header("슬롯 데이터")]

        /// <summary>
        /// 현재 슬롯에 들어 있는 아이템 데이터.
        /// null이면 빈 슬롯이다.
        /// </summary>
        [SerializeField] private SuicideSquad_ItemData itemData = null;

        /// <summary>
        /// 현재 슬롯에 들어 있는 아이템 개수.
        /// </summary>
        [SerializeField] private int itemCount = 0;

        /// <summary>
        /// 현재 슬롯에 들어 있는 아이템 데이터.
        /// </summary>
        public SuicideSquad_ItemData ItemData => itemData;

        /// <summary>
        /// 현재 슬롯 수량.
        /// </summary>
        public int ItemCount => itemCount;

        /// <summary>
        /// 슬롯이 비어 있는지 여부.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return itemData == null || itemCount <= 0;
            }
        }

        /// <summary>
        /// 이 슬롯이 targetItemData와 같은 아이템인지 검사한다.
        /// </summary>
        /// <param name="targetItemData">비교할 아이템 데이터.</param>
        /// <returns>같은 아이템이면 true.</returns>
        public bool IsSameItem(SuicideSquad_ItemData targetItemData)
        {
            return itemData == targetItemData && targetItemData != null;
        }

        /// <summary>
        /// targetItemData를 이 슬롯에 더 스택할 수 있는지 검사한다.
        /// </summary>
        /// <param name="targetItemData">추가하려는 아이템 데이터.</param>
        /// <returns>같은 아이템이며 최대 스택 미만이면 true.</returns>
        public bool CanStack(SuicideSquad_ItemData targetItemData)
        {
            if (targetItemData == null)
            {
                return false;
            }

            if (IsEmpty)
            {
                return false;
            }

            if (itemData != targetItemData)
            {
                return false;
            }

            return itemCount < itemData.MaxStackCount;
        }

        /// <summary>
        /// 현재 슬롯의 남은 스택 가능 수량을 반환한다.
        /// </summary>
        /// <returns>더 담을 수 있는 개수.</returns>
        public int GetRemainingSpace()
        {
            if (IsEmpty)
            {
                return 0;
            }

            return Mathf.Max(0, itemData.MaxStackCount - itemCount);
        }

        /// <summary>
        /// 슬롯을 새 아이템과 수량으로 설정한다.
        /// </summary>
        /// <param name="newItemData">넣을 아이템 데이터.</param>
        /// <param name="newItemCount">넣을 수량.</param>
        public void SetSlot(SuicideSquad_ItemData newItemData, int newItemCount)
        {
            itemData = newItemData;
            itemCount = Mathf.Max(0, newItemCount);

            if (itemData == null || itemCount <= 0)
            {
                ClearSlot();
            }
        }

        /// <summary>
        /// 슬롯 수량을 증가시킨다.
        /// 최대 스택을 넘지 않도록 처리한다.
        /// </summary>
        /// <param name="addCount">추가할 수량.</param>
        /// <returns>실제로 추가된 수량.</returns>
        public int AddCount(int addCount)
        {
            if (IsEmpty)
            {
                return 0;
            }

            if (addCount <= 0)
            {
                return 0;
            }

            int appliedCount = Mathf.Min(addCount, GetRemainingSpace());
            itemCount += appliedCount;
            return appliedCount;
        }

        /// <summary>
        /// 슬롯 수량을 감소시킨다.
        /// 수량이 0 이하가 되면 슬롯을 비운다.
        /// </summary>
        /// <param name="removeCount">제거할 수량.</param>
        /// <returns>실제로 제거된 수량.</returns>
        public int RemoveCount(int removeCount)
        {
            if (IsEmpty)
            {
                return 0;
            }

            if (removeCount <= 0)
            {
                return 0;
            }

            int appliedCount = Mathf.Min(removeCount, itemCount);
            itemCount -= appliedCount;

            if (itemCount <= 0)
            {
                ClearSlot();
            }

            return appliedCount;
        }

        /// <summary>
        /// 슬롯을 완전히 비운다.
        /// </summary>
        public void ClearSlot()
        {
            itemData = null;
            itemCount = 0;
        }
    }

    [Header("슬롯 설정")]

    /// <summary>
    /// 퀵슬롯 총 개수.
    /// </summary>
    [SerializeField, Min(1)] private int slotCount = 8;

    /// <summary>
    /// 마지막 슬롯에서 다음으로 이동할 때 첫 슬롯으로 순환할지 여부.
    /// </summary>
    [SerializeField] private bool loopSelection = true;

    [Header("런타임 상태")]

    /// <summary>
    /// 현재 선택된 슬롯 번호.
    /// </summary>
    [SerializeField, Min(0)] private int selectedSlotIndex = 0;

    /// <summary>
    /// 실제 슬롯 데이터 목록.
    /// </summary>
    [SerializeField] private List<SlotData> slotList = new List<SlotData>();

    /// <summary>
    /// 인벤토리 내용이 바뀌었을 때 호출되는 이벤트.
    /// UI 갱신에 사용한다.
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// 선택 슬롯이 바뀌었을 때 호출되는 이벤트.
    /// 선택 강조 갱신에 사용한다.
    /// </summary>
    public event Action<int> OnSelectedSlotChanged;

    /// <summary>
    /// 현재 선택된 슬롯 인덱스.
    /// </summary>
    public int SelectedSlotIndex => selectedSlotIndex;

    /// <summary>
    /// 현재 슬롯 개수.
    /// </summary>
    public int SlotCount => slotList.Count;

    /// <summary>
    /// 시작 시 슬롯 리스트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        InitializeSlots();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터 값이 바뀔 때 기본값을 보정한다.
    /// </summary>
    private void OnValidate()
    {
        slotCount = Mathf.Max(1, slotCount);
        selectedSlotIndex = Mathf.Max(0, selectedSlotIndex);
    }
#endif

    /// <summary>
    /// 슬롯 리스트를 slotCount 개수에 맞게 생성/보정한다.
    /// </summary>
    public void InitializeSlots()
    {
        if (slotList == null)
        {
            slotList = new List<SlotData>();
        }

        if (slotList.Count < slotCount)
        {
            while (slotList.Count < slotCount)
            {
                slotList.Add(new SlotData());
            }
        }
        else if (slotList.Count > slotCount)
        {
            slotList.RemoveRange(slotCount, slotList.Count - slotCount);
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slotList.Count - 1);

        RaiseInventoryChanged();
        RaiseSelectedSlotChanged();
    }

    /// <summary>
    /// 아이템을 인벤토리에 추가한다.
    /// 먼저 기존 슬롯에 스택하고, 남은 수량은 빈 슬롯에 넣는다.
    /// 슬롯이 부족하면 들어갈 수 있는 만큼만 추가한다.
    /// </summary>
    /// <param name="itemData">추가할 아이템 데이터.</param>
    /// <param name="addCount">추가할 수량.</param>
    /// <returns>실제로 추가된 수량.</returns>
    public int AddItem(SuicideSquad_ItemData itemData, int addCount)
    {
        // TODO (협업 - 아이템 획득 연결):
        // 드랍, 상자, 채집, 보상 지급 코드에서
        // 아이템을 얻는 순간 이 함수를 호출하면 된다.

        if (itemData == null)
        {
            Debug.LogWarning("[SuicideSquad_ItemInventory] AddItem 실패: itemData가 비어 있습니다.");
            return 0;
        }

        if (addCount <= 0)
        {
            Debug.LogWarning("[SuicideSquad_ItemInventory] AddItem 실패: addCount는 1 이상이어야 합니다.");
            return 0;
        }

        if (slotList == null || slotList.Count == 0)
        {
            InitializeSlots();
        }

        int remainingCount = addCount;
        int totalAddedCount = 0;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].CanStack(itemData))
            {
                int addedCount = slotList[i].AddCount(remainingCount);
                remainingCount -= addedCount;
                totalAddedCount += addedCount;

                if (remainingCount <= 0)
                {
                    RaiseInventoryChanged();
                    return totalAddedCount;
                }
            }
        }

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].IsEmpty)
            {
                int placeCount = Mathf.Min(itemData.MaxStackCount, remainingCount);
                slotList[i].SetSlot(itemData, placeCount);

                remainingCount -= placeCount;
                totalAddedCount += placeCount;

                if (remainingCount <= 0)
                {
                    RaiseInventoryChanged();
                    return totalAddedCount;
                }
            }
        }

        if (totalAddedCount > 0)
        {
            RaiseInventoryChanged();
        }

        return totalAddedCount;
    }

    /// <summary>
    /// 현재 선택된 슬롯의 아이템을 count만큼 소비한다.
    /// </summary>
    /// <param name="count">소비할 수량.</param>
    /// <returns>성공적으로 소비했으면 true.</returns>
    public bool TryConsumeSelectedItem(int count = 1)
    {
        if (count <= 0)
        {
            return false;
        }

        if (IsValidIndex(selectedSlotIndex) == false)
        {
            return false;
        }

        SlotData selectedSlot = slotList[selectedSlotIndex];

        if (selectedSlot.IsEmpty)
        {
            return false;
        }

        if (selectedSlot.ItemCount < count)
        {
            return false;
        }

        selectedSlot.RemoveCount(count);
        RaiseInventoryChanged();
        return true;
    }

    /// <summary>
    /// 선택 슬롯을 direction 방향으로 이동한다.
    /// -1이면 이전, +1이면 다음이다.
    /// </summary>
    /// <param name="direction">이동 방향.</param>
    public void MoveSelection(int direction)
    {
        if (slotList == null || slotList.Count == 0)
        {
            return;
        }

        int nextIndex = selectedSlotIndex + direction;

        if (loopSelection)
        {
            selectedSlotIndex = WrapIndex(nextIndex, slotList.Count);
        }
        else
        {
            selectedSlotIndex = Mathf.Clamp(nextIndex, 0, slotList.Count - 1);
        }

        RaiseSelectedSlotChanged();
        RaiseInventoryChanged();
    }

    /// <summary>
    /// 특정 슬롯을 직접 선택한다.
    /// </summary>
    /// <param name="slotIndex">선택할 슬롯 인덱스.</param>
    public void SelectSlot(int slotIndex)
    {
        if (slotList == null || slotList.Count == 0)
        {
            return;
        }

        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, slotList.Count - 1);
        RaiseSelectedSlotChanged();
        RaiseInventoryChanged();
    }

    /// <summary>
    /// 현재 선택된 슬롯의 아이템 정보와 수량을 반환한다.
    /// </summary>
    /// <param name="itemData">선택된 아이템 데이터 반환값.</param>
    /// <param name="itemCount">선택된 수량 반환값.</param>
    /// <returns>유효한 아이템이 있으면 true.</returns>
    public bool TryGetSelectedItem(out SuicideSquad_ItemData itemData, out int itemCount)
    {
        itemData = null;
        itemCount = 0;

        if (IsValidIndex(selectedSlotIndex) == false)
        {
            return false;
        }

        SlotData selectedSlot = slotList[selectedSlotIndex];

        if (selectedSlot.IsEmpty)
        {
            return false;
        }

        itemData = selectedSlot.ItemData;
        itemCount = selectedSlot.ItemCount;
        return true;
    }

    /// <summary>
    /// 지정한 슬롯의 아이템 정보와 수량을 반환한다.
    /// UI 슬롯 그릴 때 사용한다.
    /// </summary>
    /// <param name="slotIndex">조회할 슬롯 번호.</param>
    /// <param name="itemData">아이템 데이터 반환값.</param>
    /// <param name="itemCount">수량 반환값.</param>
    /// <returns>유효한 아이템이 있으면 true.</returns>
    public bool TryGetSlotInfo(int slotIndex, out SuicideSquad_ItemData itemData, out int itemCount)
    {
        itemData = null;
        itemCount = 0;

        if (IsValidIndex(slotIndex) == false)
        {
            return false;
        }

        SlotData slot = slotList[slotIndex];

        if (slot.IsEmpty)
        {
            return false;
        }

        itemData = slot.ItemData;
        itemCount = slot.ItemCount;
        return true;
    }

    /// <summary>
    /// 특정 아이템의 총 보유 수량을 반환한다.
    /// </summary>
    /// <param name="itemData">확인할 아이템 데이터.</param>
    /// <returns>전체 슬롯을 합친 총 개수.</returns>
    public int GetTotalItemCount(SuicideSquad_ItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        int totalCount = 0;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (slotList[i].IsSameItem(itemData))
            {
                totalCount += slotList[i].ItemCount;
            }
        }

        return totalCount;
    }

    /// <summary>
    /// 특정 아이템을 requiredCount 이상 보유 중인지 검사한다.
    /// </summary>
    /// <param name="itemData">확인할 아이템 데이터.</param>
    /// <param name="requiredCount">필요 수량.</param>
    /// <returns>충분히 보유 중이면 true.</returns>
    public bool HasItem(SuicideSquad_ItemData itemData, int requiredCount = 1)
    {
        return GetTotalItemCount(itemData) >= requiredCount;
    }

    /// <summary>
    /// 모든 슬롯을 비운다.
    /// </summary>
    public void ClearAllSlots()
    {
        for (int i = 0; i < slotList.Count; i++)
        {
            slotList[i].ClearSlot();
        }

        RaiseInventoryChanged();
    }

    /// <summary>
    /// 슬롯 인덱스가 유효한지 검사한다.
    /// </summary>
    /// <param name="index">검사할 인덱스.</param>
    /// <returns>유효하면 true.</returns>
    private bool IsValidIndex(int index)
    {
        return slotList != null && index >= 0 && index < slotList.Count;
    }

    /// <summary>
    /// 순환 선택이 켜져 있을 때 인덱스를 0~count-1로 보정한다.
    /// </summary>
    /// <param name="index">원본 인덱스.</param>
    /// <param name="count">전체 개수.</param>
    /// <returns>보정된 인덱스.</returns>
    private int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        index %= count;

        if (index < 0)
        {
            index += count;
        }

        return index;
    }

    /// <summary>
    /// 인벤토리 변경 이벤트를 호출한다.
    /// </summary>
    private void RaiseInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 선택 슬롯 변경 이벤트를 호출한다.
    /// </summary>
    private void RaiseSelectedSlotChanged()
    {
        OnSelectedSlotChanged?.Invoke(selectedSlotIndex);
    }
}