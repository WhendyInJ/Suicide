using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 2슬롯 선택형 퀵슬롯 UI를 관리하는 스크립트.
///
/// 구조:
/// - 슬롯 1 : 인벤토리 0번 슬롯 표시
/// - 슬롯 2 : 인벤토리 1번 슬롯 표시
/// - 각 슬롯은 Frame + Icon 구성만 사용한다.
/// - 선택된 슬롯은 부드럽게 커지고, 선택되지 않은 슬롯은 작게 유지된다.
/// - 1 / 2 키로 슬롯 선택 변경
/// - Ctrl 입력 시 현재 선택 아이템의 "사용 요청 이벤트"를 호출한다.
///
/// 주의:
/// - 이 스크립트는 UI 표시와 기본 입력 처리만 담당한다.
/// - 실제 아이템 사용 효과 적용은 외부 로직에서 처리하는 것을 권장한다.
/// - Ctrl 입력 시 onUseSelectedItemRequested 이벤트를 호출하므로,
///   플레이어 아이템 사용 함수가 준비되면 인스펙터에서 연결하면 된다.
/// - 수량 텍스트와 Ctrl 힌트 UI는 사용하지 않는 전제로 구성되어 있다.
/// </summary>
public class SuicideSquad_QuickSlotUI : MonoBehaviour
{
    [Header("인벤토리 참조")]

    /// <summary>
    /// 표시 대상 인벤토리.
    /// 플레이어에 붙어 있는 SuicideSquad_ItemInventory 를 연결한다.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemInventory itemInventory = null;

    [Header("슬롯 1 UI")]

    /// <summary>
    /// 1번 슬롯 전체 루트.
    /// 선택 여부에 따라 이 RectTransform의 크기를 부드럽게 변경한다.
    /// </summary>
    [SerializeField] private RectTransform slot1Root = null;

    /// <summary>
    /// 1번 슬롯의 외곽 프레임 이미지.
    /// </summary>
    [SerializeField] private Image slot1FrameImage = null;

    /// <summary>
    /// 1번 슬롯의 아이템 아이콘 이미지.
    /// </summary>
    [SerializeField] private Image slot1IconImage = null;

    [Header("슬롯 2 UI")]

    /// <summary>
    /// 2번 슬롯 전체 루트.
    /// 선택 여부에 따라 이 RectTransform의 크기를 부드럽게 변경한다.
    /// </summary>
    [SerializeField] private RectTransform slot2Root = null;

    /// <summary>
    /// 2번 슬롯의 외곽 프레임 이미지.
    /// </summary>
    [SerializeField] private Image slot2FrameImage = null;

    /// <summary>
    /// 2번 슬롯의 아이템 아이콘 이미지.
    /// </summary>
    [SerializeField] private Image slot2IconImage = null;

    [Header("입력 처리")]

    /// <summary>
    /// 이 UI가 직접 1 / 2 / Ctrl 입력을 받을지 여부.
    /// 다른 입력 스크립트가 이미 있으면 false로 끌 수 있다.
    /// </summary>
    [SerializeField] private bool handleInputInThisUI = true;

    /// <summary>
    /// Ctrl 입력 시 호출되는 이벤트.
    /// 실제 아이템 사용 로직은 여기 연결해서 처리하면 된다.
    /// </summary>
    [SerializeField] private UnityEvent onUseSelectedItemRequested = new UnityEvent();

    [Header("선택 확대 연출")]

    /// <summary>
    /// 선택된 슬롯의 목표 크기 배율.
    /// </summary>
    [SerializeField] private Vector3 selectedScale = new Vector3(1.15f, 1.15f, 1f);

    /// <summary>
    /// 선택되지 않은 슬롯의 목표 크기 배율.
    /// </summary>
    [SerializeField] private Vector3 unselectedScale = new Vector3(0.88f, 0.88f, 1f);

    /// <summary>
    /// 슬롯 크기가 목표 크기로 따라가는 속도.
    /// 값이 클수록 더 빨리 커지고 줄어든다.
    /// </summary>
    [SerializeField, Min(0.01f)] private float scaleLerpSpeed = 12f;

    [Header("프레임 / 아이콘 스타일")]

    /// <summary>
    /// 선택된 슬롯 프레임 색상.
    /// </summary>
    [SerializeField] private Color selectedFrameColor = Color.white;

    /// <summary>
    /// 선택되지 않은 슬롯 프레임 색상.
    /// </summary>
    [SerializeField] private Color unselectedFrameColor = new Color(1f, 1f, 1f, 0.55f);

    /// <summary>
    /// 아이콘 기본 색상.
    /// </summary>
    [SerializeField] private Color iconColor = Color.white;

    /// <summary>
    /// 슬롯에 아이템이 없으면 아이콘 이미지를 숨길지 여부.
    /// </summary>
    [SerializeField] private bool hideIconWhenEmpty = true;

    /// <summary>
    /// 활성화 시 이벤트를 구독하고 UI를 즉시 갱신한다.
    /// </summary>
    private void OnEnable()
    {
        SubscribeInventoryEvents();
        RefreshAllUI();
        SnapSlotScalesImmediately();
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeInventoryEvents();
    }

