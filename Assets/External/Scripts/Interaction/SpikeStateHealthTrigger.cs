using System.Collections.Generic;
using UnityEngine;

public enum SpikeTriggerEffectMode
{
    DamageWhenDamageState = 0,
    HealWhenHealState = 1
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SpikeStateHealthTrigger : MonoBehaviour
{
    private sealed class OccupantState
    {
        public int OverlapCount;
        public float NextTickTime;
    }

    [Header("Mode Source")]
    [SerializeField] private SpikePlatformGroupController groupController;
    [SerializeField] private SpikeFloorGroupSide groupSide = SpikeFloorGroupSide.First;
    [SerializeField] private SpikeTriggerEffectMode effectMode = SpikeTriggerEffectMode.DamageWhenDamageState;

    [Header("Effect")]
    [SerializeField] private bool applyFirstTickImmediatelyOnEnter = true;
    [SerializeField] private bool applyFirstTickImmediatelyOnModeChange = false;
    [SerializeField, Min(0f)] private float amountPerTick = 1f;
    [SerializeField, Min(0.01f)] private float tickInterval = 0.2f;

    [Header("Options")]
    [SerializeField] private bool affectOnlyLocallyOwnedPlayers = true;
    [SerializeField] private bool enableDebugLogs = true;

    private readonly Dictionary<int, PlayerHealth> colliderToHealth = new();
    private readonly Dictionary<PlayerHealth, OccupantState> occupants = new();
    private readonly List<PlayerHealth> removalBuffer = new();

    private bool lastEffectEnabled;

    private bool IsEffectEnabled
    {
        get
        {
            if (groupController == null)
                return false;

            return effectMode == SpikeTriggerEffectMode.HealWhenHealState
                ? groupController.IsGroupInHealState(groupSide)
                : groupController.IsGroupInDamageState(groupSide);
        }
    }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning($"{name} 의 SpikeStateHealthTrigger는 Trigger Collider가 필요합니다.", this);
        }

        lastEffectEnabled = IsEffectEnabled;
    }

    private void OnDisable()
    {
        colliderToHealth.Clear();
        occupants.Clear();
        removalBuffer.Clear();
    }

    private void Update()
    {
        if (groupController == null)
            return;

        bool currentEffectEnabled = IsEffectEnabled;
        if (currentEffectEnabled != lastEffectEnabled)
        {
            lastEffectEnabled = currentEffectEnabled;
            RescheduleOccupants(applyFirstTickImmediatelyOnModeChange);
        }

        if (!currentEffectEnabled || occupants.Count == 0)
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

            ApplyTick(health);
            state.NextTickTime = now + tickInterval;
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            occupants.Remove(removalBuffer[i]);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SpikeStateHealthTrigger:{name}] OnTriggerEnter -> {other.name}", this);
        }
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
        if (other == null)
            return;

        int colliderId = other.GetInstanceID();
        if (colliderToHealth.ContainsKey(colliderId))
            return;

        if (!TryResolveHealth(other, out PlayerHealth health))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[SpikeStateHealthTrigger:{name}] PlayerHealth를 찾지 못함 -> {other.name}",
                    this);
            }
            return;
        }

        if (!CanTrackHealth(health))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[SpikeStateHealthTrigger:{name}] 추적 불가 대상 -> {health.name} | localOwned={health.IsLocallyOwned}",
                    this);
            }
            return;
        }

        colliderToHealth.Add(colliderId, health);

        if (occupants.TryGetValue(health, out OccupantState existingState))
        {
            existingState.OverlapCount++;
            return;
        }

        OccupantState newState = new OccupantState
        {
            OverlapCount = 1,
            NextTickTime = Time.time + tickInterval
        };

        occupants.Add(health, newState);

        if (applyFirstTickImmediatelyOnEnter && IsEffectEnabled)
        {
            ApplyTick(health);
            newState.NextTickTime = Time.time + tickInterval;
        }
        else if (enableDebugLogs)
        {
            Debug.Log(
                $"[SpikeStateHealthTrigger:{name}] 등록됨 -> {health.name} | effectEnabled={IsEffectEnabled}",
                this);
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
            occupants.Remove(health);
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

    private void ApplyTick(PlayerHealth health)
    {
        if (health == null || amountPerTick <= 0f)
            return;

        if (effectMode == SpikeTriggerEffectMode.HealWhenHealState)
        {
            health.RequestHeal(amountPerTick);
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"플레이어 총 체력 : {health.MaxHealth} / 데미지 : 0 / 힐 : {amountPerTick} / 나머지 체력 : {health.CurrentHealth}",
                    this);
            }
            return;
        }

        health.RequestDamage(amountPerTick);
        if (enableDebugLogs)
        {
            Debug.Log(
                $"플레이어 총 체력 : {health.MaxHealth} / 데미지 : {amountPerTick} / 힐 : 0 / 나머지 체력 : {health.CurrentHealth}",
                this);
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

            if (applyImmediateTick && IsEffectEnabled)
                ApplyTick(health);

            state.NextTickTime = now + tickInterval;
        }
    }
}
