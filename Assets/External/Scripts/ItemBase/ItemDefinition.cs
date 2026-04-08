using UnityEngine;

public abstract class ItemDefinition : ScriptableObject
{
    [Header("Common")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(1)] private int maxStack = 1;
    [SerializeField, Min(0f)] private float useCooldown = 0f;

    [Header("Use")]
    [SerializeField] private ItemUseMode useMode = ItemUseMode.Instant;
    [SerializeField] private ItemAimSettings aimSettings;

    [Header("Held Visual")]
    [SerializeField] private HeldItemVisualData heldVisual;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public int MaxStack => Mathf.Max(1, maxStack);
    public float UseCooldown => useCooldown;

    public ItemUseMode UseMode => useMode;
    public ItemAimSettings AimSettings => aimSettings;
    public HeldItemVisualData HeldVisual => heldVisual;

    public abstract IItemRuntime CreateRuntime(ItemRuntimeContext context);
}