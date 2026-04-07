using System;
using Photon.Pun;
using UnityEngine;

public enum HealthChangeKind
{
    Damage = 0,
    Heal = 1,
    Set = 2,
    Sync = 3,
    MaxHealthChanged = 4
}

public struct HealthChangedEventArgs
{
    public HealthChangeKind ChangeKind;
    public float PreviousHealth;
    public float CurrentHealth;
    public float MaxHealth;
    public int SourceViewId;
    public bool WasDead;
    public bool IsDead;

    public float Delta => CurrentHealth - PreviousHealth;
    public float NormalizedHealth => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;

    public HealthChangedEventArgs(
        HealthChangeKind changeKind,
        float previousHealth,
        float currentHealth,
        float maxHealth,
        int sourceViewId,
        bool wasDead,
        bool isDead)
    {
        ChangeKind = changeKind;
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        SourceViewId = sourceViewId;
        WasDead = wasDead;
        IsDead = isDead;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public class PlayerHealth : PhotonOwnedBehaviour, IDamageable, IPunObservable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool startAtMaxHealth = true;
    [SerializeField, Min(0f)] private float initialHealth = 100f;

    [Header("Options")]
    [SerializeField] private bool invulnerable = false;
    [SerializeField] private bool allowHealingWhenDead = false;

    public static event Action<PlayerHealth> Registered;
    public static event Action<PlayerHealth> Unregistered;

    public event Action<PlayerHealth, HealthChangedEventArgs> HealthChanged;
    public event Action<PlayerHealth, HealthChangedEventArgs> Damaged;
    public event Action<PlayerHealth, HealthChangedEventArgs> Healed;
    public event Action<PlayerHealth, HealthChangedEventArgs> Died;
    public event Action<PlayerHealth, HealthChangedEventArgs> Revived;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;

    public bool IsDead => currentHealth <= 0f;
    public bool IsAlive => !IsDead;
    public bool IsLocallyOwned => HasLocalAuthority;
    public bool CanReceiveDamage => !IsDead && !invulnerable;

    public int OwnerActorNumber
    {
        get
        {
            if (CachedPhotonView != null && CachedPhotonView.Owner != null)
                return CachedPhotonView.Owner.ActorNumber;

            return -1;
        }
    }

    private float currentHealth;

    protected override void Awake()
    {
        base.Awake();

        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = startAtMaxHealth
            ? maxHealth
            : Mathf.Clamp(initialHealth, 0f, maxHealth);
    }

    private void OnEnable()
    {
        Registered?.Invoke(this);
    }

    private void OnDisable()
    {
        Unregistered?.Invoke(this);
    }

    public bool RequestDamage(float amount, int sourceViewId = -1)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return false;

        if (ShouldForwardRequestToOwner())
        {
            CachedPhotonView.RPC(nameof(RPC_RequestDamage), CachedPhotonView.Owner, amount, sourceViewId);
            return true;
        }

        return ApplyDamageInternal(amount, sourceViewId);
    }

    public bool RequestHeal(float amount, int sourceViewId = -1)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return false;

        if (ShouldForwardRequestToOwner())
        {
            CachedPhotonView.RPC(nameof(RPC_RequestHeal), CachedPhotonView.Owner, amount, sourceViewId);
            return true;
        }

