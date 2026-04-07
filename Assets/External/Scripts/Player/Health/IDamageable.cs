public interface IDamageable
{
    bool CanReceiveDamage { get; }
    float CurrentHealth { get; }
    float MaxHealth { get; }

    bool RequestDamage(float amount, int sourceViewId = -1);
    bool RequestHeal(float amount, int sourceViewId = -1);
}