using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 몬헌식 3칸 퀵슬롯 UI를 관리하는 스크립트.
/// - 왼쪽: 이전 아이템
/// - 중앙: 현재 선택 아이템
/// - 오른쪽: 다음 아이템
/// - J / K 키 이미지 강조
/// - 왼/중/오 모두 수량 표시
/// 
/// 기존 배열 기반 QuickSlotUI 대신,
/// 현재 프로젝트의 left / center / right / j / k 구조에 맞춰 만든 전용 버전이다.
/// </summary>
public class SuicideSquad_QuickSlotUI : MonoBehaviour
{
    [Header("인벤토리 참조")]

    /// <summary>
    /// 표시할 대상 인벤토리.
    /// 플레이어에 붙어 있는 SuicideSquad_ItemInventory 를 연결한다.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemInventory itemInventory = null;

    [Header("Image Root 참조")]

    /// <summary>
    /// 이전 아이템 아이콘을 표시할 이미지.
    /// </summary>
    [SerializeField] private Image leftImage = null;

    /// <summary>
    /// 현재 선택 아이템 아이콘을 표시할 이미지.
    /// </summary>
    [SerializeField] private Image centerImage = null;

    /// <summary>
    /// 다음 아이템 아이콘을 표시할 이미지.
    /// </summary>
    [SerializeField] private Image rightImage = null;

    /// <summary>
    /// J 키 힌트 이미지.
    /// 이전 슬롯으로 이동 시 강조 표시된다.
    /// </summary>
    [SerializeField] private Image jImage = null;

    /// <summary>
    /// K 키 힌트 이미지.
    /// 다음 슬롯으로 이동 시 강조 표시된다.
    /// </summary>
    [SerializeField] private Image kImage = null;

    [Header("Text Root 참조")]

    /// <summary>
    /// 이전 아이템 수량 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text leftCountText = null;

    /// <summary>
    /// 현재 선택 아이템 수량 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text centerCountText = null;

    /// <summary>
    /// 다음 아이템 수량 텍스트.
    /// </summary>
    [SerializeField] private TMP_Text rightCountText = null;

    [Header("슬롯 표시 스타일")]

    /// <summary>
    /// 좌우 작은 슬롯 아이콘에 적용할 색상.
    /// 중앙보다 조금 흐리게 보이도록 알파를 낮춰두는 것을 권장한다.
    /// </summary>
    [SerializeField] private Color sideSlotColor = new Color(1f, 1f, 1f, 0.65f);

    /// <summary>
    /// 중앙 슬롯 아이콘에 적용할 색상.
    /// 보통 완전한 흰색으로 둔다.
    /// </summary>
    [SerializeField] private Color centerSlotColor = Color.white;

    /// <summary>
    /// 슬롯이 비어 있을 때 아이콘을 숨길지 여부.
    /// true면 icon.enabled = false 처리한다.
    /// </summary>
    [SerializeField] private bool hideIconWhenEmpty = true;

    [Header("수량 텍스트 표시 규칙")]

    /// <summary>
    /// 수량이 없을 때 텍스트를 숨길지 여부.
    /// </summary>
    [SerializeField] private bool hideCountWhenEmpty = true;

    /// <summary>
    /// 표시 가능한 최대 숫자.
    /// 이를 넘으면 overflowCountText 로 표시한다.
    /// </summary>
    [SerializeField, Min(1)] private int maxVisibleCount = 99;

    /// <summary>
    /// 최대 표시 숫자를 넘었을 때 대신 보여줄 문자열.
    /// </summary>
    [SerializeField] private string overflowCountText = "99+";

    [Header("미리보기 방식")]

    /// <summary>
    /// 이전/다음 아이템 미리보기 계산 시 순환형으로 표시할지 여부.
    /// 인벤토리 loopSelection 과 동일하게 두는 것을 권장한다.
    /// 현재 인벤토리 기본값이 true 이므로 기본적으로 true 권장.
    /// </summary>
    [SerializeField] private bool useWrappedPreview = true;

    [Header("J / K 강조 스타일")]

    /// <summary>
    /// 키 힌트가 평상시 가질 색상.
    /// </summary>
    [SerializeField] private Color idleKeyColor = new Color(1f, 1f, 1f, 0.55f);

    /// <summary>
    /// 키가 눌렸을 때 잠깐 강조할 색상.
    /// </summary>
    [SerializeField] private Color pressedKeyColor = Color.white;

    /// <summary>
    /// 키 강조가 유지되는 시간.
    /// </summary>
    [SerializeField, Min(0.01f)] private float keyFlashDuration = 0.12f;

    /// <summary>
    /// 직전 선택 슬롯 번호를 저장한다.
    /// J / K 중 어느 방향으로 이동했는지 판별하는 데 사용한다.
    /// </summary>
    private int lastSelectedIndex = -1;

