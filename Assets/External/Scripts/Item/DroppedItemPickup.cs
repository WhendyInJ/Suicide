using Photon.Pun;
using UnityEngine;

/// <summary>
/// 월드에 떨어진 3D 아이템 픽업.
/// 
/// 파일명과 클래스명은 기존 원본을 유지한다.
/// 
/// 우선순위:
/// 1. itemData 가 있으면 새 인벤토리 시스템(SuicideSquad_ItemInventory)에 지급
/// 2. itemData 가 없고 allowLegacyEquipFallback 이 켜져 있으면
///    기존 ItemController.EquipItem(itemType) 흐름으로 fallback
/// </summary>
[RequireComponent(typeof(Collider))]
public class DroppedItemPickup : MonoBehaviour
{
    [Header("새 인벤토리 지급 방식")]

    /// <summary>
    /// 새 퀵슬롯/인벤토리 시스템으로 지급할 아이템 데이터.
    /// 이 값이 설정되어 있으면 itemType보다 우선한다.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemData itemData = null;

    /// <summary>
    /// 새 인벤토리 시스템으로 지급할 수량.
    /// </summary>
    [SerializeField, Min(1)] private int giveCount = 1;

    [Header("기존 원본 방식 호환")]

    /// <summary>
    /// 기존 원본 아이템 타입.
    /// 예전 프리팹 호환용으로 남겨둔다.
    /// </summary>
    [SerializeField] private ItemType itemType = ItemType.None;

    /// <summary>
    /// itemData 가 없을 때 기존 EquipItem 방식 fallback을 허용할지 여부.
    /// </summary>
    [SerializeField] private bool allowLegacyEquipFallback = false;

    [Header("공통 옵션")]

    /// <summary>
    /// 픽업 성공 시 오브젝트를 제거할지 여부.
    /// 새 인벤토리 방식에서는 남은 giveCount가 0 이하일 때 제거된다.
    /// </summary>
    [SerializeField] private bool destroyOnPickup = true;

    /// <summary>
    /// 바닥 아이템 회전 연출 사용 여부.
    /// </summary>
    [SerializeField] private bool rotateWhileDropped = true;

