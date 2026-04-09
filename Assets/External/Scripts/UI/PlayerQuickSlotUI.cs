using System;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 2칸 퀵슬롯 UI를 담당하는 스크립트.
///
/// 이 스크립트의 역할:
/// 1. 로컬 플레이어의 인벤토리 / 아이템 러너 / 입력 소스를 자동 탐색한다.
/// 2. 1번 슬롯, 2번 슬롯의 아이콘을 갱신한다.
/// 3. 선택 상태 / 조준 상태에 따라 슬롯 프레임과 크기를 갱신한다.
/// 4. 슬롯과 별도로 배치된 "1", "2" 고정 힌트 텍스트를 항상 표시한다.
/// 5. 고정 힌트 텍스트도 슬롯 선택 확대를 따라가도록 만든다.
/// 6. 좌우 순환 가능함을 알려주는 연결 이미지를 항상 표시한다.
/// 7. 투명 배경 아이콘이 잘 보이도록 2중 프레임(검정 바깥 프레임 + 흰색 안쪽 프레임) 구조를 지원한다.
///
/// 주의:
/// - 서버에서 아이템이 들어오는 코드나 네트워크 로직은 수정하지 않는다.
/// - 이 스크립트는 UI 표시 전용이다.
/// - 아이템 아이콘이 투명한 경우를 고려해서 프레임은 검정/흰색 이중 구조로 유지한다.
/// </summary>
public class PlayerQuickSlotUI : MonoBehaviour
{
    /// <summary>
    /// 슬롯 1개에 필요한 UI 참조를 묶는 내부 클래스.
    /// </summary>
    [Serializable]
    private sealed class SlotUI
    {
        [Header("슬롯 루트")]
        [SerializeField] private RectTransform root;

        [Header("바깥 프레임 이미지 (검정)")]
        [FormerlySerializedAs("frameImage")]
        [SerializeField] private Image outerFrameImage;

        [Header("안쪽 프레임 이미지 (흰색)")]
        [SerializeField] private Image innerFrameImage;

        [Header("슬롯 아이콘 이미지")]
        [SerializeField] private Image iconImage;

        /// <summary>
        /// 슬롯 전체를 대표하는 루트.
        /// 선택 / 비선택 확대 연출은 이 루트에 적용된다.
        /// 반드시 프레임과 아이콘을 함께 감싸는 부모여야 한다.
        /// </summary>
        public RectTransform Root => root;

        /// <summary>
        /// 바깥 프레임 이미지.
        /// 검정색 outline 역할을 담당한다.
        /// </summary>
        public Image OuterFrameImage => outerFrameImage;

        /// <summary>
        /// 안쪽 프레임 이미지.
        /// 흰색 outline 역할을 담당한다.
        /// </summary>
        public Image InnerFrameImage => innerFrameImage;

        /// <summary>
        /// 슬롯 아이콘 이미지.
        /// 실제 아이템 아이콘이 표시되는 곳이다.
        /// </summary>
        public Image IconImage => iconImage;
    }

    [Header("플레이어 참조")]
    /// <summary>
    /// 플레이어 아이템 인벤토리 참조.
    /// 서버/게임플레이 코드에서 들어온 아이템 데이터를 읽기만 한다.
    /// </summary>
    [SerializeField] private PlayerItemInventory inventory;

    /// <summary>
    /// 플레이어 아이템 사용/조준 상태 참조.
    /// 조준 중일 때 UI 강조에 사용한다.
    /// </summary>
    [SerializeField] private PlayerItemRunner itemRunner;

    /// <summary>
    /// 플레이어 입력 소스 참조.
    /// 로컬 권한 여부를 판단하는 데 사용한다.
    /// </summary>
    [SerializeField] private PlayerInputSource inputSource;

    /// <summary>
    /// 로컬 플레이어 참조를 자동으로 탐색할지 여부.
    /// </summary>
    [SerializeField] private bool autoResolveLocalPlayerReferences = true;

    /// <summary>
    /// 자동 탐색 재시도 간격.
    /// 플레이어 생성이 늦는 멀티 상황을 대비한다.
    /// </summary>
    [SerializeField, Min(0.1f)] private float resolveRetryInterval = 0.5f;

    [Header("슬롯 UI")]
    /// <summary>
    /// 1번 슬롯 UI 참조 묶음.
    /// </summary>
    [SerializeField] private SlotUI slot1 = new SlotUI();

    /// <summary>
    /// 2번 슬롯 UI 참조 묶음.
    /// </summary>
    [SerializeField] private SlotUI slot2 = new SlotUI();

    [Header("고정 키 힌트 텍스트")]
    /// <summary>
    /// 1번 슬롯용 고정 힌트 텍스트.
    /// 슬롯과 별도로 배치되지만 항상 표시된다.
    /// </summary>
    [SerializeField] private TextMeshProUGUI slot1FixedKeyText;

    /// <summary>
    /// 2번 슬롯용 고정 힌트 텍스트.
    /// 슬롯과 별도로 배치되지만 항상 표시된다.
    /// </summary>
    [SerializeField] private TextMeshProUGUI slot2FixedKeyText;

