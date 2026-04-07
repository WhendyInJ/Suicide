using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DroppedItemPickup : MonoBehaviour
{
    [SerializeField] private ItemType itemType = ItemType.None;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private bool rotateWhileDropped = true;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 120f, 0f);

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        if (!rotateWhileDropped)
            return;

        transform.Rotate(rotationSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (itemType == ItemType.None)
            return;

        ItemController itemController = other.GetComponent<ItemController>();
        if (itemController == null)
            itemController = other.GetComponentInParent<ItemController>();

        if (itemController == null)
            return;

        itemController.EquipItem(itemType);

        if (destroyOnPickup)
            Destroy(gameObject);
    }
}
