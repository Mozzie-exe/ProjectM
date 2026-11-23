using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Giriş sinematiği. Sahne açılınca Timeline'ı oynatır, bu sürede oyuncu kontrolünü kapatır.
// Sinematik bitince oyuncuyu sinematik kameranın son konumuna/açısına koyar ve kontrolü ona verir.
// Sahne kurulumu: Tools > Intro Cinematic > Build (IntroCinematicBuilder).
[DefaultExecutionOrder(-100)]
public class IntroCinematic : MonoBehaviour
{
    public PlayableDirector director;
    public Camera cinematicCamera;
    [Tooltip("Sinematikteki kafa (kamera) objesi. Bitişte oyuncu buraya yerleşir.")]
    public Transform head;
    [Tooltip("Sadece sinematikte görünen eller; bitişte gizlenir.")]
    public GameObject[] hideOnHandoff;

    [Header("Oyuncu")]
    public Transform player;
    public Camera playerCamera;
    [Tooltip("Sinematik süresince kapatılır, bitişte eski hâline döner.")]
    public Behaviour[] disableDuringCinematic;
    public Flashlight flashlight;
    public bool flashlightOnAfter = true;

    [Header("Zamanlama")]
    [Tooltip("Timeline'da kontrolün oyuncuya geçtiği an (sn).")]
    public float handoffTime = 49f;
    public float cinematicNearClip = 0.05f;

    [Header("Kararma")]
    public Image fade;
    [Tooltip("Açılmadan önce ekran bu kadar sn simsiyah kalır")]
    public float fadeHoldAtStart = 0f;
    public float fadeInAtStart = 2f;
    public float fadeOutBeforeHandoff = 0.5f;
    public float fadeInAfterHandoff = 0.6f;

    [Header("Yer ve saat yazısı (sol alt)")]
    public Graphic titleText;
    public Graphic timeText;
    [Tooltip("Timeline zamanı (sn)")]
    public float titleIn = 5f, timeIn = 7.5f, textOut = 21f;
    // Diyalog altyazıları (Timeline zamanı)
    public Text subtitleText;
    public SubtitleLine[] subtitles;
    [Tooltip("Diyalog sırasında kısılan sesler (motor, sürüş, trafik)")]
    public AudioSource[] duckSources;
    [Range(0f, 1f)] public float duckLevel = 0.5f;   // 0.5 ≈ -6 dB
    public float duckAttack = 0.3f, duckRelease = 0.8f;
    float[] duckBase;
    public float textFadeDuration = 1.8f;

    [Header("Ortam sesi")]
    [Tooltip("Araçtayken boğuk, kapı açılınca netleşir; oyun boyunca çalmaya devam eder.")]
    public AmbienceLoop[] ambience;
    public float ambienceOpenStart = 26.7f;
    public float ambienceOpenEnd = 27.9f;
    [Tooltip("Motor kapanınca ortam bir an susar, sonra sesler teker teker geri gelir (Timeline zamanı, sn). <0: kapalı")]
    public float hushAt = -1f;
    public float hushIn = 0.5f, hushHold = 1.8f, hushReturn = 3.5f;
    [Tooltip("Her ortam sesi bir öncekinden bu kadar sonra döner")]
    public float hushStagger = 1.4f;

    [Header("Bitiş")]
    [Tooltip("Sinematiğin sonunda sönen ev ışıkları (oyunda da sönük kalır)")]
    public GameObject houseLights;
    [Tooltip("Timeline zamanı (sn). <0: sönmez")]
    public float houseLightsOffAt = -1f;

    [Header("Atlama")]
    [Tooltip("Bu tuşlardan biri basılı tutulunca sinematik atlanır")]
    public KeyCode[] skipKeys = { KeyCode.Space, KeyCode.Return, KeyCode.Escape };
    public float skipHold = 0.8f;
    public float skipFade = 0.35f;
    [Tooltip("Sağ altta 'basılı tut' ipucu")]
    public Graphic skipHint;
    public float hintIn = 4f;
    [Range(0f, 1f)] public float hintAlpha = 0.45f;
    [Tooltip("Sinematik bir kez izlendiyse (kontrole geçildiyse) sonraki açılışlarda doğrudan oyuna başla")]
    public bool autoSkipIfSeen = true;
    [Tooltip("Editörde de otomatik atla (kapalıyken editörde sinematik her seferinde oynar)")]
    public bool autoSkipInEditor = false;
    const string SeenKey = "IntroCinematic.Seen";

