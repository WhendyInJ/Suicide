using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class HealSprayerHealZone : MonoBehaviour
{
    private sealed class OccupantState
    {
        public int OverlapCount;
        public float NextHealTime;
    }

    [SerializeField] private PlayerItemRunner ownerRunner;

    private Collider volumeCollider;

    private readonly Dictionary<int, PlayerHealth> colliderToHealth = new();
    private readonly Dictionary<PlayerHealth, OccupantState> occupants = new();
    private readonly List<PlayerHealth> removalBuffer = new();

    private PlayerHealth ownerHealth;
    private float healPerTick = 1f;
    private float healTickInterval = 0.2f;
    private LayerMask targetMask = ~0;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        volumeCollider = GetComponent<Collider>();
        if (volumeCollider != null && !volumeCollider.isTrigger)
            Debug.LogWarning($"{name} 의 HealSprayerHealZone은 Trigger Collider가 필요합니다.", this);
    }

    private void OnDisable()
    {
        ClearTracking();
    }

    /// <summary>
    /// Enables or disables the heal volume (trigger collider). When off, overlap/heal state is cleared.
    /// Use while aiming + primary fire from <see cref="HealSprayerHeldVisual"/>.
    /// </summary>
    public void SetSprayVolumeActive(bool active)
    {
        if (volumeCollider != null)
            volumeCollider.enabled = active;

        if (!active)
            ClearTracking();
    }

    private void ClearTracking()
    {
        colliderToHealth.Clear();
        occupants.Clear();
        removalBuffer.Clear();
    }

    public void Configure(PlayerItemRunner runner, HealSprayerItemDefinition definition)
    {
        ownerRunner = runner;
        ownerHealth = ownerRunner != null ? ownerRunner.GetComponentInParent<PlayerHealth>() : null;

        if (definition == null)
            return;

        healPerTick = definition.HealPerTick;
        healTickInterval = definition.HealTickInterval;
        targetMask = definition.TargetMask;
    }

    private void Update()
    {
        if (volumeCollider != null && !volumeCollider.enabled)
            return;

        if (occupants.Count == 0 || healPerTick <= 0f)
            return;

        float now = Time.time;
        removalBuffer.Clear();

        foreach (KeyValuePair<PlayerHealth, OccupantState> pair in occupants)
        {
            PlayerHealth health = pair.Key;
            OccupantState state = pair.Value;

            if (health == null || state == null || state.OverlapCount <= 0)
            {
                removalBuffer.Add(health);
                continue;
            }

            if (now < state.NextHealTime)
                continue;

            health.RequestHeal(healPerTick);
            state.NextHealTime = now + healTickInterval;
        }

        for (int i = 0; i < removalBuffer.Count; i++)
            occupants.Remove(removalBuffer[i]);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryRegisterCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        TryUnregisterCollider(other);
    }

    private void TryRegisterCollider(Collider other)
    {
        if (other == null || (volumeCollider != null && !volumeCollider.enabled))
            return;

        int colliderId = other.GetInstanceID();
        if (colliderToHealth.ContainsKey(colliderId))
            return;

        if (!TryResolveHealth(other, out PlayerHealth health))
            return;

        if (!CanHealTarget(health, other.gameObject.layer))
            return;

        colliderToHealth.Add(colliderId, health);

        if (occupants.TryGetValue(health, out OccupantState existingState))
        {
            existingState.OverlapCount++;
            return;
        }

        occupants.Add(health, new OccupantState
        {
            OverlapCount = 1,
            NextHealTime = Time.time + healTickInterval
        });
    }

    private void TryUnregisterCollider(Collider other)
    {
        if (other == null)
            return;

        int colliderId = other.GetInstanceID();
        if (!colliderToHealth.TryGetValue(colliderId, out PlayerHealth health))
            return;

        colliderToHealth.Remove(colliderId);

        if (health == null || !occupants.TryGetValue(health, out OccupantState state))
            return;

        state.OverlapCount--;
        if (state.OverlapCount <= 0)
            occupants.Remove(health);
    }

    private static bool TryResolveHealth(Collider other, out PlayerHealth health)
    {
        health = other.GetComponentInParent<PlayerHealth>();
        return health != null;
    }

    private bool CanHealTarget(PlayerHealth health, int colliderLayer)
    {
        if (health == null || ownerRunner == null || !ownerRunner.IsLocallyOwned)
            return false;

        if (ownerHealth != null && health == ownerHealth)
            return false;

        return (targetMask.value & (1 << colliderLayer)) != 0;
    }
}
