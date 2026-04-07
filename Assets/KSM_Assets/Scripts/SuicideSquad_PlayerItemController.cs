using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// 플레이어가 아이템 효과를 받을 수 있도록 하는 인터페이스.
/// 기존 PlayerHealth, PlayerMovement, StatusEffect 같은 스크립트가
/// 이 인터페이스를 구현하면 PlayerItemController가 아이템 사용을 전달할 수 있다.
/// </summary>
public interface SuicideSquad_IPlayerItemReceiver
{
    /// <summary>
    /// 이 리시버가 해당 아이템을 처리할 수 있는지 검사한다.
    /// </summary>
    /// <param name="itemData">검사할 아이템 데이터.</param>
    /// <returns>처리 가능하면 true.</returns>
    bool CanUseItem(SuicideSquad_ItemData itemData);

    /// <summary>
    /// 실제로 아이템 효과를 적용한다.
    /// </summary>
    /// <param name="itemData">적용할 아이템 데이터.</param>
    /// <returns>적용 성공 시 true.</returns>
    bool TryUseItem(SuicideSquad_ItemData itemData);
}

/// <summary>
/// 플레이어 입력을 받아 퀵슬롯 이동과 아이템 사용을 처리하는 컨트롤러.
/// 인벤토리 자체는 아이템 정보만 관리하고,
/// 이 스크립트는 "플레이어가 그 정보를 사용"하는 흐름만 담당한다.
/// </summary>
public class SuicideSquad_PlayerItemController : MonoBehaviour
{
    [Header("기본 참조")]

    /// <summary>
    /// 플레이어가 사용하는 인벤토리 참조.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemInventory itemInventory = null;

    /// <summary>
    /// 플레이어 루트 오브젝트.
    /// 이 루트 아래에서 SuicideSquad_IPlayerItemReceiver 구현체를 찾는다.
    /// </summary>
    [SerializeField] private Transform playerRoot = null;

    [Header("입력 설정")]

    /// <summary>
    /// 이전 슬롯으로 이동하는 키.
    /// </summary>
    [SerializeField] private KeyCode previousSlotKey = KeyCode.J;

    /// <summary>
    /// 다음 슬롯으로 이동하는 키.
    /// </summary>
    [SerializeField] private KeyCode nextSlotKey = KeyCode.K;

    /// <summary>
    /// 현재 선택된 아이템 사용 키.
    /// </summary>
    [SerializeField] private KeyCode useItemKey = KeyCode.L;

    /// <summary>
    /// 이 스크립트가 직접 키 입력을 처리할지 여부.
    /// New Input System이면 false로 두고 외부에서 함수 호출해도 된다.
    /// </summary>
    [SerializeField] private bool handleKeyboardInput = true;

    [Header("네트워크 입력 제어")]

    /// <summary>
    /// 멀티플레이 시 로컬 소유 플레이어만 입력을 받도록 제한할지 여부.
    /// </summary>
    [SerializeField] private bool blockRemoteInputInNetwork = true;

    /// <summary>
    /// 시작 시 플레이어 리시버를 자동으로 찾을지 여부.
    /// </summary>
    [SerializeField] private bool autoCacheReceiversOnAwake = true;

    /// <summary>
    /// 현재 플레이어 루트에서 찾은 아이템 처리 대상들.
    /// </summary>
    private readonly List<SuicideSquad_IPlayerItemReceiver> cachedReceivers = new List<SuicideSquad_IPlayerItemReceiver>();

    /// <summary>
    /// 멀티플레이 로컬 소유권 확인용 PhotonView.
    /// </summary>
    private PhotonView photonView = null;

    /// <summary>
    /// 시작 시 참조를 보정하고 플레이어 리시버를 캐싱한다.
    /// </summary>
    private void Awake()
    {
        if (playerRoot == null)
        {
            playerRoot = transform;
        }

        photonView = GetComponent<PhotonView>();

        if (photonView == null)
        {
            photonView = GetComponentInParent<PhotonView>();
        }

        if (autoCacheReceiversOnAwake)
        {
            CacheReceivers();
        }
    }

    /// <summary>
    /// 매 프레임 키 입력을 확인한다.
    /// </summary>
    private void Update()
    {
        if (handleKeyboardInput == false)
        {
            return;
        }

        if (IsLocalInputOwner() == false)
        {
            return;
        }

        HandleInput();
    }

