using UnityEngine;

public interface ISkillRuntime
{
    SkillDefinition Definition { get; }
    void Tick(float deltaTime);
    bool TryActivate();
}

public abstract class SkillRuntimeBase<TDefinition> : ISkillRuntime
    where TDefinition : SkillDefinition
{
    protected TDefinition TypedDefinition { get; }
    protected SkillRuntimeContext Context { get; }

    private float cooldownRemaining;

    public SkillDefinition Definition => TypedDefinition;

    protected SkillRuntimeBase(TDefinition definition, SkillRuntimeContext context)
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

    public bool TryActivate()
    {
        if (cooldownRemaining > 0f)
            return false;

        if (!CanActivate())
            return false;

        if (!Activate())
            return false;

        cooldownRemaining = TypedDefinition.Cooldown;
        return true;
    }

    protected virtual void OnTick(float deltaTime)
    {
    }

    protected virtual bool CanActivate()
    {
        return true;
    }

    protected abstract bool Activate();
}