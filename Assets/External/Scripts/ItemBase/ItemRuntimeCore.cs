public interface IItemRuntime
{
    ItemDefinition Definition { get; }
    ItemUseMode UseMode { get; }
    ItemAimSettings AimSettings { get; }

    void Tick(float deltaTime);
    bool CanUse();
    bool TryUse(in ItemUseRequest request);
}

public abstract class ItemRuntimeBase<TDefinition> : IItemRuntime
    where TDefinition : ItemDefinition
{
    protected TDefinition TypedDefinition { get; }
    protected ItemRuntimeContext Context { get; }

    private float cooldownRemaining;

    public ItemDefinition Definition => TypedDefinition;
    public ItemUseMode UseMode => TypedDefinition.UseMode;
    public ItemAimSettings AimSettings => TypedDefinition.AimSettings;

    protected ItemRuntimeBase(TDefinition definition, ItemRuntimeContext context)
    {
        TypedDefinition = definition;
        Context = context;
    }

    public void Tick(float deltaTime)
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining -= deltaTime;
            if (cooldownRemaining < 0f)
                cooldownRemaining = 0f;
        }

        OnTick(deltaTime);
    }

    public bool CanUse()
    {
        if (cooldownRemaining > 0f)
            return false;

        return CanUseInternal();
    }

    public bool TryUse(in ItemUseRequest request)
    {
        if (!CanUse())
            return false;

        if (!UseInternal(request))
            return false;

        cooldownRemaining = TypedDefinition.UseCooldown;
        return true;
    }

    protected virtual void OnTick(float deltaTime)
    {
    }

    protected virtual bool CanUseInternal()
    {
        return true;
    }

    protected abstract bool UseInternal(in ItemUseRequest request);
}