using UnityEngine;
using UnityEngine.Audio;

// Mixer'a erişim noktası. Ayar asset'i yoksa her şey mixer'sız (eskisi gibi) çalışır.
public static class GameAudio
{
    static GameAudioSettings settings;

    public static GameAudioSettings Settings
    {
        get
        {
            if (!settings) settings = Resources.Load<GameAudioSettings>("GameAudioSettings");
            return settings;
        }
    }

    public static AudioMixerGroup Group(AudioChannel ch)
    {
        var s = Settings;
        return s ? s.GetGroup(ch) : null;
    }

    // AudioSource.PlayClipAtPoint yerine: aynı 3D ses, ama doğru mixer grubunda
    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume, AudioChannel ch)
    {
        if (!clip) return null;
        var go = new GameObject("OneShot_" + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = 1f;
        src.outputAudioMixerGroup = Group(ch);
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
        return src;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnStart()
    {
        var s = Settings;
        if (s) s.ApplyAll();
    }
}
