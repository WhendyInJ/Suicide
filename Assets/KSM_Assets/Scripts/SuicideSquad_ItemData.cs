using UnityEngine;

/// <summary>
/// 아이템 종류를 구분하는 enum.
/// 인벤토리 저장, UI 표시, 아이템 사용 분기에서 사용한다.
/// </summary>
public enum SuicideSquad_ItemType
{
    /// <summary>
    /// 비어 있는 상태를 의미하는 기본값.
    /// </summary>
    None = 0,

    /// <summary>
    /// 체력 회복용 아이템.
    /// </summary>
    MedKit = 1,

    /// <summary>
    /// 이동 속도 증가 아이템.
    /// </summary>
    SpeedBooster = 2,

    /// <summary>
    /// 실드/보호막 계열 아이템.
    /// </summary>
    ShieldBattery = 3,

    /// <summary>
    /// 상태이상 제거 계열 아이템.
    /// </summary>
    CleanseKit = 4,

    /// <summary>
    /// 장애물 무시/방해 해제 계열 아이템.
    /// </summary>
    Breaker = 5
}

/// <summary>
/// 아이템 사용 시 어떤 효과를 줄지 구분하는 enum.
/// 플레이어 쪽에서 이 값을 보고 실제 효과를 적용한다.
/// </summary>
public enum SuicideSquad_ItemEffectType
{
    /// <summary>
    /// 효과 없음.
    /// </summary>
    None = 0,

    /// <summary>
    /// 체력 회복.
    /// </summary>
    Heal = 1,

    /// <summary>
    /// 이동 속도 증가.
    /// </summary>
    SpeedBuff = 2,

    /// <summary>
    /// 실드/보호막 적용.
    /// </summary>
    Shield = 3,

    /// <summary>
    /// 상태이상/방해 제거.
    /// </summary>
    Cleanse = 4,

    /// <summary>
    /// 장애물 무시 횟수 부여.
    /// </summary>
    IgnoreObstacle = 5
}

/// <summary>
/// 아이템의 원본 데이터를 저장하는 ScriptableObject.
/// 인벤토리는 이 데이터를 참조하고,
/// UI는 여기서 이름/아이콘을 읽고,
/// 플레이어는 여기서 효과 정보를 읽는다.
///
/// 추가로 linkedLegacyItemType 을 통해
/// 기존 원본 ItemController / ItemType 시스템과 연결할 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "SuicideSquad_ItemData", menuName = "SuicideSquad/Item Data")]
public class SuicideSquad_ItemData : ScriptableObject
{
    [Header("기본 정보")]

    /// <summary>
    /// 아이템 종류 구분값.
    /// SuicideSquad 전용 인벤토리/표시 시스템에서 사용한다.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemType itemType = SuicideSquad_ItemType.None;

    /// <summary>
    /// UI에 표시할 이름.
    /// </summary>
    [SerializeField] private string displayName = "New Item";

    /// <summary>
    /// UI에 표시할 아이콘 이미지.
    /// </summary>
    [SerializeField] private Sprite iconSprite = null;

    [Header("스택 설정")]

    /// <summary>
    /// 한 슬롯에 최대로 쌓을 수 있는 개수.
    /// </summary>
    [SerializeField, Min(1)] private int maxStackCount = 99;

    [Header("효과 정보")]

    /// <summary>
    /// 아이템이 어떤 효과를 가지는지 정의한다.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemEffectType effectType = SuicideSquad_ItemEffectType.None;

    /// <summary>
    /// 효과 수치.
    /// Heal이면 회복량, SpeedBuff면 배율/증가량 등으로 해석한다.
    /// </summary>
    [SerializeField] private float effectValue = 0f;

    /// <summary>
    /// 지속 시간이 필요한 효과의 시간 값.
    /// </summary>
    [SerializeField] private float effectDuration = 0f;

    [Header("원본 능력 시스템 연결")]

    /// <summary>
    /// 기존 원본 ItemController / ItemType 시스템과 연결하기 위한 브리지 값.
    ///
    /// 주의:
    /// 이 클래스 안에는 ItemType 이라는 프로퍼티가 이미 존재하므로,
    /// 기존 전역 enum ItemType 과 이름 충돌이 발생할 수 있다.
    /// 그래서 반드시 global::ItemType 으로 명시해서 사용한다.
    /// </summary>
    [SerializeField] private global::ItemType linkedLegacyItemType = global::ItemType.None;

    /// <summary>
    /// 외부에서 읽는 SuicideSquad 전용 아이템 종류.
    /// </summary>
    public SuicideSquad_ItemType ItemType => itemType;

    /// <summary>
    /// 외부에서 읽는 표시용 이름.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 외부에서 읽는 아이콘 이미지.
    /// </summary>
    public Sprite IconSprite => iconSprite;

    /// <summary>
    /// 외부에서 읽는 최대 스택 수.
    /// </summary>
    public int MaxStackCount => maxStackCount;

    /// <summary>
    /// 외부에서 읽는 효과 타입.
    /// </summary>
    public SuicideSquad_ItemEffectType EffectType => effectType;

    /// <summary>
    /// 외부에서 읽는 효과 수치.
    /// </summary>
    public float EffectValue => effectValue;

    /// <summary>
    /// 외부에서 읽는 효과 지속 시간.
    /// </summary>
    public float EffectDuration => effectDuration;

    /// <summary>
    /// 외부에서 읽는 기존 원본 능력 타입 연결값.
    /// 기존 ItemController 쪽에서 실제 능력 실행 시 사용한다.
    /// </summary>
    public global::ItemType LinkedLegacyItemType => linkedLegacyItemType;

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터 값이 비정상적으로 들어가는 것을 방지한다.
    /// </summary>
    private void OnValidate()
    {
        maxStackCount = Mathf.Max(1, maxStackCount);
        effectDuration = Mathf.Max(0f, effectDuration);
    }
#endif
}