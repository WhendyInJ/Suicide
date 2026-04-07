using UnityEngine;

/// <summary>
/// 월드에 배치된 아이템 픽업 오브젝트.
/// 플레이어가 닿으면 인벤토리에 아이템을 추가한다.
/// </summary>
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
    /// </summary>
    [SerializeField] private string playerTag = "Player";

    /// <summary>
    /// 픽업 후 수량이 0이 되면 오브젝트를 제거할지 여부.
    /// </summary>
    [SerializeField] private bool destroyWhenEmpty = true;

    /// <summary>
    /// 부모/자식에서 인벤토리를 같이 찾을지 여부.
    /// </summary>
    [SerializeField] private bool searchParentsAndChildren = true;

    /// <summary>
    /// 트리거 진입 시 플레이어 인벤토리를 찾아 아이템을 지급한다.
    /// </summary>
    /// <param name="other">충돌한 다른 Collider2D.</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
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
    /// 충돌한 오브젝트 기준으로 인벤토리를 찾는다.
    /// </summary>
    /// <param name="other">충돌한 Collider2D.</param>
    /// <returns>찾은 인벤토리. 없으면 null.</returns>
    private SuicideSquad_ItemInventory FindInventory(Collider2D other)
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