public sealed class HealSprayerItemRuntime : ItemRuntimeBase<HealSprayerItemDefinition>
{
    public HealSprayerItemRuntime(HealSprayerItemDefinition definition, ItemRuntimeContext context)
        : base(definition, context)
    {
    }

    protected override bool UseInternal(in ItemUseRequest request)
    {
        // Actual heal spray behavior will be added later.
        return false;
    }
}
