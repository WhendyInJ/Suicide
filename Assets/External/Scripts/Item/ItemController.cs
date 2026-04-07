using Photon.Pun;
using UnityEngine;

/// <summary>
/// 기존 원본 아이템 실행기.
/// 
/// 파일명과 클래스명은 유지하되,
/// 역할은 "현재 퀵슬롯에서 선택된 아이템을 실제 능력으로 실행하는 어댑터"로 사용한다.
/// 
/// 기존 legacy 방식(Q키 직접 사용, EquipItem)도 옵션으로 남겨두었다.
/// </summary>
public class ItemController : MonoBehaviour, SuicideSquad_IPlayerItemReceiver
{
    [Header("Legacy Direct Use (Optional)")]

    /// <summary>
    /// 기존처럼 Q 키로 현재 장착 아이템을 직접 사용할지 여부.
    /// 새 퀵슬롯 시스템을 사용할 때는 보통 false로 두는 것을 권장한다.
    /// </summary>
    [SerializeField] private bool allowLegacyDirectUseInput = false;

    /// <summary>
    /// 기존 direct use 입력 키.
    /// </summary>
    [SerializeField] private KeyCode useKey = KeyCode.Q;

    [Header("Legacy Equipped State")]

    /// <summary>
    /// 현재 원본 시스템 기준으로 장착된 아이템 타입.
    /// 새 퀵슬롯 시스템에서는 아이템 사용 직전에 이 값이 갱신된다.
    /// </summary>
    [SerializeField] private ItemType currentItemType = ItemType.None;

    /// <summary>
    /// 현재 장착된 원본 아이템 타입.
    /// </summary>
    public ItemType CurrentItemType => currentItemType;

    /// <summary>
    /// 멀티플레이에서 로컬 소유권 체크를 위한 PhotonView.
    /// </summary>
    private PhotonView photonView = null;

    /// <summary>
    /// 시작 시 PhotonView 참조를 캐싱한다.
    /// </summary>
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();

        if (photonView == null)
        {
            photonView = GetComponentInParent<PhotonView>();
        }
    }

    /// <summary>
    /// 필요 시 기존 direct use 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        if (allowLegacyDirectUseInput == false)
        {
            return;
        }

        if (IsLocalOwner() == false)
        {
            return;
        }

        if (!Input.GetKeyDown(useKey))
        {
            return;
        }

        UseCurrentItem();
    }

    /// <summary>
    /// 기존 원본 아이템 타입을 장착 상태로 반영한다.
    /// 필요한 ability를 켜고, 필요 없는 ability는 끈다.
    /// </summary>
    /// <param name="newItemType">장착할 원본 아이템 타입.</param>
    public void EquipItem(ItemType newItemType)
    {
        currentItemType = newItemType;
        ApplyAbilityState(currentItemType);
    }

    /// <summary>
    /// 새 퀵슬롯 시스템이 이 ItemController에게 해당 아이템 사용을 넘겨도 되는지 검사한다.
    /// </summary>
    /// <param name="itemData">검사할 새 아이템 데이터.</param>
    /// <returns>기존 능력 시스템과 연결 가능한 아이템이면 true.</returns>
    public bool CanUseItem(SuicideSquad_ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        ItemType resolvedType = ResolveLegacyItemType(itemData);
        return resolvedType != ItemType.None;
    }

    /// <summary>
    /// 새 퀵슬롯 시스템에서 전달된 아이템 데이터를
    /// 기존 능력 시스템으로 실제 실행한다.
    /// </summary>
    /// <param name="itemData">사용할 새 아이템 데이터.</param>
    /// <returns>실행 성공 시 true.</returns>
    public bool TryUseItem(SuicideSquad_ItemData itemData)
    {
        // TODO (협업):
        // 지금은 linkedLegacyItemType 기반으로 기존 능력 시스템과 연결한다.
        // 추후 회복/실드/버프 등의 신규 시스템을 붙일 경우,
        // 여기 또는 별도 receiver에서 분기 확장 가능하다.

        if (IsLocalOwner() == false)
        {
            return false;
        }

        if (CanUseItem(itemData) == false)
        {
            return false;
        }

        ItemType resolvedType = ResolveLegacyItemType(itemData);

        EquipItem(resolvedType);
        return UseCurrentItem();
    }

    /// <summary>
    /// 현재 장착된 원본 아이템을 실제로 사용한다.
    /// </summary>
    /// <returns>성공적으로 실행했으면 true.</returns>
    public bool UseCurrentItem()
    {
        switch (currentItemType)
        {
            case ItemType.NautilusGrab:
                {
                    NautilusGrabAbility grab = GetComponent<NautilusGrabAbility>();

                    if (grab != null && grab.isActiveAndEnabled)
                    {
                        grab.StartCoroutine(grab.Execute());
                        return true;
                    }

                    return false;
                }

            case ItemType.StructureShockwave:
                {
                    ShockwaveAbility shockwave = GetComponent<ShockwaveAbility>();

                    if (shockwave != null && shockwave.isActiveAndEnabled)
                    {
                        shockwave.Execute();
                        return true;
                    }

                    return false;
                }
        }

        return false;
    }

    /// <summary>
    /// 현재 장착 타입에 맞게 필요한 ability를 켜고, 나머지는 끈다.
    /// </summary>
    /// <param name="itemType">반영할 장착 타입.</param>
    private void ApplyAbilityState(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.NautilusGrab:
                EnsureAbility<NautilusGrabAbility>(true);
                EnsureAbility<ShockwaveAbility>(false);
                break;

            case ItemType.StructureShockwave:
                EnsureAbility<ShockwaveAbility>(true);
                EnsureAbility<NautilusGrabAbility>(false);
                break;

            default:
                EnsureAbility<NautilusGrabAbility>(false);
                EnsureAbility<ShockwaveAbility>(false);
                break;
        }
    }

    /// <summary>
    /// 새 아이템 데이터에서 기존 원본 능력 타입 연결값을 가져온다.
    /// </summary>
    /// <param name="itemData">해석할 새 아이템 데이터.</param>
    /// <returns>기존 원본 능력 타입. 연결 안 되어 있으면 None.</returns>
    private ItemType ResolveLegacyItemType(SuicideSquad_ItemData itemData)
    {
        if (itemData == null)
        {
            return ItemType.None;
        }

        return itemData.LinkedLegacyItemType;
    }

    /// <summary>
    /// 필요한 ability 컴포넌트를 보장하고 활성/비활성 상태를 맞춘다.
    /// </summary>
    /// <typeparam name="T">대상 ability 타입.</typeparam>
    /// <param name="shouldEnable">활성화 여부.</param>
    /// <returns>찾거나 생성한 ability 컴포넌트.</returns>
    private T EnsureAbility<T>(bool shouldEnable) where T : Behaviour
    {
        T ability = GetComponent<T>();

        if (ability == null && shouldEnable)
        {
            ability = gameObject.AddComponent<T>();
        }

        if (ability != null)
        {
            ability.enabled = shouldEnable;
        }

        return ability;
    }

    /// <summary>
    /// 멀티플레이 시 이 오브젝트가 로컬 소유자인지 검사한다.
    /// 오프라인 테스트에서는 항상 true를 반환한다.
    /// </summary>
    /// <returns>로컬 소유자면 true.</returns>
    private bool IsLocalOwner()
    {
        if (!PhotonNetwork.IsConnected)
        {
            return true;
        }

        if (photonView == null)
        {
            return false;
        }

        return photonView.IsMine;
    }
}