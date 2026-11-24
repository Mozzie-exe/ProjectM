using UnityEngine;

// Oyuncu ayarları (fare hassasiyeti, ses). PlayerPrefs'e kaydedilir, oyun açılınca uygulanır.
public static class GameSettings
{
    const string SensKey = "Settings.Sensitivity";
    const string VolKey = "Settings.Volume";

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 6f;

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(SensKey, 2f);
        set { PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); ApplySensitivity(); }
    }

    // 0-1, ana ses (Master kanalı)
    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolKey, 0.8f);
        set { PlayerPrefs.SetFloat(VolKey, Mathf.Clamp01(value)); ApplyVolume(); }
    }

    public static void Save() => PlayerPrefs.Save();

    public static void ApplyAll()
    {
        ApplySensitivity();
        ApplyVolume();
    }

    static void ApplySensitivity()
    {
        float s = Sensitivity;
        foreach (var m in Object.FindObjectsByType<MouseLook>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            m.sens = s;
    }

    static void ApplyVolume()
    {
        var s = GameAudio.Settings;
        if (s) s.SetVolume(AudioChannel.Master, Volume);
        else AudioListener.volume = Volume;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnStart() => ApplyAll();
}