        return ApplyHealInternal(amount, sourceViewId);
    }

    public bool SetHealthLocal(float newHealth, int sourceViewId = -1)
    {
        if (!CanMutateStateLocally())
            return false;

        return SetHealthInternal(newHealth, HealthChangeKind.Set, sourceViewId);
    }

    public bool SetMaxHealthLocal(float newMaxHealth, bool keepRatio = true, int sourceViewId = -1)
    {
        if (!CanMutateStateLocally())
            return false;

        newMaxHealth = Mathf.Max(1f, newMaxHealth);

        float previousHealth = currentHealth;
        float previousMaxHealth = maxHealth;
        bool wasDead = IsDead;

        float normalized = previousMaxHealth > 0f ? previousHealth / previousMaxHealth : 1f;

        maxHealth = newMaxHealth;
        currentHealth = keepRatio
            ? Mathf.Clamp(maxHealth * normalized, 0f, maxHealth)
            : Mathf.Clamp(previousHealth, 0f, maxHealth);

        RaiseHealthEvents(new HealthChangedEventArgs(
            HealthChangeKind.MaxHealthChanged,
            previousHealth,
            currentHealth,
            maxHealth,
            sourceViewId,
            wasDead,
            IsDead));

        return true;
    }

    public bool RestoreFullHealthLocal(int sourceViewId = -1)
    {
        return SetHealthLocal(maxHealth, sourceViewId);
    }

    public bool KillLocal(int sourceViewId = -1)
    {
        return SetHealthLocal(0f, sourceViewId);
    }

    public void SetInvulnerableLocal(bool value)
    {
        if (!CanMutateStateLocally())
            return;

        invulnerable = value;
    }

    private bool ApplyDamageInternal(float amount, int sourceViewId)
    {
        if (!CanReceiveDamage)
            return false;

        return SetHealthInternal(currentHealth - amount, HealthChangeKind.Damage, sourceViewId);
    }

    private bool ApplyHealInternal(float amount, int sourceViewId)
    {
        if (IsDead && !allowHealingWhenDead)
            return false;

        return SetHealthInternal(currentHealth + amount, HealthChangeKind.Heal, sourceViewId);
    }

    private bool SetHealthInternal(float targetHealth, HealthChangeKind changeKind, int sourceViewId)
    {
        float clampedHealth = Mathf.Clamp(targetHealth, 0f, maxHealth);
        float previousHealth = currentHealth;
        bool wasDead = IsDead;

        if (Mathf.Approximately(previousHealth, clampedHealth))
            return false;

        currentHealth = clampedHealth;

        RaiseHealthEvents(new HealthChangedEventArgs(
            changeKind,
            previousHealth,
            currentHealth,
            maxHealth,
            sourceViewId,
            wasDead,
            IsDead));

        return true;
    }

    private void ApplySynchronizedState(float syncedCurrentHealth, float syncedMaxHealth)
    {
        syncedMaxHealth = Mathf.Max(1f, syncedMaxHealth);
        syncedCurrentHealth = Mathf.Clamp(syncedCurrentHealth, 0f, syncedMaxHealth);

        bool maxChanged = !Mathf.Approximately(maxHealth, syncedMaxHealth);
        bool healthChanged = !Mathf.Approximately(currentHealth, syncedCurrentHealth);

        if (!maxChanged && !healthChanged)
            return;

        float previousHealth = currentHealth;
        bool wasDead = IsDead;

        maxHealth = syncedMaxHealth;
        currentHealth = syncedCurrentHealth;

        RaiseHealthEvents(new HealthChangedEventArgs(
            HealthChangeKind.Sync,
            previousHealth,
            currentHealth,
            maxHealth,
            -1,
            wasDead,
            IsDead));
    }

    private void RaiseHealthEvents(HealthChangedEventArgs args)
    {
        HealthChanged?.Invoke(this, args);

        switch (args.ChangeKind)
        {
            case HealthChangeKind.Damage:
                Damaged?.Invoke(this, args);
                break;

            case HealthChangeKind.Heal:
                Healed?.Invoke(this, args);
                break;
        }

        if (!args.WasDead && args.IsDead)
        {
            Died?.Invoke(this, args);
        }
        else if (args.WasDead && !args.IsDead)
        {
            Revived?.Invoke(this, args);
        }
    }

    private bool ShouldForwardRequestToOwner()
    {
        return PhotonNetwork.InRoom &&
               CachedPhotonView != null &&
               CachedPhotonView.Owner != null &&
               !HasLocalAuthority;
    }

    private bool CanMutateStateLocally()
    {
        if (!PhotonNetwork.InRoom)
            return true;

        return HasLocalAuthority;
    }

    [PunRPC]
    private void RPC_RequestDamage(float amount, int sourceViewId)
    {
        if (PhotonNetwork.InRoom && !HasLocalAuthority)
            return;

        ApplyDamageInternal(amount, sourceViewId);
    }

    [PunRPC]
    private void RPC_RequestHeal(float amount, int sourceViewId)
    {
        if (PhotonNetwork.InRoom && !HasLocalAuthority)
            return;

        ApplyHealInternal(amount, sourceViewId);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentHealth);
            stream.SendNext(maxHealth);
        }
        else
        {
            float syncedCurrentHealth = (float)stream.ReceiveNext();
            float syncedMaxHealth = (float)stream.ReceiveNext();

            ApplySynchronizedState(syncedCurrentHealth, syncedMaxHealth);
        }
    }
}