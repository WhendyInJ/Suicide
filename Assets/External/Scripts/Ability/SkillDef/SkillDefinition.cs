using UnityEngine;

public abstract class SkillDefinition : ScriptableObject
{
    [Header("Common")]
    [SerializeField] private string skillId;
    [SerializeField] private string displayName;
    [SerializeField, Min(0f)] private float cooldown = 0.5f;

    public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float Cooldown => cooldown;

    public abstract ISkillRuntime CreateRuntime(SkillRuntimeContext context);
}