    /// <summary>
    /// 매 프레임 입력 처리와 슬롯 확대 애니메이션을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (handleInputInThisUI)
        {
            HandleSelectionInput();
            HandleUseInput();
        }

        AnimateSlotScales();
    }

    /// <summary>
    /// 인벤토리 이벤트를 구독한다.
    /// </summary>
    private void SubscribeInventoryEvents()
    {
        if (itemInventory == null)
        {
            return;
        }

        itemInventory.OnInventoryChanged += HandleInventoryChanged;
        itemInventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;
    }

    /// <summary>
    /// 인벤토리 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeInventoryEvents()
    {
        if (itemInventory == null)
        {
            return;
        }

        itemInventory.OnInventoryChanged -= HandleInventoryChanged;
        itemInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
    }

    /// <summary>
    /// 인벤토리 내용이 바뀌면 UI를 갱신한다.
    /// </summary>
    private void HandleInventoryChanged()
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 선택 슬롯이 바뀌면 UI를 갱신한다.
    /// </summary>
    /// <param name="selectedIndex">새로 선택된 슬롯 인덱스.</param>
    private void HandleSelectedSlotChanged(int selectedIndex)
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 1 / 2 키 입력을 처리해서 슬롯 선택을 바꾼다.
    /// </summary>
    private void HandleSelectionInput()
    {
        if (itemInventory == null)
        {
            return;
        }

        int displaySlotCount = GetDisplaySlotCount();

        if (displaySlotCount <= 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            itemInventory.SelectSlot(0);
        }

        if (displaySlotCount >= 2 && (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)))
        {
            itemInventory.SelectSlot(1);
        }
    }

    /// <summary>
    /// Ctrl 입력을 처리한다.
    /// 실제 아이템 사용은 이벤트에 연결된 외부 로직이 담당한다.
    /// </summary>
    private void HandleUseInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
        {
            // TODO:
            // 실제 아이템 효과 적용 + 소비 로직은
            // 외부 플레이어/아이템 사용 스크립트에서 처리한 뒤,
            // 아래 UnityEvent 에 연결해서 사용한다.
            onUseSelectedItemRequested?.Invoke();
        }
    }

    /// <summary>
    /// 외부에서 강제로 전체 UI를 갱신하고 싶을 때 호출하는 함수.
    /// </summary>
    public void RefreshAllUI()
    {
        if (itemInventory == null)
        {
            ClearAllUI();
            return;
        }

        int displaySlotCount = GetDisplaySlotCount();

        if (displaySlotCount <= 0)
        {
            ClearAllUI();
            return;
        }

        EnsureSelectedSlotIndexIsValid(displaySlotCount);

        int selectedIndex = Mathf.Clamp(itemInventory.SelectedSlotIndex, 0, displaySlotCount - 1);

        RefreshSlot(slot1FrameImage, slot1IconImage, 0, selectedIndex == 0);
        RefreshSlot(slot2FrameImage, slot2IconImage, 1, selectedIndex == 1);
    }

    /// <summary>
    /// 표시 대상 슬롯 수를 반환한다.
    /// 이 UI는 최대 2칸만 표시한다.
    /// </summary>
    /// <returns>0~2 범위의 슬롯 수.</returns>
    private int GetDisplaySlotCount()
    {
        if (itemInventory == null)
        {
            return 0;
        }

        return Mathf.Min(2, itemInventory.SlotCount);
    }

    /// <summary>
    /// 선택 인덱스가 현재 표시 범위를 벗어나면 0으로 보정한다.
    /// </summary>
    /// <param name="displaySlotCount">현재 표시 슬롯 수.</param>
    private void EnsureSelectedSlotIndexIsValid(int displaySlotCount)
    {
        if (itemInventory == null || displaySlotCount <= 0)
        {
            return;
        }

        if (itemInventory.SelectedSlotIndex < 0 || itemInventory.SelectedSlotIndex >= displaySlotCount)
        {
            itemInventory.SelectSlot(0);
        }
    }

    /// <summary>
    /// 지정한 슬롯 하나의 UI를 갱신한다.
    /// </summary>
    /// <param name="frameImage">슬롯 프레임 이미지.</param>
    /// <param name="iconImage">슬롯 아이콘 이미지.</param>
    /// <param name="slotIndex">조회할 인벤토리 슬롯 인덱스.</param>
    /// <param name="isSelected">현재 선택 슬롯 여부.</param>
    private void RefreshSlot(
        Image frameImage,
        Image iconImage,
        int slotIndex,
        bool isSelected)
    {
        bool hasItem = TryGetDisplaySlotInfo(slotIndex, out SuicideSquad_ItemData itemData);

        if (hasItem == false)
        {
            ApplyEmptySlot(frameImage, iconImage, isSelected);
            return;
        }

        ApplyFilledSlot(frameImage, iconImage, itemData, isSelected);
    }

    /// <summary>
    /// 표시용 슬롯 정보를 안전하게 가져온다.
    /// 슬롯이 비어 있거나 유효하지 않으면 false를 반환한다.
    /// </summary>
    /// <param name="slotIndex">조회할 슬롯 번호.</param>
    /// <param name="itemData">반환할 아이템 데이터.</param>
    /// <returns>유효한 아이템이 있으면 true.</returns>
    private bool TryGetDisplaySlotInfo(int slotIndex, out SuicideSquad_ItemData itemData)
    {
        itemData = null;

        if (itemInventory == null)
        {
            return false;
        }

        int displaySlotCount = GetDisplaySlotCount();

        if (slotIndex < 0 || slotIndex >= displaySlotCount)
        {
            return false;
        }

        bool success = itemInventory.TryGetSlotInfo(slotIndex, out itemData, out int itemCount);

        if (success == false)
        {
            return false;
        }

        if (itemData == null || itemCount <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 아이템이 들어 있는 슬롯의 UI 상태를 적용한다.
    /// </summary>
    /// <param name="frameImage">슬롯 프레임 이미지.</param>
    /// <param name="iconImage">슬롯 아이콘 이미지.</param>
    /// <param name="itemData">표시할 아이템 데이터.</param>
    /// <param name="isSelected">선택된 슬롯인지 여부.</param>
    private void ApplyFilledSlot(
        Image frameImage,
        Image iconImage,
        SuicideSquad_ItemData itemData,
        bool isSelected)
    {
        if (frameImage != null)
        {
            frameImage.enabled = true;
            frameImage.color = isSelected ? selectedFrameColor : unselectedFrameColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = itemData.IconSprite;
            iconImage.color = iconColor;
            iconImage.enabled = itemData.IconSprite != null || hideIconWhenEmpty == false;
        }
    }

    /// <summary>
    /// 빈 슬롯의 UI 상태를 적용한다.
    /// 프레임은 유지하고 아이콘만 비우는 방식이다.
    /// </summary>
    /// <param name="frameImage">슬롯 프레임 이미지.</param>
    /// <param name="iconImage">슬롯 아이콘 이미지.</param>
    /// <param name="isSelected">선택된 슬롯인지 여부.</param>
    private void ApplyEmptySlot(
        Image frameImage,
        Image iconImage,
        bool isSelected)
    {
        if (frameImage != null)
        {
            frameImage.enabled = true;
            frameImage.color = isSelected ? selectedFrameColor : unselectedFrameColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.color = iconColor;
            iconImage.enabled = hideIconWhenEmpty == false;
        }
    }

    /// <summary>
    /// 전체 UI를 빈 상태로 초기화한다.
    /// </summary>
    private void ClearAllUI()
    {
        ApplyEmptySlot(slot1FrameImage, slot1IconImage, true);
        ApplyEmptySlot(slot2FrameImage, slot2IconImage, false);
    }

    /// <summary>
    /// 선택 여부에 맞는 목표 스케일을 반환한다.
    /// </summary>
    /// <param name="isSelected">선택된 슬롯인지 여부.</param>
    /// <returns>목표 스케일.</returns>
    private Vector3 GetTargetScale(bool isSelected)
    {
        return isSelected ? selectedScale : unselectedScale;
    }

    /// <summary>
    /// 슬롯의 현재 선택 상태를 기준으로 스케일을 부드럽게 보간한다.
    /// </summary>
    private void AnimateSlotScales()
    {
        int displaySlotCount = GetDisplaySlotCount();
        int selectedIndex = 0;

        if (itemInventory != null && displaySlotCount > 0)
        {
            selectedIndex = Mathf.Clamp(itemInventory.SelectedSlotIndex, 0, displaySlotCount - 1);
        }

        AnimateSingleSlotScale(slot1Root, selectedIndex == 0);
        AnimateSingleSlotScale(slot2Root, selectedIndex == 1);
    }

    /// <summary>
    /// 슬롯 하나의 루트 스케일을 목표값으로 부드럽게 보간한다.
    /// </summary>
    /// <param name="slotRoot">대상 슬롯 루트.</param>
    /// <param name="isSelected">선택 여부.</param>
    private void AnimateSingleSlotScale(RectTransform slotRoot, bool isSelected)
    {
        if (slotRoot == null)
        {
            return;
        }

        Vector3 targetScale = GetTargetScale(isSelected);
        slotRoot.localScale = Vector3.Lerp(slotRoot.localScale, targetScale, Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    /// <summary>
    /// 활성화 직후 슬롯 스케일을 즉시 목표값으로 맞춘다.
    /// 첫 프레임에서 튀는 현상을 줄인다.
    /// </summary>
    private void SnapSlotScalesImmediately()
    {
        int displaySlotCount = GetDisplaySlotCount();
        int selectedIndex = 0;

        if (itemInventory != null && displaySlotCount > 0)
        {
            selectedIndex = Mathf.Clamp(itemInventory.SelectedSlotIndex, 0, displaySlotCount - 1);
        }

        if (slot1Root != null)
        {
            slot1Root.localScale = GetTargetScale(selectedIndex == 0);
        }

        if (slot2Root != null)
        {
            slot2Root.localScale = GetTargetScale(selectedIndex == 1);
        }
    }
}