    /// <summary>
    /// 1번 슬롯용 고정 힌트 텍스트의 스케일 대상 루트.
    /// 비워두면 slot1FixedKeyText.rectTransform 을 자동 사용한다.
    /// 텍스트를 감싸는 부모를 넣으면 더 안정적으로 확대된다.
    /// </summary>
    [SerializeField] private RectTransform slot1FixedKeyScaleRoot;

    /// <summary>
    /// 2번 슬롯용 고정 힌트 텍스트의 스케일 대상 루트.
    /// 비워두면 slot2FixedKeyText.rectTransform 을 자동 사용한다.
    /// </summary>
    [SerializeField] private RectTransform slot2FixedKeyScaleRoot;

    /// <summary>
    /// 1번 슬롯에 표시할 문자열.
    /// </summary>
    [SerializeField] private string slot1KeyLabel = "1";

    /// <summary>
    /// 2번 슬롯에 표시할 문자열.
    /// </summary>
    [SerializeField] private string slot2KeyLabel = "2";

    /// <summary>
    /// 고정 키 텍스트 색상.
    /// 어두운 HUD 위에서도 잘 보이도록 흰색 기본값을 사용한다.
    /// </summary>
    [SerializeField] private Color fixedKeyTextColor = Color.white;

    [Header("고정 키 힌트 확대")]
    /// <summary>
    /// 고정 키 힌트 텍스트가 슬롯 선택 확대를 따라갈지 여부.
    /// true면 슬롯과 같은 배율감으로 커진다.
    /// </summary>
    [SerializeField] private bool scaleFixedKeyTextWithSlot = true;

    /// <summary>
    /// 고정 키 텍스트의 추가 배율 보정값.
    /// 텍스트가 너무 크거나 작으면 여기서 미세 조정한다.
    /// </summary>
    [SerializeField] private Vector3 fixedKeyAdditionalScaleMultiplier = Vector3.one;

    [Header("좌우 연결 힌트 이미지")]
    /// <summary>
    /// 슬롯 1과 슬롯 2가 좌우로 이어질 수 있음을 보여주는 이미지.
    /// 예: 양방향 화살표, 링크선, 루프 아이콘.
    /// </summary>
    [SerializeField] private Image slotLinkImage;

    /// <summary>
    /// 좌우 연결 힌트 이미지 색상.
    /// </summary>
    [SerializeField] private Color slotLinkImageColor = Color.white;

    [Header("선택 연출")]
    /// <summary>
    /// 선택된 슬롯의 목표 크기.
    /// </summary>
    [SerializeField] private Vector3 selectedScale = new Vector3(1.15f, 1.15f, 1f);

    /// <summary>
    /// 선택되지 않은 슬롯의 목표 크기.
    /// </summary>
    [SerializeField] private Vector3 unselectedScale = new Vector3(0.88f, 0.88f, 1f);

    /// <summary>
    /// 조준 중인 선택 슬롯의 목표 크기.
    /// </summary>
    [SerializeField] private Vector3 aimingScale = new Vector3(1.2f, 1.2f, 1f);

    /// <summary>
    /// 슬롯 크기 보간 속도.
    /// </summary>
    [SerializeField, Min(0.01f)] private float scaleLerpSpeed = 12f;

    [Header("바깥 프레임 색상 (검정 계열)")]
    /// <summary>
    /// 선택된 슬롯의 바깥 프레임 색상.
    /// </summary>
    [SerializeField] private Color selectedOuterFrameColor = new Color(0f, 0f, 0f, 1f);

    /// <summary>
    /// 비선택 슬롯의 바깥 프레임 색상.
    /// </summary>
    [SerializeField] private Color unselectedOuterFrameColor = new Color(0f, 0f, 0f, 0.85f);

    /// <summary>
    /// 조준 중 슬롯의 바깥 프레임 색상.
    /// </summary>
    [SerializeField] private Color aimingOuterFrameColor = new Color(0.15f, 0.12f, 0.05f, 1f);

    [Header("안쪽 프레임 색상 (흰색 계열)")]
    /// <summary>
    /// 선택된 슬롯의 안쪽 프레임 색상.
    /// </summary>
    [SerializeField] private Color selectedInnerFrameColor = new Color(1f, 1f, 1f, 1f);

    /// <summary>
    /// 비선택 슬롯의 안쪽 프레임 색상.
    /// </summary>
    [SerializeField] private Color unselectedInnerFrameColor = new Color(1f, 1f, 1f, 0.72f);

    /// <summary>
    /// 조준 중 슬롯의 안쪽 프레임 색상.
    /// </summary>
    [SerializeField] private Color aimingInnerFrameColor = new Color(1f, 0.95f, 0.75f, 1f);

    [Header("아이콘 색상")]
    /// <summary>
    /// 아이템이 있을 때 아이콘 색상.
    /// 흰색 tint로 두어 원본 아이콘이 가장 자연스럽게 보이게 한다.
    /// </summary>
    [SerializeField] private Color iconColor = Color.white;

