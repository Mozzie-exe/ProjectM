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

    // AudioSource.PlayClipAtPoint yerine: 3D ses, doğru mixer grubunda, mesafeyle sönen.
    // occlude: kaynakla dinleyici farklı kapalı alandaysa (IndoorZone) duvar arkası gibi boğuk duyulur.
    // Kapı ve pencere gibi duvarın üstündeki sesler false vermeli (iki taraftan da açık duyulur).
    public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume, AudioChannel ch,
                                     float maxDistance = 30f, float minDistance = 1.5f, bool occlude = true)
    {
        if (!clip) return null;
        var go = new GameObject("OneShot_" + clip.name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.outputAudioMixerGroup = Group(ch);
        Apply3D(src, minDistance, maxDistance);
        if (occlude && TryListener(out Vector3 lp))
        {
            float o = IndoorZone.Occlusion(lp, pos);
            if (o > 0f)
            {
                src.volume = volume * Mathf.Lerp(1f, 0.45f, o);
                go.AddComponent<AudioLowPassFilter>().cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(1100f), o));
            }
        }
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
        return src;
    }

    // Mesafe zayıflaması: minDistance'a kadar tam ses, sonra 1/mesafe (her iki katında -6 dB),
    // son %40'ta yumuşakça sıfıra iner; maxDistance'ta hiç duyulmaz (Unity'nin logaritmik eğrisi sıfıra inmez).
    public static void Apply3D(AudioSource src, float minDistance, float maxDistance)
    {
        maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
        src.spatialBlend = 1f;
        src.dopplerLevel = 0f;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Custom;
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(minDistance, maxDistance));
    }

    // x: mesafe / maxDistance, y: kazanç
    public static AnimationCurve Rolloff(float minDistance, float maxDistance)
    {
        const int N = 14;
        var keys = new Keyframe[N + 2];
        keys[0] = new Keyframe(0f, 1f);
        float r = Mathf.Pow(maxDistance / minDistance, 1f / N);
        for (int i = 0; i <= N; i++)
        {
            float d = minDistance * Mathf.Pow(r, i), x = d / maxDistance;
            float tail = Mathf.Clamp01((x - 0.6f) / 0.4f);
            keys[i + 1] = new Keyframe(x, minDistance / d * (1f - tail * tail * (3f - 2f * tail)));
        }
        keys[N + 1].value = 0f;
        // Monoton teğetler (Fritsch-Carlson): eğri anahtarlar arasında 1'i aşmaz, geri yükselmez
        for (int i = 0; i < keys.Length; i++)
        {
            float a = i > 0 ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time) : 0f;
            float b = i < keys.Length - 1 ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time) : 0f;
            float m = a * b <= 0f ? 0f : 2f * a * b / (a + b);
            keys[i].inTangent = keys[i].outTangent = m;
        }
        return new AnimationCurve(keys);
    }

    // Etkin AudioListener (sinematikte ve oyunda farklı kamerada olabilir)
    static AudioListener listener;
    static int listenerSearchFrame = -1;
    public static bool TryListener(out Vector3 pos)
    {
        if ((!listener || !listener.isActiveAndEnabled) && listenerSearchFrame != Time.frameCount)
        {
            listenerSearchFrame = Time.frameCount;
            listener = null;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.isActiveAndEnabled) { listener = l; break; }
        }
        bool ok = listener && listener.isActiveAndEnabled;
        pos = ok ? listener.transform.position : default;
        return ok;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnStart()
    {
        var s = Settings;
        if (s) s.ApplyAll();
    }
}
