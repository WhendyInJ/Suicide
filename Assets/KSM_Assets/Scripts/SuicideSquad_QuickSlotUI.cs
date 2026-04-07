using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀵슬롯 UI 전체를 관리하는 스크립트.
/// 슬롯별 아이콘/수량/이름/선택 강조를 배열로 직접 처리한다.
/// 별도 슬롯 스크립트를 만들지 않아도 되도록 구성했다.
/// </summary>
public class SuicideSquad_QuickSlotUI : MonoBehaviour
{
    [Header("인벤토리 참조")]

    /// <summary>
    /// 표시할 대상 인벤토리.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemInventory itemInventory = null;

    [Header("슬롯 UI 배열")]

    /// <summary>
    /// 각 슬롯의 아이콘 이미지 배열.
    /// 슬롯 순서와 동일하게 넣어야 한다.
    /// </summary>
    [SerializeField] private Image[] slotIconImages = new Image[0];

    /// <summary>
    /// 각 슬롯의 수량 텍스트 배열.
    /// </summary>
    [SerializeField] private TMP_Text[] slotCountTexts = new TMP_Text[0];

    /// <summary>
    /// 각 슬롯의 이름 텍스트 배열.
    /// 필요 없으면 비워둬도 된다.
    /// </summary>
    [SerializeField] private TMP_Text[] slotNameTexts = new TMP_Text[0];

    /// <summary>
    /// 각 슬롯의 선택 강조 오브젝트 배열.
    /// </summary>
    [SerializeField] private GameObject[] slotSelectedHighlights = new GameObject[0];

    /// <summary>
    /// 각 슬롯의 빈 슬롯 상태 오브젝트 배열.
    /// 필요 없으면 비워둬도 된다.
    /// </summary>
    [SerializeField] private GameObject[] slotEmptyStateObjects = new GameObject[0];

    [Header("추가 표시 UI")]

    /// <summary>
    /// 현재 선택된 아이템 이름/수량을 보여주는 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text selectedItemInfoText = null;

    /// <summary>
    /// 현재 선택된 슬롯 번호를 보여주는 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text selectedSlotIndexText = null;

    /// <summary>
    /// 활성화 시 인벤토리 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        if (itemInventory == null)
        {
            return;
        }

        itemInventory.OnInventoryChanged += RefreshAllUI;
        itemInventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;

        RefreshAllUI();
    }

    /// <summary>
    /// 비활성화 시 인벤토리 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (itemInventory == null)
        {
            return;
        }

        itemInventory.OnInventoryChanged -= RefreshAllUI;
        itemInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
    }

    /// <summary>
    /// 선택 슬롯이 바뀌었을 때 UI를 다시 갱신한다.
    /// </summary>
    /// <param name="selectedIndex">현재 선택 슬롯 번호.</param>
    private void HandleSelectedSlotChanged(int selectedIndex)
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 전체 슬롯 UI와 선택 정보 텍스트를 갱신한다.
    /// </summary>
    public void RefreshAllUI()
    {
        if (itemInventory == null)
        {
            return;
        }

        int visualSlotCount = GetVisualSlotCount();

        for (int i = 0; i < visualSlotCount; i++)
        {
            bool hasItem = itemInventory.TryGetSlotInfo(i, out SuicideSquad_ItemData itemData, out int itemCount);
            bool isSelected = i == itemInventory.SelectedSlotIndex;

            RefreshSingleSlot(i, hasItem ? itemData : null, hasItem ? itemCount : 0, isSelected);
        }

        RefreshSelectedInfoText();
    }

    /// <summary>
    /// 한 슬롯의 UI를 갱신한다.
    /// </summary>
    /// <param name="slotIndex">갱신할 슬롯 번호.</param>
    /// <param name="itemData">표시할 아이템 데이터.</param>
    /// <param name="itemCount">표시할 수량.</param>
    /// <param name="isSelected">현재 선택된 슬롯인지 여부.</param>
    private void RefreshSingleSlot(int slotIndex, SuicideSquad_ItemData itemData, int itemCount, bool isSelected)
    {
        bool isEmpty = itemData == null || itemCount <= 0;

        Image iconImage = GetArrayElement(slotIconImages, slotIndex);
        TMP_Text countText = GetArrayElement(slotCountTexts, slotIndex);
        TMP_Text nameText = GetArrayElement(slotNameTexts, slotIndex);
        GameObject selectedObject = GetArrayElement(slotSelectedHighlights, slotIndex);
        GameObject emptyObject = GetArrayElement(slotEmptyStateObjects, slotIndex);

        if (iconImage != null)
        {
            iconImage.enabled = isEmpty == false && itemData.IconSprite != null;
            iconImage.sprite = isEmpty ? null : itemData.IconSprite;
        }

        if (countText != null)
        {
            countText.text = isEmpty ? string.Empty : itemCount.ToString();
        }

        if (nameText != null)
        {
            nameText.text = isEmpty ? string.Empty : itemData.DisplayName;
        }

        if (selectedObject != null)
        {
            selectedObject.SetActive(isSelected);
        }

        if (emptyObject != null)
        {
            emptyObject.SetActive(isEmpty);
        }
    }

    /// <summary>
    /// 현재 선택된 아이템 정보 텍스트를 갱신한다.
    /// </summary>
    private void RefreshSelectedInfoText()
    {
        if (selectedSlotIndexText != null)
        {
            selectedSlotIndexText.text = (itemInventory.SelectedSlotIndex + 1).ToString();
        }

        if (selectedItemInfoText != null)
        {
            if (itemInventory.TryGetSelectedItem(out SuicideSquad_ItemData itemData, out int itemCount))
            {
                selectedItemInfoText.text = $"{itemData.DisplayName} x{itemCount}";
            }
            else
            {
                selectedItemInfoText.text = "빈 슬롯";
            }
        }
    }

    /// <summary>
    /// 현재 UI에 실제로 표시 가능한 슬롯 수를 계산한다.
    /// 아이콘/텍스트/하이라이트 배열 길이 중 가장 긴 값을 사용한다.
    /// </summary>
    /// <returns>표시 가능한 슬롯 수.</returns>
    private int GetVisualSlotCount()
    {
        int maxCount = 0;

        maxCount = Mathf.Max(maxCount, slotIconImages != null ? slotIconImages.Length : 0);
        maxCount = Mathf.Max(maxCount, slotCountTexts != null ? slotCountTexts.Length : 0);
        maxCount = Mathf.Max(maxCount, slotNameTexts != null ? slotNameTexts.Length : 0);
        maxCount = Mathf.Max(maxCount, slotSelectedHighlights != null ? slotSelectedHighlights.Length : 0);
        maxCount = Mathf.Max(maxCount, slotEmptyStateObjects != null ? slotEmptyStateObjects.Length : 0);

        return maxCount;
    }

    /// <summary>
    /// 배열에서 index 위치의 요소를 안전하게 가져온다.
    /// 길이를 넘거나 배열이 비어 있으면 null을 반환한다.
    /// </summary>
    /// <typeparam name="T">가져올 참조 타입.</typeparam>
    /// <param name="array">대상 배열.</param>
    /// <param name="index">가져올 인덱스.</param>
    /// <returns>유효하면 요소, 아니면 null.</returns>
    private T GetArrayElement<T>(T[] array, int index) where T : class
    {
        if (array == null)
        {
            return null;
        }

        if (index < 0 || index >= array.Length)
        {
            return null;
        }

        return array[index];
    }
}