    /// <summary>
    /// 빈 슬롯일 때 아이콘 색상.
    /// hideIconWhenEmpty가 false일 때만 의미가 있다.
    /// </summary>
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.10f);

    [Header("표시 설정")]
    /// <summary>
    /// 슬롯이 비어 있을 때 아이콘을 숨길지 여부.
    /// true면 아이템이 없을 때 아이콘은 꺼지고 프레임만 남는다.
    /// </summary>
    [SerializeField] private bool hideIconWhenEmpty = true;

    /// <summary>
    /// 로컬 플레이어 권한이 있는 경우에만 아이템 아이콘을 표시할지 여부.
    /// 서버 로직은 건드리지 않고 UI 표시만 제한한다.
    /// </summary>
    [SerializeField] private bool requireLocalAuthority = true;

    [Header("2중 프레임 레이아웃 자동 보정")]
    /// <summary>
    /// 프레임 / 아이콘 레이아웃을 자동으로 정리할지 여부.
    /// true면 바깥 프레임은 루트를 꽉 채우고,
    /// 안쪽 프레임은 약간 안쪽,
    /// 아이콘은 더 안쪽으로 들어간다.
    /// </summary>
    [SerializeField] private bool autoConfigureDoubleFrameLayout = true;

    /// <summary>
    /// 안쪽 프레임이 바깥 프레임보다 안쪽으로 들어갈 거리.
    /// </summary>
    [SerializeField, Min(0f)] private float innerFrameInset = 3f;

    /// <summary>
    /// 아이콘이 슬롯 바깥 기준으로 안쪽으로 들어갈 거리.
    /// 값이 커질수록 프레임 outline이 더 두꺼워 보인다.
    /// </summary>
    [SerializeField, Min(0f)] private float iconInset = 9f;

    /// <summary>
    /// 프레임과 아이콘의 표시 순서를 강제로 정리할지 여부.
    /// true면 바깥 프레임 → 안쪽 프레임 → 아이콘 순서가 되도록 맞춘다.
    /// </summary>
    [SerializeField] private bool forceFrameOrder = true;

    /// <summary>
    /// 아이콘의 비율을 유지할지 여부.
    /// </summary>
    [SerializeField] private bool preserveIconAspect = true;

    /// <summary>
    /// 아이콘 스프라이트가 비어 있는데 아이템 데이터는 들어온 경우
    /// 한 번만 경고를 띄울지 여부.
    /// </summary>
    [SerializeField] private bool logWarningWhenIconSpriteMissing = true;

    /// <summary>
    /// 슬롯 배열 캐시.
    /// </summary>
    private SlotUI[] slots;

    /// <summary>
    /// 다음 자동 탐색 시도 시간.
    /// </summary>
    private float nextResolveTime;

    /// <summary>
    /// 실제로 확대를 적용할 고정 텍스트 루트 캐시.
    /// 직접 루트를 넣지 않으면 텍스트 RectTransform을 사용한다.
    /// </summary>
    private RectTransform cachedSlot1FixedKeyScaleRoot;

    /// <summary>
    /// 실제로 확대를 적용할 고정 텍스트 루트 캐시.
    /// 직접 루트를 넣지 않으면 텍스트 RectTransform을 사용한다.
    /// </summary>
    private RectTransform cachedSlot2FixedKeyScaleRoot;

    /// <summary>
    /// 고정 텍스트 루트의 원래 로컬 스케일.
    /// 확대 시 기준 배율로 사용한다.
    /// </summary>
    private Vector3 slot1FixedKeyBaseScale = Vector3.one;

    /// <summary>
    /// 고정 텍스트 루트의 원래 로컬 스케일.
    /// 확대 시 기준 배율로 사용한다.
    /// </summary>
    private Vector3 slot2FixedKeyBaseScale = Vector3.one;

    /// <summary>
    /// 슬롯 하이어라키 경고 중복 방지용 플래그.
    /// </summary>
    private bool hasWarnedSlot1Hierarchy;

    /// <summary>
    /// 슬롯 하이어라키 경고 중복 방지용 플래그.
    /// </summary>
    private bool hasWarnedSlot2Hierarchy;

    /// <summary>
    /// 아이콘 누락 경고 중복 방지용 플래그.
    /// </summary>
    private bool hasWarnedMissingSlot1Icon;

    /// <summary>
    /// 아이콘 누락 경고 중복 방지용 플래그.
    /// </summary>
    private bool hasWarnedMissingSlot2Icon;

    /// <summary>
    /// 에디터 Reset 시 참조와 기본 스케일을 캐싱한다.
    /// </summary>
    private void Reset()
    {
        ResolveReferences(true);
        CacheSlots();
        CacheFixedKeyScaleRoots();
        CaptureFixedKeyBaseScales();
        RefreshFixedTextsAndLinkImage();
    }

    /// <summary>
    /// 시작 시 참조 탐색, 스케일 캐싱, 하이어라키 검사를 수행한다.
    /// </summary>
    private void Awake()
    {
        ResolveReferences(true);
        CacheSlots();
        CacheFixedKeyScaleRoots();
        CaptureFixedKeyBaseScales();
        ValidateSlotHierarchy();
        ApplyAutoDoubleFrameLayout();
        RefreshAllUI();
        SnapAllScalesImmediately();
    }

    /// <summary>
    /// 활성화 시 이벤트 구독 후 UI를 갱신한다.
    /// </summary>
    private void OnEnable()
    {
        SubscribeEvents();
        CacheFixedKeyScaleRoots();
        CaptureFixedKeyBaseScales();
        ValidateSlotHierarchy();
        ApplyAutoDoubleFrameLayout();
        RefreshAllUI();
        SnapAllScalesImmediately();
    }

    /// <summary>
    /// 비활성화 시 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 매 프레임 자동 참조 탐색 / 슬롯 확대 / 고정 텍스트 확대 / 레이아웃 보정을 처리한다.
    /// </summary>
    private void Update()
    {
        TryResolveLocalPlayerReferencesIfNeeded();
        ApplyAutoDoubleFrameLayout();
        AnimateSlotScales();
        AnimateFixedKeyScales();
    }

    /// <summary>
    /// 전체 UI를 다시 그린다.
    /// - 1/2 고정 텍스트와 연결 이미지는 항상 유지한다.
    /// - 아이콘만 로컬 권한 / 인벤토리 상태에 따라 갱신한다.
    /// </summary>
    public void RefreshAllUI()
    {
        CacheSlots();
        CacheFixedKeyScaleRoots();
        ApplyAutoDoubleFrameLayout();
        RefreshFixedTextsAndLinkImage();

        if (!CanRenderItemIcons())
        {
            ClearSlotIconsOnly();
            return;
        }

        int selectedSlotIndex = inventory != null ? inventory.SelectedSlotIndex : -1;
        bool isAimingSelectedItem = itemRunner != null && itemRunner.IsAimingItem;

        RefreshSlot(slot1, 0, selectedSlotIndex == 0, isAimingSelectedItem && selectedSlotIndex == 0);
        RefreshSlot(slot2, 1, selectedSlotIndex == 1, isAimingSelectedItem && selectedSlotIndex == 1);
    }

    /// <summary>
    /// 필요 시 로컬 플레이어 관련 참조를 자동으로 찾는다.
    /// </summary>
    /// <param name="allowAutoResolve">자동 탐색 허용 여부.</param>
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

    /// <summary>
    /// 슬롯 배열 캐시를 준비한다.
    /// </summary>
    private void CacheSlots()
    {
        if (slots != null && slots.Length == 2)
            return;

        slots = new[] { slot1, slot2 };
    }

    /// <summary>
    /// 고정 키 텍스트 확대 루트를 캐싱한다.
    /// 직접 루트를 넣지 않았으면 텍스트 자신의 RectTransform을 사용한다.
    /// </summary>
    private void CacheFixedKeyScaleRoots()
    {
        cachedSlot1FixedKeyScaleRoot = slot1FixedKeyScaleRoot != null
            ? slot1FixedKeyScaleRoot
            : (slot1FixedKeyText != null ? slot1FixedKeyText.rectTransform : null);

        cachedSlot2FixedKeyScaleRoot = slot2FixedKeyScaleRoot != null
            ? slot2FixedKeyScaleRoot
            : (slot2FixedKeyText != null ? slot2FixedKeyText.rectTransform : null);
    }

    /// <summary>
    /// 고정 키 텍스트 루트의 현재 스케일을 기본값으로 저장한다.
    /// 텍스트를 원하는 크기로 잡아둔 뒤 그 위에 확대 배율만 곱할 수 있다.
    /// </summary>
    private void CaptureFixedKeyBaseScales()
    {
        if (cachedSlot1FixedKeyScaleRoot != null)
            slot1FixedKeyBaseScale = cachedSlot1FixedKeyScaleRoot.localScale;

        if (cachedSlot2FixedKeyScaleRoot != null)
            slot2FixedKeyBaseScale = cachedSlot2FixedKeyScaleRoot.localScale;
    }

    /// <summary>
    /// 인벤토리와 아이템 러너 이벤트를 구독한다.
    /// </summary>
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

    /// <summary>
    /// 인벤토리와 아이템 러너 이벤트 구독을 해제한다.
    /// </summary>
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

    /// <summary>
    /// 인벤토리 내용이 바뀌면 UI를 다시 그린다.
    /// </summary>
    private void HandleInventoryChanged()
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 선택 슬롯이 바뀌면 UI를 다시 그린다.
    /// </summary>
    /// <param name="_">변경된 선택 슬롯 인덱스.</param>
    private void HandleSelectedSlotChanged(int _)
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 조준 상태가 바뀌면 UI를 다시 그린다.
    /// </summary>
    /// <param name="_">조준 상태 여부.</param>
    private void HandleAimStateChanged(bool _)
    {
        RefreshAllUI();
    }

    /// <summary>
    /// 자동 탐색이 켜져 있으면 주기적으로 로컬 플레이어 참조를 다시 찾는다.
    /// </summary>
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
        SnapAllScalesImmediately();
    }

    /// <summary>
    /// 슬롯 하나를 갱신한다.
    /// </summary>
    /// <param name="slot">대상 슬롯 UI.</param>
    /// <param name="slotIndex">인벤토리 슬롯 인덱스.</param>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    private void RefreshSlot(SlotUI slot, int slotIndex, bool isSelected, bool isAiming)
    {
        if (slot == null)
            return;

        if (!TryGetSlotInfo(slotIndex, out ItemDefinition definition, out int count))
        {
            ApplyEmptySlot(slot, isSelected, isAiming);
            return;
        }

        ApplyFilledSlot(slot, definition, count, isSelected, isAiming, slotIndex);
    }

    /// <summary>
    /// 인벤토리에서 슬롯 정보를 가져온다.
    /// </summary>
    /// <param name="slotIndex">조회할 슬롯 번호.</param>
    /// <param name="definition">아이템 정의 반환값.</param>
    /// <param name="count">현재 수량 반환값.</param>
    /// <returns>유효한 아이템이 있으면 true.</returns>
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

    /// <summary>
    /// 아이템이 있는 슬롯 상태를 적용한다.
    /// </summary>
    /// <param name="slot">대상 슬롯.</param>
    /// <param name="definition">표시할 아이템 정의.</param>
    /// <param name="count">현재 수량. 이 UI에서는 표시하지 않지만 유효성 판단에 사용한다.</param>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    /// <param name="slotIndex">슬롯 번호. 경고 로그 구분용.</param>
    private void ApplyFilledSlot(SlotUI slot, ItemDefinition definition, int count, bool isSelected, bool isAiming, int slotIndex)
    {
        ApplyDoubleFrameColors(slot, isSelected, isAiming);

        if (definition != null && definition.Icon == null)
        {
            LogMissingIconWarning(slotIndex);
        }

        ApplySlotIcon(slot.IconImage, definition != null ? definition.Icon : null, true);
    }

    /// <summary>
    /// 빈 슬롯 상태를 적용한다.
    /// 프레임은 남기고 아이콘만 비우는 방식이다.
    /// </summary>
    /// <param name="slot">대상 슬롯.</param>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    private void ApplyEmptySlot(SlotUI slot, bool isSelected, bool isAiming)
    {
        ApplyDoubleFrameColors(slot, isSelected, isAiming);
        ApplySlotIcon(slot.IconImage, null, false);
    }

    /// <summary>
    /// 바깥 프레임과 안쪽 프레임의 색상을 현재 상태에 맞게 적용한다.
    /// </summary>
    /// <param name="slot">대상 슬롯.</param>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    private void ApplyDoubleFrameColors(SlotUI slot, bool isSelected, bool isAiming)
    {
        if (slot == null)
            return;

        Image outerFrameImage = slot.OuterFrameImage;
        if (outerFrameImage != null)
        {
            outerFrameImage.material = null;
            outerFrameImage.enabled = true;
            outerFrameImage.color = GetOuterFrameColor(isSelected, isAiming);
        }

        Image innerFrameImage = slot.InnerFrameImage;
        if (innerFrameImage != null)
        {
            innerFrameImage.material = null;
            innerFrameImage.enabled = true;
            innerFrameImage.color = GetInnerFrameColor(isSelected, isAiming);
        }
    }

    /// <summary>
    /// 슬롯 아이콘 표시를 적용한다.
    /// </summary>
    /// <param name="iconImage">대상 아이콘 이미지.</param>
    /// <param name="iconSprite">표시할 스프라이트.</param>
    /// <param name="hasItem">아이템 존재 여부.</param>
    private void ApplySlotIcon(Image iconImage, Sprite iconSprite, bool hasItem)
    {
        if (iconImage == null)
            return;

        iconImage.material = null;
        iconImage.preserveAspect = preserveIconAspect;
        iconImage.sprite = iconSprite;
        iconImage.overrideSprite = null;
        iconImage.color = hasItem ? iconColor : emptyIconColor;
        iconImage.enabled = hasItem
            ? (iconSprite != null || !hideIconWhenEmpty)
            : !hideIconWhenEmpty;
    }

    /// <summary>
    /// 고정 키 텍스트와 좌우 연결 이미지를 항상 표시 상태로 갱신한다.
    /// </summary>
    private void RefreshFixedTextsAndLinkImage()
    {
        ApplyFixedKeyText(slot1FixedKeyText, slot1KeyLabel);
        ApplyFixedKeyText(slot2FixedKeyText, slot2KeyLabel);
        ApplySlotLinkImage();
    }

    /// <summary>
    /// 고정 키 텍스트 하나를 적용한다.
    /// 텍스트는 항상 켜진 상태를 유지한다.
    /// </summary>
    /// <param name="targetText">대상 텍스트.</param>
    /// <param name="label">표시할 문자열.</param>
    private void ApplyFixedKeyText(TextMeshProUGUI targetText, string label)
    {
        if (targetText == null)
            return;

        targetText.text = label;
        targetText.color = fixedKeyTextColor;
        targetText.enabled = true;
    }

    /// <summary>
    /// 좌우 연결 힌트 이미지를 항상 켜고 색상을 적용한다.
    /// </summary>
    private void ApplySlotLinkImage()
    {
        if (slotLinkImage == null)
            return;

        slotLinkImage.enabled = true;
        slotLinkImage.color = slotLinkImageColor;
    }

    /// <summary>
    /// 현재 상태에 맞는 바깥 프레임 색상을 반환한다.
    /// </summary>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    /// <returns>적용할 바깥 프레임 색상.</returns>
    private Color GetOuterFrameColor(bool isSelected, bool isAiming)
    {
        if (isAiming)
            return aimingOuterFrameColor;

        return isSelected ? selectedOuterFrameColor : unselectedOuterFrameColor;
    }

    /// <summary>
    /// 현재 상태에 맞는 안쪽 프레임 색상을 반환한다.
    /// </summary>
    /// <param name="isSelected">선택 여부.</param>
    /// <param name="isAiming">조준 여부.</param>
    /// <returns>적용할 안쪽 프레임 색상.</returns>
    private Color GetInnerFrameColor(bool isSelected, bool isAiming)
    {
        if (isAiming)
            return aimingInnerFrameColor;

        return isSelected ? selectedInnerFrameColor : unselectedInnerFrameColor;
    }

    /// <summary>
    /// 슬롯 목표 크기를 계산한다.
    /// </summary>
    /// <param name="slotIndex">슬롯 번호.</param>
    /// <returns>목표 스케일.</returns>
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

    /// <summary>
    /// 슬롯들의 스케일을 부드럽게 보간한다.
    /// </summary>
    private void AnimateSlotScales()
    {
        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            AnimateSingleSlotScale(slots[i], GetTargetScale(i));
        }
    }

    /// <summary>
    /// 슬롯 하나의 스케일을 보간한다.
    /// </summary>
    /// <param name="slot">대상 슬롯.</param>
    /// <param name="targetScale">목표 스케일.</param>
    private void AnimateSingleSlotScale(SlotUI slot, Vector3 targetScale)
    {
        if (slot == null || slot.Root == null)
            return;

        slot.Root.localScale = Vector3.Lerp(
            slot.Root.localScale,
            targetScale,
            Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    /// <summary>
    /// 고정 키 텍스트도 선택 슬롯 확대를 따라가도록 스케일을 보간한다.
    /// </summary>
    private void AnimateFixedKeyScales()
    {
        if (!scaleFixedKeyTextWithSlot)
        {
            AnimateSingleTransformScale(cachedSlot1FixedKeyScaleRoot, slot1FixedKeyBaseScale);
            AnimateSingleTransformScale(cachedSlot2FixedKeyScaleRoot, slot2FixedKeyBaseScale);
            return;
        }

        AnimateSingleTransformScale(
            cachedSlot1FixedKeyScaleRoot,
            MultiplyScale(slot1FixedKeyBaseScale, MultiplyScale(GetTargetScale(0), fixedKeyAdditionalScaleMultiplier)));

        AnimateSingleTransformScale(
            cachedSlot2FixedKeyScaleRoot,
            MultiplyScale(slot2FixedKeyBaseScale, MultiplyScale(GetTargetScale(1), fixedKeyAdditionalScaleMultiplier)));
    }

    /// <summary>
    /// 외부 RectTransform 하나를 목표 스케일까지 부드럽게 보간한다.
    /// </summary>
    /// <param name="target">대상 RectTransform.</param>
    /// <param name="desiredScale">목표 스케일.</param>
    private void AnimateSingleTransformScale(RectTransform target, Vector3 desiredScale)
    {
        if (target == null)
            return;

        target.localScale = Vector3.Lerp(
            target.localScale,
            desiredScale,
            Time.unscaledDeltaTime * scaleLerpSpeed);
    }

    /// <summary>
    /// 슬롯과 고정 키 텍스트의 현재 상태 스케일을 즉시 적용한다.
    /// </summary>
    private void SnapAllScalesImmediately()
    {
        SnapSlotScalesImmediately();
        SnapFixedKeyScalesImmediately();
    }

    /// <summary>
    /// 슬롯 스케일을 현재 상태로 즉시 맞춘다.
    /// </summary>
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

    /// <summary>
    /// 고정 키 텍스트 스케일도 즉시 현재 상태에 맞춘다.
    /// </summary>
    private void SnapFixedKeyScalesImmediately()
    {
        if (!scaleFixedKeyTextWithSlot)
        {
            if (cachedSlot1FixedKeyScaleRoot != null)
                cachedSlot1FixedKeyScaleRoot.localScale = slot1FixedKeyBaseScale;

            if (cachedSlot2FixedKeyScaleRoot != null)
                cachedSlot2FixedKeyScaleRoot.localScale = slot2FixedKeyBaseScale;

            return;
        }

        if (cachedSlot1FixedKeyScaleRoot != null)
        {
            cachedSlot1FixedKeyScaleRoot.localScale =
                MultiplyScale(slot1FixedKeyBaseScale, MultiplyScale(GetTargetScale(0), fixedKeyAdditionalScaleMultiplier));
        }

        if (cachedSlot2FixedKeyScaleRoot != null)
        {
            cachedSlot2FixedKeyScaleRoot.localScale =
                MultiplyScale(slot2FixedKeyBaseScale, MultiplyScale(GetTargetScale(1), fixedKeyAdditionalScaleMultiplier));
        }
    }

    /// <summary>
    /// 두 Vector3 스케일을 축별로 곱한다.
    /// </summary>
    /// <param name="a">첫 번째 스케일.</param>
    /// <param name="b">두 번째 스케일.</param>
    /// <returns>축별 곱 결과.</returns>
    private Vector3 MultiplyScale(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    /// <summary>
    /// 아이콘만 비운다.
    /// 프레임 / 1,2 텍스트 / 연결 이미지는 유지한다.
    /// </summary>
    private void ClearSlotIconsOnly()
    {
        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            ApplyEmptySlot(slots[i], false, false);

            if (slots[i].Root != null)
            {
                slots[i].Root.localScale = unselectedScale;
            }
        }

        SnapFixedKeyScalesImmediately();
    }

    /// <summary>
    /// 2중 프레임 / 아이콘 배치를 자동 정리해서
    /// 바깥 프레임 → 안쪽 프레임 → 아이콘 구조가 확실히 보이게 만든다.
    /// </summary>
    private void ApplyAutoDoubleFrameLayout()
    {
        if (!autoConfigureDoubleFrameLayout)
            return;

        CacheSlots();

        for (int i = 0; i < slots.Length; i++)
        {
            ApplySingleSlotDoubleFrameLayout(slots[i]);
        }
    }

    /// <summary>
    /// 슬롯 하나의 2중 프레임 / 아이콘 배치를 정리한다.
    /// </summary>
    /// <param name="slot">대상 슬롯.</param>
    private void ApplySingleSlotDoubleFrameLayout(SlotUI slot)
    {
        if (slot == null || slot.Root == null)
            return;

        RectTransform rootRect = slot.Root;
        Image outerFrameImage = slot.OuterFrameImage;
        Image innerFrameImage = slot.InnerFrameImage;
        Image iconImage = slot.IconImage;

        if (outerFrameImage != null)
        {
            outerFrameImage.raycastTarget = false;

            RectTransform outerRect = outerFrameImage.rectTransform;
            if (outerRect != null && outerRect.parent == rootRect)
            {
                StretchRectToParent(outerRect, 0f);
            }
        }

        if (innerFrameImage != null)
        {
            innerFrameImage.raycastTarget = false;

            RectTransform innerRect = innerFrameImage.rectTransform;
            if (innerRect != null && innerRect.parent == rootRect)
            {
                StretchRectToParent(innerRect, innerFrameInset);
            }
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = preserveIconAspect;

            RectTransform iconRect = iconImage.rectTransform;
            if (iconRect != null && iconRect.parent == rootRect)
            {
                StretchRectToParent(iconRect, iconInset);
            }
        }

        if (forceFrameOrder)
        {
            if (outerFrameImage != null)
            {
                outerFrameImage.transform.SetAsFirstSibling();
            }

            if (innerFrameImage != null)
            {
                innerFrameImage.transform.SetAsLastSibling();
            }

            if (iconImage != null)
            {
                iconImage.transform.SetAsLastSibling();
            }
        }
    }

    /// <summary>
    /// RectTransform을 부모 전체에 맞춰 늘리고 inset만 적용한다.
    /// </summary>
    /// <param name="targetRect">정리할 RectTransform.</param>
    /// <param name="inset">안쪽 여백.</param>
    private void StretchRectToParent(RectTransform targetRect, float inset)
    {
        if (targetRect == null)
            return;

        targetRect.anchorMin = Vector2.zero;
        targetRect.anchorMax = Vector2.one;
        targetRect.pivot = new Vector2(0.5f, 0.5f);
        targetRect.anchoredPosition = Vector2.zero;
        targetRect.offsetMin = new Vector2(inset, inset);
        targetRect.offsetMax = new Vector2(-inset, -inset);
        targetRect.localScale = Vector3.one;
        targetRect.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 아이콘 표시를 해도 되는지 검사한다.
    /// 고정 텍스트와 연결 이미지는 이 검사와 관계없이 항상 표시한다.
    /// </summary>
    /// <returns>아이콘 렌더 가능 여부.</returns>
    private bool CanRenderItemIcons()
    {
        if (inventory == null)
            return false;

        if (!requireLocalAuthority)
            return true;

        if (inputSource == null)
            return true;

        return inputSource.HasLocalAuthority;
    }

    /// <summary>
    /// 로컬 플레이어 기준으로 필요한 참조를 자동 탐색한다.
    /// </summary>
    /// <returns>성공적으로 참조를 찾았으면 true.</returns>
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

    /// <summary>
    /// 새 참조를 할당한다.
    /// 참조가 바뀌면 이벤트도 다시 연결한다.
    /// </summary>
    /// <param name="newInventory">새 인벤토리.</param>
    /// <param name="newItemRunner">새 아이템 러너.</param>
    /// <param name="newInputSource">새 입력 소스.</param>
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

    /// <summary>
    /// 현재 참조가 로컬 플레이어 기준으로 정상인지 검사한다.
    /// </summary>
    /// <returns>정상 바인딩 여부.</returns>
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

    /// <summary>
    /// 현재 씬에서 로컬 소유 컴포넌트를 찾는다.
    /// </summary>
    /// <typeparam name="T">찾을 컴포넌트 타입.</typeparam>
    /// <returns>찾은 컴포넌트. 없으면 null.</returns>
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

    /// <summary>
    /// 특정 플레이어 오브젝트 범위 안에서 원하는 컴포넌트를 찾는다.
    /// </summary>
    /// <typeparam name="T">찾을 컴포넌트 타입.</typeparam>
    /// <param name="anchor">탐색 기준 컴포넌트.</param>
    /// <returns>찾은 컴포넌트. 없으면 null.</returns>
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

    /// <summary>
    /// 실제 씬 인스턴스인지 검사한다.
    /// </summary>
    /// <param name="component">검사 대상 컴포넌트.</param>
    /// <returns>실제 씬 인스턴스면 true.</returns>
    private static bool IsSceneInstance(Component component)
    {
        if (component == null)
            return false;

        if (!component.gameObject.scene.IsValid())
            return false;

        return (component.hideFlags & HideFlags.NotEditable) == 0;
    }

    /// <summary>
    /// slot.Root가 프레임과 아이콘을 함께 감싸는 구조인지 검사한다.
    /// 이게 틀어져 있으면 선택 확대 시 프레임이 같이 커지지 않는다.
    /// </summary>
    private void ValidateSlotHierarchy()
    {
        ValidateSingleSlotHierarchy(slot1, "Slot1", ref hasWarnedSlot1Hierarchy);
        ValidateSingleSlotHierarchy(slot2, "Slot2", ref hasWarnedSlot2Hierarchy);
    }

    /// <summary>
    /// 단일 슬롯 하이어라키를 검사하고, 문제가 있으면 한 번만 경고한다.
    /// </summary>
    /// <param name="slot">검사 대상 슬롯.</param>
    /// <param name="slotName">로그용 슬롯 이름.</param>
    /// <param name="hasWarned">이미 경고했는지 여부.</param>
    private void ValidateSingleSlotHierarchy(SlotUI slot, string slotName, ref bool hasWarned)
    {
        if (hasWarned || slot == null || slot.Root == null)
            return;

        Transform rootTransform = slot.Root;
        bool outerIsChild = slot.OuterFrameImage != null && slot.OuterFrameImage.transform.IsChildOf(rootTransform);
        bool innerIsChild = slot.InnerFrameImage != null && slot.InnerFrameImage.transform.IsChildOf(rootTransform);
        bool iconIsChild = slot.IconImage != null && slot.IconImage.transform.IsChildOf(rootTransform);

        if (outerIsChild && innerIsChild && iconIsChild)
            return;

        hasWarned = true;

        Debug.LogWarning(
            $"[PlayerQuickSlotUI] {slotName} 설정 확인 필요: " +
            $"slot.Root는 OuterFrame / InnerFrame / Icon 을 함께 감싸는 공통 부모여야 합니다. " +
            $"현재 이 구조가 아니면 선택 확대와 2중 프레임 표시가 틀어질 수 있습니다.",
            this);
    }

    /// <summary>
    /// 아이템 데이터는 있는데 아이콘 스프라이트가 비어 있을 때 한 번만 경고한다.
    /// </summary>
    /// <param name="slotIndex">문제가 발생한 슬롯 인덱스.</param>
    private void LogMissingIconWarning(int slotIndex)
    {
        if (!logWarningWhenIconSpriteMissing)
            return;

        if (slotIndex == 0)
        {
            if (hasWarnedMissingSlot1Icon)
                return;

            hasWarnedMissingSlot1Icon = true;
            Debug.LogWarning("[PlayerQuickSlotUI] Slot1 아이템 데이터는 있지만 Icon 스프라이트가 null 입니다. 아이콘이 안 보이면 ItemDefinition.Icon 설정을 확인하세요.", this);
            return;
        }

        if (slotIndex == 1)
        {
            if (hasWarnedMissingSlot2Icon)
                return;

            hasWarnedMissingSlot2Icon = true;
            Debug.LogWarning("[PlayerQuickSlotUI] Slot2 아이템 데이터는 있지만 Icon 스프라이트가 null 입니다. 아이콘이 안 보이면 ItemDefinition.Icon 설정을 확인하세요.", this);
        }
    }
}