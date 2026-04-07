using UnityEngine;

/// <summary>
/// 월드에 배치된 3D 아이템 픽업 오브젝트.
/// 플레이어가 3D 트리거에 들어오면 인벤토리에 아이템을 추가한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SuicideSquad_ItemPickup : MonoBehaviour
{
    [Header("지급 아이템 정보")]

    /// <summary>
    /// 플레이어에게 줄 아이템 데이터.
    /// </summary>
    [SerializeField] private SuicideSquad_ItemData itemData = null;

    /// <summary>
    /// 플레이어에게 줄 수량.
    /// </summary>
    [SerializeField, Min(1)] private int giveCount = 1;

    [Header("감지 설정")]

    /// <summary>
    /// 플레이어 태그 이름.
    /// 비어 있지 않으면 이 태그를 가진 오브젝트만 픽업 가능하다.
    /// </summary>
    [SerializeField] private string playerTag = "Player";

    /// <summary>
    /// 픽업 후 남은 수량이 0 이하가 되면 오브젝트를 제거할지 여부.
    /// </summary>
    [SerializeField] private bool destroyWhenEmpty = true;

    /// <summary>
    /// 충돌한 오브젝트의 부모/자식에서도 인벤토리를 찾을지 여부.
    /// 플레이어 루트와 충돌체가 분리된 구조를 대비한 옵션이다.
    /// </summary>
    [SerializeField] private bool searchParentsAndChildren = true;

    /// <summary>
    /// Reset 시 Collider를 자동으로 Trigger로 맞출지 여부.
    /// </summary>
    [SerializeField] private bool setTriggerOnReset = true;

    /// <summary>
    /// 에디터에서 컴포넌트를 처음 붙였을 때 Collider를 Trigger로 맞춘다.
    /// 3D 픽업 오브젝트 기본 세팅을 빠르게 하기 위한 처리다.
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
    /// 3D 트리거 진입 시 플레이어 인벤토리를 찾아 아이템을 지급한다.
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

        if (itemData == null)
        {
            Debug.LogWarning("[SuicideSquad_ItemPickup] itemData가 비어 있습니다.");
            return;
        }

        SuicideSquad_ItemInventory inventory = FindInventory(other);

        if (inventory == null)
        {
            Debug.LogWarning("[SuicideSquad_ItemPickup] 플레이어 인벤토리를 찾지 못했습니다.");
            return;
        }

        int addedCount = inventory.AddItem(itemData, giveCount);

        if (addedCount <= 0)
        {
            return;
        }

        giveCount -= addedCount;

        if (giveCount <= 0 && destroyWhenEmpty)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 충돌한 3D Collider 기준으로 인벤토리를 찾는다.
    /// 플레이어 루트, 자식 충돌체 구조를 모두 어느 정도 대응할 수 있게 작성했다.
    /// </summary>
    /// <param name="other">충돌한 3D Collider.</param>
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
}