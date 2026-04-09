using UnityEngine;

public static class CameraLookSensitivitySettings
{
    private const string YawSensitivityKey = "camera.yawSensitivity";
    private const string PitchSensitivityKey = "camera.pitchSensitivity";

    public const float MaxDisplaySensitivity = 10f;
    public const float MidDisplaySensitivity = MaxDisplaySensitivity * 0.5f;
    public const float DefaultDisplaySensitivity = 10f;
    public const float MidYawSpeed = 180f;
    public const float MidPitchSpeed = 120f;

    public static float LoadYawDisplayValue(float fallback = DefaultDisplaySensitivity)
    {
        float savedValue = PlayerPrefs.GetFloat(YawSensitivityKey, fallback);
        return NormalizeDisplayValue(savedValue, MidYawSpeed, fallback);
    }

    public static float LoadPitchDisplayValue(float fallback = DefaultDisplaySensitivity)
    {
        float savedValue = PlayerPrefs.GetFloat(PitchSensitivityKey, fallback);
        return NormalizeDisplayValue(savedValue, MidPitchSpeed, fallback);
    }

    public static float LoadYawSpeed(float fallbackDisplay = DefaultDisplaySensitivity)
    {
        return ConvertYawDisplayToSpeed(LoadYawDisplayValue(fallbackDisplay));
    }

    public static float LoadPitchSpeed(float fallbackDisplay = DefaultDisplaySensitivity)
    {
        return ConvertPitchDisplayToSpeed(LoadPitchDisplayValue(fallbackDisplay));
    }

    public static void SaveDisplayValues(float yawDisplayValue, float pitchDisplayValue)
    {
        PlayerPrefs.SetFloat(YawSensitivityKey, Mathf.Clamp(yawDisplayValue, 0f, MaxDisplaySensitivity));
        PlayerPrefs.SetFloat(PitchSensitivityKey, Mathf.Clamp(pitchDisplayValue, 0f, MaxDisplaySensitivity));
        PlayerPrefs.Save();
    }

    public static float ConvertYawDisplayToSpeed(float displayValue)
    {
        return ConvertDisplayToSpeed(displayValue, MidYawSpeed);
    }

    public static float ConvertPitchDisplayToSpeed(float displayValue)
    {
        return ConvertDisplayToSpeed(displayValue, MidPitchSpeed);
    }

    private static float ConvertDisplayToSpeed(float displayValue, float midSpeed)
    {
        float normalizedDisplay = Mathf.Clamp(displayValue, 0f, MaxDisplaySensitivity) / MidDisplaySensitivity;
        return midSpeed * normalizedDisplay;
    }

    private static float NormalizeDisplayValue(float savedValue, float midSpeed, float fallback)
    {
        float normalizedFallback = Mathf.Clamp(fallback, 0f, MaxDisplaySensitivity);

        if (savedValue < 0f)
            return normalizedFallback;

        if (savedValue <= MaxDisplaySensitivity)
            return savedValue;

        if (midSpeed <= 0f)
            return normalizedFallback;

        float legacyDisplayValue = (savedValue / midSpeed) * MidDisplaySensitivity;
        return Mathf.Clamp(legacyDisplayValue, 0f, MaxDisplaySensitivity);
    }
}
