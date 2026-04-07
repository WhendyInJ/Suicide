using UnityEngine;

public class ItemController : MonoBehaviour
{
    [SerializeField] private KeyCode useKey = KeyCode.Q;
    [SerializeField] private ItemType currentItemType = ItemType.None;

    public ItemType CurrentItemType => currentItemType;

    private void Update()
    {
        if (!Input.GetKeyDown(useKey))
            return;

        UseCurrentItem();
    }

    public void EquipItem(ItemType newItemType)
    {
        currentItemType = newItemType;

        switch (currentItemType)
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

    private void UseCurrentItem()
    {
        switch (currentItemType)
        {
            case ItemType.NautilusGrab:
                NautilusGrabAbility grab = GetComponent<NautilusGrabAbility>();
                if (grab != null && grab.isActiveAndEnabled)
                    grab.StartCoroutine(grab.Execute());
                break;

            case ItemType.StructureShockwave:
                ShockwaveAbility shockwave = GetComponent<ShockwaveAbility>();
                if (shockwave != null && shockwave.isActiveAndEnabled)
                    shockwave.Execute();
                break;
        }
    }

    private T EnsureAbility<T>(bool shouldEnable) where T : Behaviour
    {
        T ability = GetComponent<T>();

        if (ability == null && shouldEnable)
            ability = gameObject.AddComponent<T>();

        if (ability != null)
            ability.enabled = shouldEnable;

        return ability;
    }
}