    /// <summary>
    /// 플레이어 루트 아래에서 아이템 처리 가능한 리시버들을 다시 찾는다.
    /// 플레이어 구조가 동적으로 바뀌는 경우 수동으로 다시 호출해도 된다.
    /// </summary>
    public void CacheReceivers()
    {
        cachedReceivers.Clear();

        if (playerRoot == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = playerRoot.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is SuicideSquad_IPlayerItemReceiver receiver)
            {
                if (cachedReceivers.Contains(receiver) == false)
                {
                    cachedReceivers.Add(receiver);
                }
            }
        }
    }

    /// <summary>
    /// 직접 키 입력을 처리한다.
    /// </summary>
    private void HandleInput()
    {
        if (itemInventory == null)
        {
            return;
        }

        if (Input.GetKeyDown(previousSlotKey))
        {
            itemInventory.MoveSelection(-1);
        }

        if (Input.GetKeyDown(nextSlotKey))
        {
            itemInventory.MoveSelection(1);
        }

        if (Input.GetKeyDown(useItemKey))
        {
            TryUseSelectedItem();
        }
    }

    /// <summary>
    /// 현재 선택된 아이템을 사용하려 시도한다.
    /// 아이템 적용에 성공했을 때만 인벤토리 수량을 1 감소시킨다.
    /// </summary>
    /// <returns>사용 성공 시 true.</returns>
    public bool TryUseSelectedItem()
    {
        // TODO (협업 - 플레이어 연결):
        // 현재는 ItemController 가 SuicideSquad_IPlayerItemReceiver 구현체 역할을 한다.
        // 추후 PlayerHealth, Shield, Buff 시스템이 추가되면
        // 그쪽도 같은 인터페이스를 구현해서 함께 확장 가능하다.

        if (IsLocalInputOwner() == false)
        {
            return false;
        }

        if (itemInventory == null)
        {
            Debug.LogWarning("[SuicideSquad_PlayerItemController] itemInventory 참조가 없습니다.");
            return false;
        }

        if (itemInventory.TryGetSelectedItem(out SuicideSquad_ItemData itemData, out int itemCount) == false)
        {
            return false;
        }

        if (itemData == null || itemCount <= 0)
        {
            return false;
        }

        bool applied = TrySendItemToReceivers(itemData);

        if (applied == false)
        {
            Debug.Log($"[SuicideSquad_PlayerItemController] {itemData.DisplayName} 을(를) 처리할 플레이어 리시버가 없습니다.");
            return false;
        }

        bool consumed = itemInventory.TryConsumeSelectedItem(1);

        if (consumed == false)
        {
            Debug.LogWarning("[SuicideSquad_PlayerItemController] 아이템 적용 후 수량 차감에 실패했습니다.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 선택된 아이템 정보를 외부에서 읽고 싶을 때 사용하는 헬퍼 함수.
    /// </summary>
    /// <param name="itemData">현재 선택된 아이템 데이터 반환값.</param>
    /// <param name="itemCount">현재 선택된 수량 반환값.</param>
    /// <returns>현재 선택 슬롯에 아이템이 있으면 true.</returns>
    public bool TryGetCurrentSelectedItem(out SuicideSquad_ItemData itemData, out int itemCount)
    {
        itemData = null;
        itemCount = 0;

        if (itemInventory == null)
        {
            return false;
        }

        return itemInventory.TryGetSelectedItem(out itemData, out itemCount);
    }

    /// <summary>
    /// 외부에서 퀵슬롯 입력을 막고 싶을 때 사용하는 함수.
    /// ESC UI나 대화창이 열릴 때 호출하면 된다.
    /// </summary>
    /// <param name="isEnabled">입력 허용 여부.</param>
    public void SetInputEnabled(bool isEnabled)
    {
        handleKeyboardInput = isEnabled;
    }

    /// <summary>
    /// 캐싱된 리시버들에게 아이템 사용 요청을 보낸다.
    /// </summary>
    /// <param name="itemData">사용할 아이템 데이터.</param>
    /// <returns>하나라도 성공적으로 처리하면 true.</returns>
    private bool TrySendItemToReceivers(SuicideSquad_ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (cachedReceivers.Count == 0)
        {
            CacheReceivers();
        }

        for (int i = 0; i < cachedReceivers.Count; i++)
        {
            SuicideSquad_IPlayerItemReceiver receiver = cachedReceivers[i];

            if (receiver == null)
            {
                continue;
            }

            if (receiver.CanUseItem(itemData) == false)
            {
                continue;
            }

            if (receiver.TryUseItem(itemData))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 멀티플레이 시 로컬 플레이어만 입력을 받을 수 있도록 검사한다.
    /// 오프라인에서는 항상 true를 반환한다.
    /// </summary>
    /// <returns>현재 오브젝트가 로컬 입력 소유자면 true.</returns>
    private bool IsLocalInputOwner()
    {
        if (blockRemoteInputInNetwork == false)
        {
            return true;
        }

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