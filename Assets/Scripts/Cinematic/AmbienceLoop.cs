using UnityEngine;

// Temiz dönmeyen ortam seslerini (cırcır böceği, rüzgâr) iki AudioSource ile çapraz geçişle sonsuz çalar.
// muffle = 1 iken ses kısık ve boğuk (araç içi), 0 iken açık hava.
[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class AmbienceLoop : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("Döngünün başladığı ve bittiği yer (sn). loopEnd <= 0 ise klibin sonu.")]
    public float loopStart = 0.5f;
    public float loopEnd = -1f;
    public float crossfade = 2f;
    [Tooltip("Her turda perdeyi biraz değiştir, tekrar fark edilmesin.")]
    public Vector2 pitchRange = new Vector2(1f, 1f);

    [Header("Araç içi")]
    [Range(0f, 1f)] public float muffle;
    [Range(0f, 1f)] public float muffledVolume = 0.35f;
    public float muffledCutoff = 900f;

    readonly AudioSource[] src = new AudioSource[2];
    AudioLowPassFilter lowPass;
    int cur;
    float fade = -1f;
    float indoor;
    float zoneGain = -1f;

    void Awake()
    {
        lowPass = GetComponent<AudioLowPassFilter>();
        for (int i = 0; i < 2; i++)
        {
            src[i] = i == 0 ? GetComponent<AudioSource>() : gameObject.AddComponent<AudioSource>();
            src[i].clip = clip;
            src[i].playOnAwake = false;
            src[i].loop = false;
            src[i].spatialBlend = 0f;
            src[i].outputAudioMixerGroup = GameAudio.Group(AudioChannel.Ambience);
            src[i].volume = 0f;
        }
    }

    void OnEnable()
    {
        if (!clip) return;
        StartSource(cur, Random.Range(loopStart, Mathf.Max(loopStart, End - crossfade * 2f)));
        src[cur].volume = 1f;
    }

    void OnDisable()
    {
        foreach (var s in src) if (s) s.Stop();
        fade = -1f;
    }

    float End => loopEnd > 0f ? Mathf.Min(loopEnd, clip.length) : clip.length;

    void StartSource(int i, float at)
    {
        src[i].pitch = Random.Range(pitchRange.x, pitchRange.y);
        src[i].time = at;
        src[i].Play();
    }

    void Update()
    {
        if (!clip) return;
        var a = src[cur];
        int next = 1 - cur;

        if (fade < 0f && (!a.isPlaying || a.time >= End - crossfade))
        {
            StartSource(next, loopStart);
            fade = 0f;
        }

        float wa = 1f, wb = 0f;
        if (fade >= 0f)
        {
            fade += Time.deltaTime;
            float f = Mathf.Clamp01(fade / crossfade);
            wa = Mathf.Sqrt(1f - f); // eşit güç geçişi
            wb = Mathf.Sqrt(f);
            if (f >= 1f)
            {
                a.Stop();
                cur = next;
                fade = -1f;
                wa = 1f; wb = 0f;
            }
        }

        // Kapalı alandayken (IndoorZone) dışarının sesi de kısılır; sinematiğin araç içi değeriyle büyük olan geçerli
        bool hasL = GameAudio.TryListener(out Vector3 lp);
        float target = hasL ? IndoorZone.MuffleAt(lp) : 0f;
        indoor = Mathf.MoveTowards(indoor, target, Time.deltaTime * 1.5f);
        float m = Mathf.Max(muffle, indoor);
        // Bölge sesi (tarlada kargalar) ve bölgede kısılan sesler; ~2.5 sn'de geçer
        float zg = AmbienceZone.GainFor(this, hasL ? lp : new Vector3(1e6f, 0f, 1e6f));   // dinleyici yoksa: hiçbir bölgede değil
        zoneGain = zoneGain < 0f ? zg : Mathf.MoveTowards(zoneGain, zg, Time.deltaTime * 0.4f);   // ilk karede atla

        float gain = volume * zoneGain * Mathf.Lerp(1f, muffledVolume, m);
        if (fade >= 0f) { src[cur].volume = gain * wa; src[next].volume = gain * wb; }
        else src[cur].volume = gain;

        // Logaritmik geçiş: 22 kHz (açık) -> muffledCutoff (boğuk)
        lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(muffledCutoff), m));
    }
}
