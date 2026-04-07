using System;
using System.Collections.Generic;
using UnityEngine;

public enum TrapFloorMode
{
    Damage = 0,
    Heal = 1
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class TrapFloorZone : MonoBehaviour
{
    private sealed class OccupantState
    {
        public int OverlapCount;
        public float NextTickTime;
    }

    [Header("Mode")]
    [SerializeField] private TrapFloorMode initialMode = TrapFloorMode.Damage;
    [SerializeField] private bool applyFirstTickImmediatelyOnEnter = true;
    [SerializeField] private bool applyFirstTickImmediatelyOnModeChange = false;

    [Header("Damage Mode")]
    [SerializeField, Min(0f)] private float damagePerTick = 10f;
    [SerializeField, Min(0.01f)] private float damageTickInterval = 1f;

    [Header("Heal Mode")]
    [SerializeField, Min(0f)] private float healPerTick = 10f;
    [SerializeField, Min(0.01f)] private float healTickInterval = 1f;

    [Header("Multiplayer")]
    [SerializeField] private bool affectOnlyLocallyOwnedPlayers = true;

    [Header("References")]
    [SerializeField] private TrapFloorVisualController visualController;

    public event Action<TrapFloorZone, TrapFloorMode> ModeChanged;

    public TrapFloorMode CurrentMode { get; private set; }
    public float CurrentTickAmount => CurrentMode == TrapFloorMode.Damage ? damagePerTick : healPerTick;
    public float CurrentTickInterval => CurrentMode == TrapFloorMode.Damage ? damageTickInterval : healTickInterval;

    private readonly Dictionary<int, PlayerHealth> colliderToHealth = new();
    private readonly Dictionary<PlayerHealth, OccupantState> occupants = new();
    private readonly List<PlayerHealth> removalBuffer = new();

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        visualController = GetComponentInChildren<TrapFloorVisualController>();
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning($"{name} 의 TrapFloorZone은 Trigger Collider가 필요합니다.", this);
        }

        CurrentMode = initialMode;
        RefreshVisuals(true);
    }

    private void OnValidate()
    {
        damagePerTick = Mathf.Max(0f, damagePerTick);
        healPerTick = Mathf.Max(0f, healPerTick);
        damageTickInterval = Mathf.Max(0.01f, damageTickInterval);
        healTickInterval = Mathf.Max(0.01f, healTickInterval);

        if (!Application.isPlaying)
        {
            CurrentMode = initialMode;
            RefreshVisuals(false);
        }
    }

    private void OnDisable()
    {
        colliderToHealth.Clear();
        occupants.Clear();
        removalBuffer.Clear();
    }

    private void Update()
    {
        if (occupants.Count == 0)
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

            if (now < state.NextTickTime)
                continue;

            ApplyCurrentTick(health);
            state.NextTickTime = now + CurrentTickInterval;
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            occupants.Remove(removalBuffer[i]);
        }
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

    public void SetMode(TrapFloorMode newMode)
    {
        if (CurrentMode == newMode)
            return;

        CurrentMode = newMode;
        RefreshVisuals(true);
        RescheduleOccupants(applyFirstTickImmediatelyOnModeChange);
        ModeChanged?.Invoke(this, CurrentMode);
    }

    public void ToggleMode()
    {
        SetMode(CurrentMode == TrapFloorMode.Damage
            ? TrapFloorMode.Heal
            : TrapFloorMode.Damage);
    }

    private void TryRegisterCollider(Collider other)
    {
        if (other == null)
            return;

        int colliderId = other.GetInstanceID();
        if (colliderToHealth.ContainsKey(colliderId))
            return;

        if (!TryResolveHealth(other, out PlayerHealth health))
            return;

        if (!CanTrackHealth(health))
            return;

        colliderToHealth.Add(colliderId, health);

        if (occupants.TryGetValue(health, out OccupantState existingState))
        {
            existingState.OverlapCount++;
            return;
        }

        OccupantState newState = new OccupantState
        {
            OverlapCount = 1,
            NextTickTime = Time.time + CurrentTickInterval
        };

        occupants.Add(health, newState);

        if (applyFirstTickImmediatelyOnEnter)
        {
            ApplyCurrentTick(health);
            newState.NextTickTime = Time.time + CurrentTickInterval;
        }
    }

    private void TryUnregisterCollider(Collider other)
    {
        if (other == null)
            return;

        int colliderId = other.GetInstanceID();
        if (!colliderToHealth.TryGetValue(colliderId, out PlayerHealth health))
            return;

        colliderToHealth.Remove(colliderId);

        if (health == null)
            return;

        if (!occupants.TryGetValue(health, out OccupantState state))
            return;

        state.OverlapCount--;

        if (state.OverlapCount <= 0)
        {
            occupants.Remove(health);
        }
    }

    private bool TryResolveHealth(Collider other, out PlayerHealth health)
    {
        health = other.GetComponentInParent<PlayerHealth>();
        return health != null;
    }

    private bool CanTrackHealth(PlayerHealth health)
    {
        if (health == null)
            return false;

        if (!affectOnlyLocallyOwnedPlayers)
            return true;

        return health.IsLocallyOwned;
    }

    private void ApplyCurrentTick(PlayerHealth health)
    {
        if (health == null)
            return;

        switch (CurrentMode)
        {
            case TrapFloorMode.Damage:
                if (damagePerTick > 0f)
                    health.RequestDamage(damagePerTick);
                break;

            case TrapFloorMode.Heal:
                if (healPerTick > 0f)
                    health.RequestHeal(healPerTick);
                break;
        }
    }

    private void RescheduleOccupants(bool applyImmediateTick)
    {
        if (occupants.Count == 0)
            return;

        float now = Time.time;

        foreach (KeyValuePair<PlayerHealth, OccupantState> pair in occupants)
        {
            PlayerHealth health = pair.Key;
            OccupantState state = pair.Value;

            if (health == null || state == null)
                continue;

            if (applyImmediateTick)
                ApplyCurrentTick(health);

            state.NextTickTime = now + CurrentTickInterval;
        }
    }

    private void RefreshVisuals(bool includeParticles)
    {
        if (visualController == null)
            return;

        visualController.ApplyMode(CurrentMode, includeParticles);
    }
}