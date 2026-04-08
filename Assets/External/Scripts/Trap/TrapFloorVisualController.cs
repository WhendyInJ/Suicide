using UnityEngine;

[System.Serializable]
public struct TrapFloorVisualState
{
    public Color baseColor;
    public Color emissionColor;
    public GameObject[] activeObjects;
    public ParticleSystem[] particles;
}

public class TrapFloorVisualController : MonoBehaviour
{
    [Header("Target Renderers")]
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Shader Properties")]
    [SerializeField] private bool applyBaseColor = true;
    [SerializeField] private string baseColorPropertyName = "_BaseColor";
    [SerializeField] private bool applyEmissionColor = false;
    [SerializeField] private string emissionColorPropertyName = "_EmissionColor";

    [Header("Damage Visual")]
    [SerializeField] private TrapFloorVisualState damageVisual;

    [Header("Heal Visual")]
    [SerializeField] private TrapFloorVisualState healVisual;

    private readonly MaterialPropertyBlock propertyBlock = new();

    public void ApplyMode(TrapFloorMode mode, bool includeParticles = true)
    {
        TrapFloorVisualState activeState = mode == TrapFloorMode.Damage ? damageVisual : healVisual;
        TrapFloorVisualState inactiveState = mode == TrapFloorMode.Damage ? healVisual : damageVisual;

        ApplyRendererState(activeState);
        SetObjectsActive(activeState.activeObjects, true);
        SetObjectsActive(inactiveState.activeObjects, false);

        if (includeParticles)
        {
            SetParticlesActive(activeState.particles, true);
            SetParticlesActive(inactiveState.particles, false);
        }
    }

    private void ApplyRendererState(TrapFloorVisualState state)
    {
        if (targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);

            if (applyBaseColor && !string.IsNullOrWhiteSpace(baseColorPropertyName))
                propertyBlock.SetColor(baseColorPropertyName, state.baseColor);

            if (applyEmissionColor && !string.IsNullOrWhiteSpace(emissionColorPropertyName))
                propertyBlock.SetColor(emissionColorPropertyName, state.emissionColor);

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    private void SetParticlesActive(ParticleSystem[] particles, bool active)
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem ps = particles[i];
            if (ps == null)
                continue;

            if (active)
            {
                if (!ps.isPlaying)
                    ps.Play(true);
            }
            else
            {
                if (ps.isPlaying)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}