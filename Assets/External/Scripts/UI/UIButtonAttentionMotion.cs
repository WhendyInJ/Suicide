using UnityEngine;

[DisallowMultipleComponent]
public class UIButtonAttentionMotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform targetRectTransform;

    [Header("Scale Motion")]
    [SerializeField, Min(0f)] private float scaleMultiplierAmplitude = 0.08f;
    [SerializeField, Min(0.01f)] private float scaleFrequency = 1.8f;

    [Header("Rotation Motion")]
    [SerializeField, Min(0f)] private float rotationAmplitude = 4f;
    [SerializeField, Min(0.01f)] private float rotationFrequency = 1.2f;
    [SerializeField] private float rotationPhaseOffset = 0.35f;

    [Header("Options")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool randomizeStartOffset = true;

    private Vector3 baseScale = Vector3.one;
    private Quaternion baseRotation = Quaternion.identity;
    private float timeOffset;

    private void Reset()
    {
        if (targetRectTransform == null)
            targetRectTransform = transform as RectTransform;
    }

    private void Awake()
    {
        if (targetRectTransform == null)
            targetRectTransform = transform as RectTransform;

        if (targetRectTransform == null)
            targetRectTransform = GetComponent<RectTransform>();

        CacheBaseTransform();
        timeOffset = randomizeStartOffset ? Random.Range(0f, 100f) : 0f;
    }

    private void OnEnable()
    {
        CacheBaseTransform();
        ApplyMotion(0f);
    }

    private void OnDisable()
    {
        RestoreBaseTransform();
    }

    private void Update()
    {
        float currentTime = (useUnscaledTime ? Time.unscaledTime : Time.time) + timeOffset;
        ApplyMotion(currentTime);
    }

    private void CacheBaseTransform()
    {
        if (targetRectTransform == null)
            return;

        baseScale = targetRectTransform.localScale;
        baseRotation = targetRectTransform.localRotation;
    }

    private void ApplyMotion(float currentTime)
    {
        if (targetRectTransform == null)
            return;

        float scaleWave = Mathf.Sin(currentTime * scaleFrequency * Mathf.PI * 2f);
        float scaleMultiplier = 1f + scaleWave * scaleMultiplierAmplitude;

        float rotationWave = Mathf.Sin((currentTime + rotationPhaseOffset) * rotationFrequency * Mathf.PI * 2f);
        float zRotation = rotationWave * rotationAmplitude;

        targetRectTransform.localScale = baseScale * scaleMultiplier;
        targetRectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, zRotation);
    }

    private void RestoreBaseTransform()
    {
        if (targetRectTransform == null)
            return;

        targetRectTransform.localScale = baseScale;
        targetRectTransform.localRotation = baseRotation;
    }
}