    /// <summary>
    /// 회전 속도.
    /// </summary>
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 120f, 0f);

    /// <summary>
    /// 플레이어 태그 이름.
    /// 비어 있지 않으면 이 태그를 가진 오브젝트만 픽업할 수 있다.
    /// </summary>
    [SerializeField] private string playerTag = "Player";

    /// <summary>
    /// 부모/자식에서도 필요한 컴포넌트를 찾을지 여부.
    /// 플레이어 루트와 충돌체가 분리된 구조를 대비한다.
    /// </summary>
    [SerializeField] private bool searchParentsAndChildren = true;

    /// <summary>
    /// Collider를 Trigger로 자동 설정할지 여부.
    /// </summary>
    [SerializeField] private bool setTriggerOnReset = true;

    /// <summary>
    /// 처음 붙일 때 Collider를 Trigger로 맞춘다.
    /// </summary>
    private void Reset()
    {
        if (setTriggerOnReset == false)
        {
            return;
        }

        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    /// <summary>
    /// 떨어진 아이템 회전 연출을 처리한다.
    /// </summary>
    private void Update()
    {
        if (!rotateWhileDropped)
        {
            return;
        }

        transform.Rotate(rotationSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// 3D 트리거 진입 시
    /// 우선 새 인벤토리 지급을 시도하고,
    /// 필요하면 기존 EquipItem 방식으로 fallback 한다.
    /// </summary>
    /// <param name="other">충돌한 다른 3D Collider.</param>
    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(playerTag) == false && other.CompareTag(playerTag) == false)
        {
            return;
        }

        if (IsLocalOwnerCollider(other) == false)
        {
            return;
        }

        bool pickupSucceeded = false;

        if (itemData != null)
        {
            pickupSucceeded = TryGiveInventoryItem(other);
        }
        else if (allowLegacyEquipFallback)
        {
            pickupSucceeded = TryEquipLegacyItem(other);
        }

        if (pickupSucceeded == false)
        {
            return;
        }

        if (destroyOnPickup && giveCount <= 0)
        {
            Destroy(gameObject);
        }
        else if (destroyOnPickup && itemData == null)
        {
            // legacy fallback은 수량 개념이 없으므로 한 번 성공하면 바로 제거
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 새 인벤토리 시스템으로 아이템을 지급한다.
    /// </summary>
    /// <param name="other">충돌한 Collider.</param>
    /// <returns>1개 이상 정상 지급되면 true.</returns>
    private bool TryGiveInventoryItem(Collider other)
    {
        if (itemData == null)
        {
            return false;
        }

        SuicideSquad_ItemInventory inventory = FindInventory(other);

        if (inventory == null)
        {
            Debug.LogWarning("[DroppedItemPickup] SuicideSquad_ItemInventory 를 찾지 못했습니다.");
            return false;
        }

        int addedCount = inventory.AddItem(itemData, giveCount);

        if (addedCount <= 0)
        {
            return false;
        }

        giveCount -= addedCount;
        return true;
    }

    /// <summary>
    /// 기존 원본 EquipItem 방식으로 fallback 한다.
    /// </summary>
    /// <param name="other">충돌한 Collider.</param>
    /// <returns>성공 시 true.</returns>
    private bool TryEquipLegacyItem(Collider other)
    {
        if (itemType == ItemType.None)
        {
            return false;
        }

        ItemController itemController = FindItemController(other);

        if (itemController == null)
        {
            return false;
        }

        itemController.EquipItem(itemType);
        return true;
    }

    /// <summary>
    /// 충돌한 Collider 기준으로 새 인벤토리를 찾는다.
    /// </summary>
    /// <param name="other">충돌한 Collider.</param>
    /// <returns>찾은 인벤토리. 없으면 null.</returns>
    private SuicideSquad_ItemInventory FindInventory(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        SuicideSquad_ItemInventory inventory = other.GetComponent<SuicideSquad_ItemInventory>();

        if (inventory != null)
        {
            return inventory;
        }

        if (searchParentsAndChildren)
        {
            inventory = other.GetComponentInParent<SuicideSquad_ItemInventory>();

            if (inventory != null)
            {
                return inventory;
            }

            inventory = other.GetComponentInChildren<SuicideSquad_ItemInventory>();

            if (inventory != null)
            {
                return inventory;
            }
        }

        return null;
    }

    /// <summary>
    /// 충돌한 Collider 기준으로 ItemController를 찾는다.
    /// </summary>
    /// <param name="other">충돌한 Collider.</param>
    /// <returns>찾은 ItemController. 없으면 null.</returns>
    private ItemController FindItemController(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        ItemController itemController = other.GetComponent<ItemController>();

        if (itemController != null)
        {
            return itemController;
        }

        if (searchParentsAndChildren)
        {
            itemController = other.GetComponentInParent<ItemController>();

            if (itemController != null)
            {
                return itemController;
            }

            itemController = other.GetComponentInChildren<ItemController>();

            if (itemController != null)
            {
                return itemController;
            }
        }

        return null;
    }

    /// <summary>
    /// 멀티플레이에서 로컬 소유 플레이어만 픽업 처리하도록 검사한다.
    /// 오프라인에서는 항상 true.
    /// </summary>
    /// <param name="other">충돌한 Collider.</param>
    /// <returns>로컬 플레이어 소유 충돌체면 true.</returns>
    private bool IsLocalOwnerCollider(Collider other)
    {
        if (!PhotonNetwork.IsConnected)
        {
            return true;
        }

        PhotonView photonView = other.GetComponent<PhotonView>();

        if (photonView == null)
        {
            photonView = other.GetComponentInParent<PhotonView>();
        }

        if (photonView == null)
        {
            return false;
        }

        return photonView.IsMine;
    }
}