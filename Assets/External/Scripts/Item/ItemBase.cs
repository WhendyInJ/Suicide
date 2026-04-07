using UnityEngine;

public enum ItemType
{
    None = 0,
    NautilusGrab = 1,
    StructureShockwave = 2
}

public abstract class ItemBase : MonoBehaviour
{
    public abstract void Activate(GameObject user);
}