using System;
using UnityEngine;
using UnityEngine.Audio;

// Ses seviyeleri (0-1). Inspector'dan ayarlanır; ileride ayarlar menüsü SetVolume'u çağırır.
// Konum: Assets/Resources/GameAudioSettings.asset (Tools > Audio > Build Mixer oluşturur).
[CreateAssetMenu(menuName = "Audio/Game Audio Settings", fileName = "GameAudioSettings")]
public class GameAudioSettings : ScriptableObject
{
    [Serializable]
    public class Channel
    {
        public AudioChannel id;
        public AudioMixerGroup group;
        [Tooltip("Mixer'da expose edilmiş Volume parametresi")]
        public string param;
        [Range(0f, 1f)] public float level = 1f;
    }

    public AudioMixer mixer;
    public Channel[] channels = Defaults();

    static Channel[] Defaults()
    {
        var ids = (AudioChannel[])Enum.GetValues(typeof(AudioChannel));
        var list = new Channel[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            list[i] = new Channel { id = ids[i], param = ids[i] + "Vol", level = 1f };
        return list;
    }

    // 0-1 -> dB (log ölçek). 0 tam sessiz: -80 dB, -Infinity değil.
    public static float LinearToDb(float level) => level <= 0.0001f ? -80f : Mathf.Log10(level) * 20f;

    Channel Find(AudioChannel id)
    {
        if (channels == null) return null;
        foreach (var c in channels)
            if (c != null && c.id == id) return c;
        return null;
    }

    public float GetVolume(AudioChannel id) => Find(id)?.level ?? 1f;

    public void SetVolume(AudioChannel id, float level)
    {
        var c = Find(id);
        if (c == null) return;
        c.level = Mathf.Clamp01(level);
        Apply(c);
    }

    public AudioMixerGroup GetGroup(AudioChannel id) => Find(id)?.group;

    public void ApplyAll()
    {
        if (channels == null) return;
        foreach (var c in channels) Apply(c);
    }

    void Apply(Channel c)
    {
        if (c == null || !mixer || string.IsNullOrEmpty(c.param)) return;
        mixer.SetFloat(c.param, LinearToDb(c.level));
    }

    // Play modunda slider oynatınca anında duyulsun
    void OnValidate()
    {
        if (Application.isPlaying) ApplyAll();
    }
}
