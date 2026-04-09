using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerHealthStatusEntryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private RectTransform increasedArrow;
    [SerializeField] private RectTransform decreasedArrow;

    [Header("Format")]
    [SerializeField] private string fallbackPlayerName = "Unknown";
    [SerializeField] private string healthFormat = "{0}";

    [Header("Health Change Visual")]
    [SerializeField, Min(0f)] private float arrowVisibleDuration = 0.75f;
    [FormerlySerializedAs("arrowScaleAmplitude")]
    [SerializeField, Min(1f)] private float arrowScalePunchMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float arrowRotationAmplitude = 10f;
    [SerializeField, Min(0.01f)] private float arrowBounceFrequency = 5f;
    [SerializeField, Min(0.01f)] private float arrowBounceDamping = 4f;

    private float displayedHealthValue = float.NaN;
    private float arrowAnimationElapsed = 0f;
    private RectTransform activeArrow;
    private RectTransform inactiveArrow;
    private Vector3 increasedArrowBaseScale;
    private Vector3 decreasedArrowBaseScale;
    private Quaternion increasedArrowBaseRotation;
    private Quaternion decreasedArrowBaseRotation;

    private void Awake()
    {
        NormalizeAnimationSettings();
        CacheArrowDefaults();
        HideAllArrowsImmediate();
    }

    private void OnValidate()
    {
        NormalizeAnimationSettings();
    }

    private void OnDisable()
    {
        HideAllArrowsImmediate();
    }

    private void Update()
    {
        TickArrowAnimation();
    }

    public void SetPlayerName(string playerName)
    {
        if (playerNameText == null)
            return;

        playerNameText.text = string.IsNullOrWhiteSpace(playerName)
            ? fallbackPlayerName
            : playerName;
    }

    public void SetHealth(float currentHealth)
    {
        float sanitizedHealth = Mathf.Max(0f, currentHealth);
        bool shouldAnimate = !float.IsNaN(displayedHealthValue) && !Mathf.Approximately(displayedHealthValue, sanitizedHealth);

        if (healthValueText == null)
        {
            displayedHealthValue = sanitizedHealth;
            return;
        }

        int displayedHealth = Mathf.CeilToInt(sanitizedHealth);
        healthValueText.text = string.Format(healthFormat, displayedHealth);

        if (shouldAnimate)
        {
            PlayHealthChangeArrow(sanitizedHealth > displayedHealthValue);
        }

        displayedHealthValue = sanitizedHealth;
    }

    public void SetData(string playerName, float currentHealth)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            displayedHealthValue = float.NaN;
            HideAllArrowsImmediate();
        }

        SetPlayerName(playerName);
        SetHealth(currentHealth);
    }

    private void CacheArrowDefaults()
    {
        if (increasedArrow != null)
        {
            increasedArrowBaseScale = Vector3.one;
            increasedArrowBaseRotation = increasedArrow.localRotation;
        }

        if (decreasedArrow != null)
        {
            decreasedArrowBaseScale = Vector3.one;
            decreasedArrowBaseRotation = decreasedArrow.localRotation;
        }
    }

    private void PlayHealthChangeArrow(bool increased)
    {
        activeArrow = increased ? increasedArrow : decreasedArrow;
        inactiveArrow = increased ? decreasedArrow : increasedArrow;
        arrowAnimationElapsed = 0f;

        if (inactiveArrow != null)
        {
            inactiveArrow.gameObject.SetActive(false);
            ResetArrowTransform(inactiveArrow);
        }

        if (activeArrow == null)
            return;

        activeArrow.gameObject.SetActive(true);
        ResetArrowTransform(activeArrow);
    }

    private void TickArrowAnimation()
    {
        if (activeArrow == null)
            return;

        arrowAnimationElapsed += Time.unscaledDeltaTime;

        if (arrowAnimationElapsed >= arrowVisibleDuration)
        {
            HideAllArrowsImmediate();
            return;
        }

        float oscillation = Mathf.Sin(arrowAnimationElapsed * arrowBounceFrequency * Mathf.PI * 2f);
        float damping = Mathf.Exp(-arrowAnimationElapsed * arrowBounceDamping);
        float punch = Mathf.Abs(oscillation) * damping;
        float scaleMultiplier = Mathf.LerpUnclamped(1f, arrowScalePunchMultiplier, punch);
        float rotationOffset = oscillation * arrowRotationAmplitude * damping;

        Vector3 baseScale = GetBaseScale(activeArrow);
        Quaternion baseRotation = GetBaseRotation(activeArrow);

        activeArrow.localScale = baseScale * scaleMultiplier;
        activeArrow.localRotation = baseRotation * Quaternion.Euler(0f, 0f, rotationOffset);
    }

    private void HideAllArrowsImmediate()
    {
        arrowAnimationElapsed = 0f;

        if (increasedArrow != null)
        {
            increasedArrow.gameObject.SetActive(false);
            ResetArrowTransform(increasedArrow);
        }

        if (decreasedArrow != null)
        {
            decreasedArrow.gameObject.SetActive(false);
            ResetArrowTransform(decreasedArrow);
        }

        activeArrow = null;
        inactiveArrow = null;
    }

    private void ResetArrowTransform(RectTransform arrow)
    {
        if (arrow == null)
            return;

        arrow.localScale = GetBaseScale(arrow);
        arrow.localRotation = GetBaseRotation(arrow);
    }

    private Vector3 GetBaseScale(RectTransform arrow)
    {
        if (arrow == increasedArrow)
            return increasedArrowBaseScale;

        if (arrow == decreasedArrow)
            return decreasedArrowBaseScale;

        return Vector3.one;
    }

    private Quaternion GetBaseRotation(RectTransform arrow)
    {
        if (arrow == increasedArrow)
            return increasedArrowBaseRotation;

        if (arrow == decreasedArrow)
            return decreasedArrowBaseRotation;

        return Quaternion.identity;
    }

    private void NormalizeAnimationSettings()
    {
        arrowVisibleDuration = Mathf.Max(0f, arrowVisibleDuration);
        arrowRotationAmplitude = Mathf.Max(0f, arrowRotationAmplitude);
        arrowBounceFrequency = Mathf.Max(0.01f, arrowBounceFrequency);
        arrowBounceDamping = Mathf.Max(0.01f, arrowBounceDamping);

        if (arrowScalePunchMultiplier < 1f)
        {
            arrowScalePunchMultiplier = 1f + Mathf.Max(0f, arrowScalePunchMultiplier);
        }
    }
}