    /// <summary>
    /// J 키 강조용 코루틴 참조.
    /// 중복 실행 방지에 사용한다.
    /// </summary>
    private Coroutine jFlashCoroutine = null;

    /// <summary>
    /// K 키 강조용 코루틴 참조.
    /// 중복 실행 방지에 사용한다.
    /// </summary>
    private Coroutine kFlashCoroutine = null;

    /// <summary>
    /// 활성화 시 인벤토리 이벤트를 구독하고 UI를 즉시 갱신한다.
    /// </summary>
    private void OnEnable()
    {
        if (itemInventory == null)
        {
            return;
        }

        itemInventory.OnInventoryChanged += HandleInventoryChanged;
        itemInventory.OnSelectedSlotChanged += HandleSelectedSlotChanged;

        lastSelectedIndex = itemInventory.SelectedSlotIndex;
        ApplyIdleKeyColors();
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

        itemInventory.OnInventoryChanged -= HandleInventoryChanged;
        itemInventory.OnSelectedSlotChanged -= HandleSelectedSlotChanged;
    }

    /// <summary>
    /// 인벤토리 내용이 바뀌었을 때 전체 UI를 다시 그린다.
    /// 아이템 획득, 수량 변화 등에 반응한다.
    /// </summary>
    private void HandleInventoryChanged()
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 선택 슬롯이 바뀌었을 때
    /// - J / K 강조를 재생하고
    /// - 좌/중/우 슬롯 내용을 다시 계산한다.
    /// </summary>
    /// <param name="selectedIndex">새로 선택된 슬롯 번호.</param>
    private void HandleSelectedSlotChanged(int selectedIndex)
    {
        PlayKeyFeedback(lastSelectedIndex, selectedIndex);
        RefreshAllUI();
        lastSelectedIndex = selectedIndex;
    }

    /// <summary>
    /// 외부에서 강제로 UI를 다시 그리고 싶을 때 호출하는 함수.
    /// </summary>
    public void RefreshAllUI()
    {
        if (itemInventory == null)
        {
            ClearAllUI();
            return;
        }

        int slotCount = itemInventory.SlotCount;

        if (slotCount <= 0)
        {
            ClearAllUI();
            return;
        }

        int centerIndex = Mathf.Clamp(itemInventory.SelectedSlotIndex, 0, slotCount - 1);
        int leftIndex = GetPreviewIndex(centerIndex, -1, slotCount);
        int rightIndex = GetPreviewIndex(centerIndex, 1, slotCount);

        RefreshSlot(leftImage, leftCountText, leftIndex, false);
        RefreshSlot(centerImage, centerCountText, centerIndex, true);
        RefreshSlot(rightImage, rightCountText, rightIndex, false);
    }

    /// <summary>
    /// 한 칸의 아이콘과 수량 텍스트를 갱신한다.
    /// </summary>
    /// <param name="targetImage">갱신할 아이콘 이미지.</param>
    /// <param name="targetCountText">갱신할 수량 텍스트.</param>
    /// <param name="slotIndex">참조할 인벤토리 슬롯 번호. -1이면 빈 슬롯 처리.</param>
    /// <param name="isCenterSlot">중앙 슬롯 여부.</param>
    private void RefreshSlot(Image targetImage, TMP_Text targetCountText, int slotIndex, bool isCenterSlot)
    {
        if (slotIndex < 0)
        {
            ApplyEmptySlot(targetImage, targetCountText, isCenterSlot);
            return;
        }

        bool hasItem = itemInventory.TryGetSlotInfo(slotIndex, out SuicideSquad_ItemData itemData, out int itemCount);

        if (hasItem == false || itemData == null || itemCount <= 0)
        {
            ApplyEmptySlot(targetImage, targetCountText, isCenterSlot);
            return;
        }

        if (targetImage != null)
        {
            targetImage.enabled = true;
            targetImage.sprite = itemData.IconSprite;
            targetImage.color = isCenterSlot ? centerSlotColor : sideSlotColor;

            if (itemData.IconSprite == null && hideIconWhenEmpty)
            {
                targetImage.enabled = false;
            }
        }

        if (targetCountText != null)
        {
            targetCountText.text = FormatCount(itemCount);
            targetCountText.enabled = true;

            if (hideCountWhenEmpty && itemCount <= 0)
            {
                targetCountText.text = string.Empty;
            }
        }
    }

    /// <summary>
    /// 빈 슬롯 상태를 적용한다.
    /// </summary>
    /// <param name="targetImage">적용할 아이콘 이미지.</param>
    /// <param name="targetCountText">적용할 수량 텍스트.</param>
    /// <param name="isCenterSlot">중앙 슬롯 여부.</param>
    private void ApplyEmptySlot(Image targetImage, TMP_Text targetCountText, bool isCenterSlot)
    {
        if (targetImage != null)
        {
            targetImage.sprite = null;
            targetImage.color = isCenterSlot ? centerSlotColor : sideSlotColor;

            if (hideIconWhenEmpty)
            {
                targetImage.enabled = false;
            }
            else
            {
                targetImage.enabled = true;
            }
        }

        if (targetCountText != null)
        {
            targetCountText.text = hideCountWhenEmpty ? string.Empty : "0";
        }
    }

