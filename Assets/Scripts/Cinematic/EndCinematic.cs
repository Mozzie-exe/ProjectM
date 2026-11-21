using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Oyun sonu. Taş oda kapısı ilk açılınca: kısa bakış (cesede), ceset odası müziği, kalp çarpıntısı, koşu kapanır.
// Cesetle etkileşimde (CorpseInspect) asıl sinematik oynar ve oyun siyah ekranla biter.
// Sahne kurulumu: Tools > End Cinematic > Build (EndCinematicBuilder).
public class EndCinematic : MonoBehaviour
{
    [Header("Level")]
    public Door door;
    [Tooltip("Örtülü ceset; bakış ve çarpıntı mesafesi bunun görünür merkezine göre")]
    public Transform corpse;

    [Header("Oyuncu")]
    public Transform player;
    public Camera playerCamera;

    [Header("Ses")]
    public AudioClip music;
    public AudioClip heartbeat;
    [Range(0f, 1f)] public float musicVolume = 0.8f;
    public float musicFadeIn = 2f;

    [Header("Sinematik (builder bağlar)")]
    public PlayableDirector director;
    public Transform head;
    public Camera cinematicCamera;
    public Transform cabinet;
    [Tooltip("Eller, kollar, fener ve kafa; sinematik başlayana kadar gizli")]
    public GameObject rig;
    [Tooltip("Sinematik zamanı (sn)")]
    public float hushAt = 13f, blackAt = 17.4f, endAt = 19f, fallAt = 16.6f;
    [Tooltip("Dolap bu eksen etrafında (dünya) devrilir; pivot alt kenar")]
    public Vector3 cabinetPivot, cabinetAxis = Vector3.forward;
    [Tooltip("Tam ekran siyah (Canvas altında, başta kapalı)")]
    public Image black;

    public bool DoorLookDone { get; private set; }
    public bool Started { get; private set; }

    // Sinematik bitti (siyah ekran, sessizlik). Credits ekranı buna bağlanacak.
    public event System.Action Ended;

    Vector3 startPos;
    Quaternion startRot;
    bool blendDone, hushed, blacked, cinematicMuted, ended;

    AudioSource musicSource, heartbeatSource;
    bool doorTriggered, looking;
    float lookClock, musicClock = -1f;
    Quaternion lookFrom;

    Vector3 cabinetRestPos;
    Quaternion cabinetRestRot;
    bool cabinetRestSaved;

    void Awake()
    {
        musicSource = MakeSource(music, AudioChannel.Music);
        heartbeatSource = MakeSource(heartbeat, AudioChannel.SFX);
        if (rig) rig.SetActive(false);
        SaveCabinetRest();
    }

    void SaveCabinetRest()
    {
        if (!cabinet || cabinetRestSaved) return;
        cabinetRestPos = cabinet.position;
        cabinetRestRot = cabinet.rotation;
        cabinetRestSaved = true;
    }

    // Dolabın sinematik zamanındaki duruşu (editörde kare kontrolü için de çağrılır)
    public void ApplyCabinet(float t)
    {
        if (!cabinet) return;
        SaveCabinetRest();
        var q = Quaternion.AngleAxis(EndCinematicMath.CabinetFallAngle(t - fallAt), cabinetAxis);
        cabinet.SetPositionAndRotation(cabinetPivot + q * (cabinetRestPos - cabinetPivot), q * cabinetRestRot);
    }

    // Editörde kare kontrolünden sonra dolabı yerine koymak için
    public void ResetCabinet()
    {
        if (!cabinet || !cabinetRestSaved) return;
        cabinet.SetPositionAndRotation(cabinetRestPos, cabinetRestRot);
        cabinetRestSaved = false;
    }