    float skipHeld, skipClock = -1f;
    bool[] wasEnabled;
    AudioListener playerListener, cinematicListener;
    bool handedOff;
    float handoffClock;
    float playerEyeHeight;

    void Awake()
    {
        playerEyeHeight = playerCamera.transform.position.y - player.position.y;

        wasEnabled = new bool[disableDuringCinematic.Length];
        for (int i = 0; i < disableDuringCinematic.Length; i++)
        {
            wasEnabled[i] = disableDuringCinematic[i].enabled;
            disableDuringCinematic[i].enabled = false;
        }

        // Aynı ayarlar (Pixelated render texture, post-process) korunsun diye oyuncu kamerasını kopyala
        cinematicCamera.CopyFrom(playerCamera);
        // CopyFrom konumu da kopyalar; kamera kafada kalmalı
        cinematicCamera.transform.localPosition = Vector3.zero;
        cinematicCamera.transform.localRotation = Quaternion.identity;
        cinematicCamera.nearClipPlane = cinematicNearClip; // eller ve torpido kesilmesin
        var src = playerCamera.GetUniversalAdditionalCameraData();
        var dst = cinematicCamera.GetUniversalAdditionalCameraData();
        dst.renderPostProcessing = src.renderPostProcessing;
        dst.antialiasing = src.antialiasing;
        dst.renderShadows = src.renderShadows;

        playerCamera.enabled = false;
        cinematicCamera.enabled = true;
        // 3D sesler (geçen arabalar) sinematik kafadan duyulsun: dinleyici de kameraya geçer
        playerListener = playerCamera.GetComponent<AudioListener>();
        cinematicListener = cinematicCamera.GetComponent<AudioListener>();
        if (!cinematicListener) cinematicListener = cinematicCamera.gameObject.AddComponent<AudioListener>();
        if (playerListener) playerListener.enabled = false;
        cinematicListener.enabled = true;
        SetFade(1f);
        SetAlpha(titleText, 0f);
        SetAlpha(timeText, 0f);
        SetAlpha(skipHint, 0f);
        SetAlpha(subtitleText, 0f);
        if (duckSources != null)
        {
            duckBase = new float[duckSources.Length];
            for (int i = 0; i < duckSources.Length; i++) duckBase[i] = duckSources[i] ? duckSources[i].volume : 1f;
        }
    }

    void SetDuck(float d)
    {
        if (duckSources == null || duckBase == null) return;
        float k = Mathf.Lerp(1f, duckLevel, d);
        for (int i = 0; i < duckSources.Length; i++)
            if (duckSources[i]) duckSources[i].volume = duckBase[i] * k;
    }

    void Start()
    {
        bool auto = autoSkipIfSeen && PlayerPrefs.GetInt(SeenKey, 0) == 1;
#if UNITY_EDITOR
        auto &= autoSkipInEditor;
#endif
        if (auto)
        {
            director.time = handoffTime;
            director.Evaluate();
            Handoff();   // kararmadan açılır (fadeInAfterHandoff)
            return;
        }
        // Projede "Enter Play Mode" sahne yeniden yüklemesi kapalı; Play On Awake'e güvenme
        if (director.state != PlayState.Playing)
        {
            director.time = 0;
            director.Play();
        }
    }