    /// <summary>
    /// 전체 UI를 빈 상태로 초기화한다.
    /// </summary>
    private void ClearAllUI()
    {
        ApplyEmptySlot(leftImage, leftCountText, false);
        ApplyEmptySlot(centerImage, centerCountText, true);
        ApplyEmptySlot(rightImage, rightCountText, false);
        ApplyIdleKeyColors();
    }

    /// <summary>
    /// 현재 선택 인덱스를 기준으로 이전/다음 미리보기 슬롯 번호를 계산한다.
    /// </summary>
    /// <param name="currentIndex">현재 선택 슬롯 번호.</param>
    /// <param name="direction">-1이면 이전, +1이면 다음.</param>
    /// <param name="slotCount">총 슬롯 수.</param>
    /// <returns>미리보기 슬롯 번호. 없으면 -1.</returns>
    private int GetPreviewIndex(int currentIndex, int direction, int slotCount)
    {
        if (slotCount <= 1)
        {
            return -1;
        }

        int targetIndex = currentIndex + direction;

        if (useWrappedPreview)
        {
            return WrapIndex(targetIndex, slotCount);
        }

        if (targetIndex < 0 || targetIndex >= slotCount)
        {
            return -1;
        }

        return targetIndex;
    }

    /// <summary>
    /// 인덱스를 0 ~ slotCount-1 범위로 순환 보정한다.
    /// </summary>
    /// <param name="index">보정할 인덱스.</param>
    /// <param name="slotCount">총 슬롯 수.</param>
    /// <returns>순환 보정된 인덱스.</returns>
    private int WrapIndex(int index, int slotCount)
    {
        if (slotCount <= 0)
        {
            return 0;
        }

        index %= slotCount;

        if (index < 0)
        {
            index += slotCount;
        }

        return index;
    }

    /// <summary>
    /// 수량 숫자를 화면용 문자열로 변환한다.
    /// </summary>
    /// <param name="count">표시할 수량.</param>
    /// <returns>화면에 출력할 문자열.</returns>
    private string FormatCount(int count)
    {
        if (count <= 0)
        {
            return hideCountWhenEmpty ? string.Empty : "0";
        }

        if (count > maxVisibleCount)
        {
            return overflowCountText;
        }

        return count.ToString();
    }

    /// <summary>
    /// J / K 키 힌트의 평상시 색상을 적용한다.
    /// </summary>
    private void ApplyIdleKeyColors()
    {
        if (jImage != null)
        {
            jImage.color = idleKeyColor;
        }

        if (kImage != null)
        {
            kImage.color = idleKeyColor;
        }
    }

    /// <summary>
    /// 직전 선택 인덱스와 현재 선택 인덱스를 비교해서
    /// 어느 방향으로 이동했는지 추정하고 J 또는 K 이미지를 강조한다.
    /// </summary>
    /// <param name="previousIndex">이전 선택 슬롯 번호.</param>
    /// <param name="currentIndex">현재 선택 슬롯 번호.</param>
    private void PlayKeyFeedback(int previousIndex, int currentIndex)
    {
        if (itemInventory == null)
        {
            return;
        }

        int slotCount = itemInventory.SlotCount;

        if (slotCount <= 0)
        {
            return;
        }

        if (previousIndex < 0)
        {
            return;
        }

        if (currentIndex == previousIndex)
        {
            return;
        }

        bool movedRight =
            currentIndex == previousIndex + 1 ||
            currentIndex == WrapIndex(previousIndex + 1, slotCount);

        bool movedLeft =
            currentIndex == previousIndex - 1 ||
            currentIndex == WrapIndex(previousIndex - 1, slotCount);

        if (movedLeft && jImage != null)
        {
            if (jFlashCoroutine != null)
            {
                StopCoroutine(jFlashCoroutine);
            }

            jFlashCoroutine = StartCoroutine(CoFlashKeyImage(jImage));
        }

        if (movedRight && kImage != null)
        {
            if (kFlashCoroutine != null)
            {
                StopCoroutine(kFlashCoroutine);
            }

            kFlashCoroutine = StartCoroutine(CoFlashKeyImage(kImage));
        }
    }

    /// <summary>
    /// 키 이미지 하나를 잠깐 강조한 뒤 원래 색으로 되돌린다.
    /// </summary>
    /// <param name="targetImage">강조할 키 이미지.</param>
    /// <returns>코루틴 열거자.</returns>
    private IEnumerator CoFlashKeyImage(Image targetImage)
    {
        if (targetImage == null)
        {
            yield break;
        }

        targetImage.color = pressedKeyColor;
        yield return new WaitForSeconds(keyFlashDuration);
        targetImage.color = idleKeyColor;
    }
}