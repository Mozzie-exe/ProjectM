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
    public float fadeInAtStart = 2f;
    public float fadeOutBeforeHandoff = 0.5f;
    public float fadeInAfterHandoff = 0.6f;

    [Header("Yer ve saat yazısı (sol alt)")]
    public Graphic titleText;
    public Graphic timeText;
    [Tooltip("Timeline zamanı (sn)")]
    public float titleIn = 5f, timeIn = 7.5f, textOut = 21f;
    public float textFadeDuration = 1.8f;

    [Header("Ortam sesi")]
    [Tooltip("Araçtayken boğuk, kapı açılınca netleşir; oyun boyunca çalmaya devam eder.")]
    public AmbienceLoop[] ambience;
    public float ambienceOpenStart = 26.7f;
    public float ambienceOpenEnd = 27.9f;

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
    }

    void Start()
    {
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

        float t = (float)director.time;
        float fadeIn = 1f - Smooth01(t / fadeInAtStart);
        float fadeOut = Mathf.Clamp01((t - (handoffTime - fadeOutBeforeHandoff)) / fadeOutBeforeHandoff);
        SetFade(Mathf.Max(fadeIn, fadeOut));
        SetMuffle(1f - Mathf.Clamp01((t - ambienceOpenStart) / Mathf.Max(0.01f, ambienceOpenEnd - ambienceOpenStart)));
        float outA = 1f - Smooth01((t - textOut) / textFadeDuration);
        SetAlpha(titleText, Smooth01((t - titleIn) / textFadeDuration) * outA);
        SetAlpha(timeText, Smooth01((t - timeIn) / textFadeDuration) * outA);

        if (t >= handoffTime - 0.001f)
            Handoff();
    }

    void Handoff()
    {
        handedOff = true;
        director.Pause(); // son kare olduğu gibi kalsın (araba park hâlinde)
        SetMuffle(0f);
        SetAlpha(titleText, 0f);
        SetAlpha(timeText, 0f);

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