    void Update()
    {
        if (handedOff)
        {
            handoffClock += Time.deltaTime;
            SetFade(1f - Mathf.Clamp01(handoffClock / fadeInAfterHandoff));
            if (handoffClock >= fadeInAfterHandoff) enabled = false;
            return;
        }

        // Atlama: tuş basılı tutulunca kararır, sonra son kareye atlayıp kontrolü verir
        if (skipClock < 0f)
        {
            bool held = false;
            foreach (var k in skipKeys) held |= Input.GetKey(k);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            if (skipHeld >= skipHold) skipClock = 0f;
        }
        if (skipClock >= 0f)
        {
            skipClock += Time.unscaledDeltaTime;
            SetFade(Mathf.Max(fade ? fade.color.a : 0f, Mathf.Clamp01(skipClock / skipFade)));
            SetAlpha(titleText, 0f);
            SetAlpha(timeText, 0f);
            SetAlpha(skipHint, 0f);
            SetAlpha(subtitleText, 0f);
            if (skipClock >= skipFade)
            {
                director.time = handoffTime;
                director.Evaluate();
                Handoff();
            }
            return;
        }

        float t = (float)director.time;
        float fadeIn = 1f - Smooth01((t - fadeHoldAtStart) / fadeInAtStart);
        float fadeOut = Mathf.Clamp01((t - (handoffTime - fadeOutBeforeHandoff)) / fadeOutBeforeHandoff);
        SetFade(Mathf.Max(fadeIn, fadeOut));
        SetMuffle(1f - Mathf.Clamp01((t - ambienceOpenStart) / Mathf.Max(0.01f, ambienceOpenEnd - ambienceOpenStart)));
        SetHush(t);
        if (houseLights && houseLightsOffAt >= 0f && t >= houseLightsOffAt && houseLights.activeSelf)
            houseLights.SetActive(false);
        float outA = 1f - Smooth01((t - textOut) / textFadeDuration);
        SetAlpha(titleText, Smooth01((t - titleIn) / textFadeDuration) * outA);
        SetAlpha(timeText, Smooth01((t - timeIn) / textFadeDuration) * outA);
        ShowSubtitle(t);
        SetDuck(Subtitles.Duck(subtitles, t, duckAttack, duckRelease));
        // İpucu: sonlara doğru (devirden 3 sn önce) söner; basılı tutarken belirginleşir
        float hint = Smooth01((t - hintIn) / 1.5f) * (1f - Smooth01((t - (handoffTime - 3f)) / 1.5f));
        SetAlpha(skipHint, hint * Mathf.Lerp(hintAlpha, 1f, Mathf.Clamp01(skipHeld / skipHold)));

        if (t >= handoffTime - 0.001f)
            Handoff();
    }

    void ShowSubtitle(float t)
    {
        if (!subtitleText) return;
        int i = Subtitles.IndexAt(subtitles, t);
        if (i < 0) { SetAlpha(subtitleText, 0f); return; }
        subtitleText.text = subtitles[i].text;
        SetAlpha(subtitleText, Subtitles.Alpha(subtitles[i], t));
    }

    void Handoff()
    {
        handedOff = true;
        SetAlpha(subtitleText, 0f);
        SetDuck(0f);
        director.Pause(); // son kare olduğu gibi kalsın (araba park hâlinde)
        SetMuffle(0f);
        SetHush(float.MaxValue);
        if (houseLights && houseLightsOffAt >= 0f) houseLights.SetActive(false);
        SetAlpha(titleText, 0f);
        SetAlpha(timeText, 0f);
        SetAlpha(skipHint, 0f);
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.position = head.position - Vector3.up * playerEyeHeight;
        player.rotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f); // bakış yatay biter, MouseLook 0'dan başlar
        if (cc) cc.enabled = true;

        foreach (var go in hideOnHandoff)
            if (go) go.SetActive(false);

        cinematicCamera.enabled = false;
        playerCamera.enabled = true;
        if (cinematicListener) cinematicListener.enabled = false;
        if (playerListener) playerListener.enabled = true;

        for (int i = 0; i < disableDuringCinematic.Length; i++)
            disableDuringCinematic[i].enabled = wasEnabled[i];

        if (flashlight && flashlightOnAfter)
            flashlight.SetOn(true, false);
    }

    void SetMuffle(float m)
    {
        if (ambience == null) return;
        foreach (var a in ambience)
            if (a) a.muffle = m;
    }

    void SetHush(float t)
    {
        if (ambience == null) return;
        for (int i = 0; i < ambience.Length; i++)
        {
            if (!ambience[i]) continue;
            float h = 0f;
            if (hushAt >= 0f)
            {
                float back = hushAt + hushIn + hushHold + i * hushStagger;
                h = Smooth01((t - hushAt) / hushIn) * (1f - Smooth01((t - back) / hushReturn));
            }
            ambience[i].hush = h;
        }
    }

    static float Smooth01(float s) { s = Mathf.Clamp01(s); return s * s * (3f - 2f * s); }

    static void SetAlpha(Graphic g, float a)
    {
        if (!g) return;
        var c = g.color;
        c.a = a;
        g.color = c;
        g.enabled = a > 0.001f;
    }

    void SetFade(float a)
    {
        if (!fade) return;
        var c = fade.color;
        c.a = a;
        fade.color = c;
        fade.enabled = a > 0.001f;
    }
}
