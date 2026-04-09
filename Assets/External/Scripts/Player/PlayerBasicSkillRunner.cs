using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PlayerBasicSkillRunner : PhotonOwnedBehaviour, ISkillExecutionBridge
{
    private sealed class RuntimeEntry
    {
        public BasicSkillBinding Binding;
        public ISkillRuntime Runtime;
    }

    [Header("References")]
    [SerializeField] private Transform castOrigin;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private PlayerMovementEffectReceiver selfMovementReceiver;
    [SerializeField] private PlayerInputSource inputSource;
    [SerializeField] private PlayerItemInventory itemInventory;

    [Header("Basic Skills")]
    [SerializeField] private BasicSkillBinding[] skillBindings;

    private readonly List<RuntimeEntry> runtimeEntries = new();

    protected override void Awake()
    {
        base.Awake();

        if (castOrigin == null)
            castOrigin = transform;

        if (selfMovementReceiver == null)
            selfMovementReceiver = GetComponent<PlayerMovementEffectReceiver>();

        if (inputSource == null)
            inputSource = GetComponent<PlayerInputSource>();

        if (itemInventory == null)
            itemInventory = GetComponent<PlayerItemInventory>();

        BuildRuntimes();
    }

    private void Update()
    {
        if (!HasLocalAuthority)
            return;

        float deltaTime = Time.deltaTime;

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            runtimeEntries[i].Runtime.Tick(deltaTime);
        }

        if (inputSource == null)
            return;

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            RuntimeEntry entry = runtimeEntries[i];

            if (entry.Binding == null || entry.Runtime == null)
                continue;

            if (ShouldBlockSkillActivation(entry.Binding.slot))
                continue;

            if (inputSource.GetSkillPressedThisFrame(entry.Binding.slot))
            {
                entry.Runtime.TryActivate();
            }
        }
    }

    [ContextMenu("Rebuild Skill Runtimes")]
    public void RebuildSkillRuntimes()
    {
        BuildRuntimes();
    }

    public bool TryActivate(BasicSkillSlotType slot)
    {
        if (!HasLocalAuthority)
            return false;

        for (int i = 0; i < runtimeEntries.Count; i++)
        {
            RuntimeEntry entry = runtimeEntries[i];

            if (entry.Binding == null || entry.Runtime == null)
                continue;

            if (ShouldBlockSkillActivation(entry.Binding.slot))
                return false;

            if (entry.Binding.slot == slot)
                return entry.Runtime.TryActivate();
        }

        return false;
    }

    private void BuildRuntimes()
    {
        runtimeEntries.Clear();

        if (skillBindings == null)
            return;

        SkillRuntimeContext context = new SkillRuntimeContext(
            transform,
            castOrigin,
            effectSpawnPoint,
            selfMovementReceiver,
            this);

        HashSet<BasicSkillSlotType> duplicatedSlotCheck = new();

        for (int i = 0; i < skillBindings.Length; i++)
        {
            BasicSkillBinding binding = skillBindings[i];
            if (binding == null || binding.definition == null)
                continue;

            if (!duplicatedSlotCheck.Add(binding.slot))
            {
                Debug.LogWarning($"중복된 기본 스킬 슬롯이 있습니다. slot={binding.slot}", this);
            }

            ISkillRuntime runtime = binding.definition.CreateRuntime(context);
            if (runtime == null)
                continue;

            runtimeEntries.Add(new RuntimeEntry
            {
                Binding = binding,
                Runtime = runtime
            });
        }
    }

    public int OverlapBox(
        Vector3 center,
        Vector3 halfExtents,
        Quaternion rotation,
        Collider[] results,
        LayerMask layerMask,
        QueryTriggerInteraction triggerInteraction)
    {
        return Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            results,
            rotation,
            layerMask,
            triggerInteraction);
    }

    public void SpawnNetworkEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation)
    {
        if (effectPrefab == null)
            return;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.Instantiate(effectPrefab.name, position, rotation);
        }
        else
        {
            Instantiate(effectPrefab, position, rotation);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = castOrigin != null ? castOrigin : transform;
        if (origin == null || skillBindings == null)
            return;

        for (int i = 0; i < skillBindings.Length; i++)
        {
            BasicSkillBinding binding = skillBindings[i];
            if (binding == null || binding.definition == null)
                continue;

            if (binding.definition is PushWaveSkillDefinition pushWave)
            {
                DrawPushWaveGizmo(origin, pushWave);
            }
        }
    }

    private bool ShouldBlockSkillActivation(BasicSkillSlotType slot)
    {
        if (slot != BasicSkillSlotType.Primary)
            return false;

        return itemInventory != null && itemInventory.HasSelection;
    }

    private void DrawPushWaveGizmo(Transform origin, PushWaveSkillDefinition definition)
    {
        Vector3 center = origin.position + origin.forward * (definition.ForwardOffset + definition.Range * 0.5f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(definition.Width, definition.Height, definition.Range));

        Gizmos.matrix = previousMatrix;
    }
}
