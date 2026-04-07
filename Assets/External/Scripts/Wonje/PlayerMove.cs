using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private ItemController playerItemController;
    [SerializeField] private ItemType startingItem = ItemType.None;

    private void Start()
    {
        if (playerItemController != null && startingItem != ItemType.None)
            playerItemController.EquipItem(startingItem);
    }

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 moveDir = new Vector3(x, 0f, z).normalized;
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }
}
