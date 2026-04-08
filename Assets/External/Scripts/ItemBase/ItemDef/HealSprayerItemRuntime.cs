public sealed class HealSprayerItemRuntime : ItemRuntimeBase<HealSprayerItemDefinition>, IContinuousAimedItemRuntime
{
    private bool isContinuousUseActive;

    public HealSprayerItemRuntime(HealSprayerItemDefinition definition, ItemRuntimeContext context)
        : base(definition, context)
    {
    }

    public bool BeginContinuousUse(in ItemUseRequest request)
    {
        isContinuousUseActive = true;
        return true;
    }

    public void TickContinuousUse(in ItemUseRequest request, float deltaTime)
    {
    }

    public void EndContinuousUse()
    {
        isContinuousUseActive = false;
    }

    protected override bool UseInternal(in ItemUseRequest request)
    {
        // Continuous-use items are driven by IContinuousAimedItemRuntime.
        return false;
    }
}