    AudioSource MakeSource(AudioClip clip, AudioChannel ch)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.outputAudioMixerGroup = GameAudio.Group(ch);
        return s;
    }

    public Vector3 CorpseCenter
    {
        get
        {
            bool any = false;
            var b = new Bounds(corpse.position, Vector3.zero);
            foreach (var r in corpse.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b.center;
        }
    }

    void Update()
    {
        if (!doorTriggered && door && door.IsOpen) TriggerDoor();

        if (looking) UpdateDoorLook();

        if (musicClock >= 0f && !Started)
        {
            musicClock += Time.deltaTime;
            musicSource.volume = musicVolume * Mathf.Clamp01(musicClock / Mathf.Max(0.01f, musicFadeIn));
        }

        if (doorTriggered && !Started)
        {
            Vector3 d = playerCamera.transform.position - CorpseCenter;
            d.y = 0f;
            heartbeatSource.volume = EndCinematicMath.HeartbeatVolume(d.magnitude);
            heartbeatSource.pitch = EndCinematicMath.HeartbeatPitch(d.magnitude);
        }
    }

    void TriggerDoor()
    {
        doorTriggered = true;
        foreach (var pm in FindObjectsByType<PlayerMovement>())
            pm.sprintAllowed = false;

        musicClock = 0f;
        if (music) musicSource.Play();
        if (heartbeat) heartbeatSource.Play();

        PlayerControlLock.Push();
        // Kontrol kilidi imleci açar; bakış sırasında görünmesin
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        lookFrom = playerCamera.transform.localRotation;
        lookClock = 0f;
        looking = true;
    }

    void UpdateDoorLook()
    {
        lookClock += Time.deltaTime;
        var cam = playerCamera.transform;
        if (lookClock >= EndCinematicMath.DoorLookDuration)
        {
            cam.localRotation = lookFrom;   // MouseLook'un iç açısıyla aynı kalsın
            looking = false;
            DoorLookDone = true;
            PlayerControlLock.Pop();
            return;
        }
        var worldTo = Quaternion.LookRotation(CorpseCenter - cam.position, Vector3.up);
        var parentRot = cam.parent ? cam.parent.rotation : Quaternion.identity;
        var localTo = Quaternion.Inverse(parentRot) * worldTo;
        cam.localRotation = Quaternion.Slerp(lookFrom, localTo, EndCinematicMath.DoorLookWeight(lookClock));
    }

    // ------------------------------------------------------------------ asıl sinematik (CorpseInspect başlatır)
    public void BeginMain()
    {
        if (Started) return;
        Started = true;

        PlayerControlLock.Push();   // bir daha açılmaz: oyun burada biter
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Geçiş oyuncunun o anki bakışından başlar (eğilmiş, yan bakıyor olabilir)
        var pc = playerCamera.transform;
        startPos = pc.position;
        startRot = pc.rotation;

        // Aynı ayarlar (render texture, post-process) korunsun diye oyuncu kamerasını kopyala
        cinematicCamera.CopyFrom(playerCamera);
        var src = playerCamera.GetUniversalAdditionalCameraData();
        var dst = cinematicCamera.GetUniversalAdditionalCameraData();
        dst.renderPostProcessing = src.renderPostProcessing;
        dst.antialiasing = src.antialiasing;
        dst.renderShadows = src.renderShadows;
        cinematicCamera.nearClipPlane = 0.05f;   // eller kesilmesin
        cinematicCamera.transform.SetPositionAndRotation(startPos, startRot);

        // Oyuncu kamerası objesi kapanır: fener modeli ve ışığı, dinleyici, etkileşim yazısı da gider.
        // Sinematiğin feneri pil ve aç/kapa durumundan bağımsız hep yanar (Rig/HandR/FlashR/Beam).
        playerCamera.gameObject.SetActive(false);
        var listener = cinematicCamera.GetComponent<AudioListener>();
        if (!listener) listener = cinematicCamera.gameObject.AddComponent<AudioListener>();
        listener.enabled = true;
        cinematicCamera.enabled = true;
        if (rig) rig.SetActive(true);

        director.time = 0;
        director.Play();
    }

    void LateUpdate()
    {
        if (!Started) return;
        float t = (float)director.time;
        ApplyCabinet(t);

        var cam = cinematicCamera.transform;
        if (t < EndCinematicMath.StartBlendDuration)
        {
            float w = EndCinematicMath.StartBlend(t);
            cam.SetPositionAndRotation(Vector3.Lerp(startPos, head.position, w), Quaternion.Slerp(startRot, head.rotation, w));
        }
        else if (!blendDone)
        {
            cam.localPosition = Vector3.zero;
            cam.localRotation = Quaternion.identity;
            blendDone = true;
        }

        if (!hushed && t >= hushAt) Hush();
        if (!blacked && t >= blackAt) Black();
        if (blacked && !cinematicMuted && t >= blackAt + 0.1f)
        {
            cinematicMuted = true;
            Mute(AudioChannel.Cinematic);
        }
        if (!ended && t >= endAt - 0.001f)
        {
            ended = true;
            director.Pause();
            Ended?.Invoke();
        }
    }

    // Her şey susar; yalnızca Cinematic kanalı (dolap sesleri) açık kalır.
    // Mixer parametresi doğrudan yazılır: GameAudioSettings.SetVolume kullanıcının ayarını kaydeder.
    void Hush()
    {
        hushed = true;
        foreach (var ch in EndCinematicMath.HushChannels) Mute(ch);
        musicSource.Stop();
        heartbeatSource.Stop();
    }

    void Mute(AudioChannel id)
    {
        var s = GameAudio.Settings;
        if (!s || !s.mixer || s.channels == null) return;
        foreach (var c in s.channels)
            if (c != null && c.id == id && !string.IsNullOrEmpty(c.param))
                s.mixer.SetFloat(c.param, -80f);
    }

    void Black()
    {
        blacked = true;
        if (!black) return;
        black.transform.SetAsLastSibling();
        black.color = Color.black;
        black.enabled = true;
    }
}
