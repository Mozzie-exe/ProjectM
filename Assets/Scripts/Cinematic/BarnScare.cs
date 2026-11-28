using System.Collections;
using UnityEngine;

// Ahır gerilim olayı: sürgülü kapı açıldıktan sonra oyuncu alana girince bir kere oynar.
// Gıcırtı → kamera samanlık kenarına çekilir → balyalar devrilip önüne düşer → (ayak sesi)
// → kalp atışı → kontrol geri gelir. Zamanlar BarnScareMath'te.
// Kurulum: Tools > Level > Build Barn Scare (BarnScareBuilder).
public class BarnScare : MonoBehaviour
{
    public SlidingDoor door;
    public Vector3 areaSize = new Vector3(3f, 3f, 4f);   // yerel ölçüde tetik alanı
    public Vector3 areaCenter = Vector3.zero;

    [Header("Kadraj")]
    public Transform lookTarget;                         // kameranın çekileceği nokta (samanlık kenarı)

    [Header("Balyalar")]
    public Transform[] bales;
    public float[] baleHeights;                          // her balyanın düşeceği yükseklik
    public float[] baleSpins;                            // yere inene kadar toplam dönüş (derece); yoksa 90
    public Vector3 tipAxis = Vector3.forward;            // devrilme ekseni (dünya, samanlık kenarına paralel)
    public Vector3 outward = Vector3.right;              // kenardan koridora doğru (dünya)
    public float outwardReach = 1.2f;                    // devrilirken kenardan dışarı taşma
    public ParticleSystem dust;

    [Header("Ses")]
    public AudioClip creak;
    public AudioClip baleThud;
    public AudioClip heartbeat;
    public AudioClip gasp;
    public AudioClip woodSteps;
    [Range(0f, 1f)] public float heartVolume = 0.7f;

    public bool Fired { get; private set; }

    Transform player;
    Vector3[] baleStart;
    Quaternion[] baleRot;

    void Awake()
    {
        int n = bales != null ? bales.Length : 0;
        baleStart = new Vector3[n];
        baleRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            baleStart[i] = bales[i].position;
            baleRot[i] = bales[i].rotation;
            SetColliders(bales[i], false);   // düşerken oyuncuyu itmesin
        }
    }

    void Update()
    {
        if (Fired || door == null) return;
        if (player == null)
        {
            if (PlayerInventory.Instance == null) return;
            player = PlayerInventory.Instance.transform;
        }

        Vector3 p = transform.InverseTransformPoint(player.position) - areaCenter;
        bool inside = Mathf.Abs(p.x) < areaSize.x * 0.5f && Mathf.Abs(p.y) < areaSize.y * 0.5f && Mathf.Abs(p.z) < areaSize.z * 0.5f;
        if (inside && BarnScareMath.CanTrigger(door.IsOpen, Fired)) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Fired = true;
        var look = FindAnyObjectByType<MouseLook>();
        Transform holder = look != null ? look.transform : null;
        Transform body = look != null ? look.playerBody : null;
        var camT = holder != null ? holder.GetComponentInChildren<Camera>()?.transform : null;
        Vector3 camBase = camT != null ? camT.localPosition : Vector3.zero;

        if (creak != null && lookTarget != null)
            GameAudio.PlayAt(creak, lookTarget.position, 1f, AudioChannel.SFX, 40f, 3f, false);

        int n = bales != null ? bales.Length : 0;
        var landed = new bool[n];
        bool locked = false, released = false, heartDone = false, stepsDone = false;
        float yaw0 = 0f, pitch0 = 0f, yawT = 0f, pitchT = 0f;
        float shake = 0f;
        float t = 0f;

        while (!released || !AllTrue(landed))
        {
            t += Time.deltaTime;

            // Kamera çekişi
            if (!locked && !released && t >= BarnScareMath.T_Pull && holder != null && body != null)
            {
                PlayerControlLock.Push();
                Cursor.lockState = CursorLockMode.Locked;   // Push imleci açar; bu olayda görünmesin
                Cursor.visible = false;
                locked = true;
                yaw0 = body.eulerAngles.y;
                pitch0 = look.Pitch;
                Vector3 d = (lookTarget != null ? lookTarget.position : holder.position + body.forward) - holder.position;
                yawT = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                pitchT = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -80f, 80f);
            }
            if (locked)
            {
                float w = BarnScareMath.LookWeight(t);
                if (t < BarnScareMath.T_Release)
                {
                    body.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(yaw0, yawT, w), 0f);
                    holder.localRotation = Quaternion.Euler(Mathf.LerpAngle(pitch0, pitchT, w), 0f, 0f);
                }
                else
                {
                    look.Pitch = Mathf.DeltaAngle(0f, holder.localEulerAngles.x);
                    PlayerControlLock.Pop();
                    locked = false;
                    released = true;
                }
            }
            else if (t >= BarnScareMath.T_Release) released = true;   // oyuncu/kamera bulunamadıysa

            // Balyalar
            for (int i = 0; i < n; i++)
            {
                if (landed[i]) continue;
                float tl = t - (BarnScareMath.T_Bales + i * BarnScareMath.BaleStagger);
                if (tl <= 0f) continue;
                float h = baleHeights != null && i < baleHeights.Length ? baleHeights[i] : 0f;
                float ang = BarnScareMath.TipAngle(tl);
                float drop = BarnScareMath.FallDrop(tl, h);
                // Devrildikten sonra düşerken dönmeye devam eder, yere yatık iner
                float spin = baleSpins != null && i < baleSpins.Length ? baleSpins[i] : 90f;
                float frac = h > 0f ? drop / h : 1f;
                bales[i].rotation = Quaternion.AngleAxis(ang + (spin - 90f) * frac, tipAxis.normalized) * baleRot[i];
                bales[i].position = baleStart[i] + outward.normalized * (outwardReach * ang / 90f) + Vector3.down * drop;
                if (drop >= h - 1e-4f && tl > BarnScareMath.TipDuration)
                {
                    landed[i] = true;
                    SetColliders(bales[i], true);
                    if (baleThud != null) GameAudio.PlayAt(baleThud, bales[i].position, 1f - i * 0.15f, AudioChannel.SFX, 40f, 3f, false);
                    if (dust != null) { dust.transform.position = bales[i].position; dust.Emit(25); }
                    shake = 0.25f;
                }
            }

            // Kamerada küçük sarsıntı
            if (camT != null)
            {
                if (shake > 0f)
                {
                    shake -= Time.deltaTime;
                    camT.localPosition = camBase + Random.insideUnitSphere * (0.05f * Mathf.Clamp01(shake / 0.25f));
                }
                else camT.localPosition = camBase;
            }

            // Samanlıkta biri var: görünmez, yalnızca ayak sesi (ses atanırsa)
            if (!stepsDone && t >= BarnScareMath.T_Steps)
            {
                stepsDone = true;
                if (woodSteps != null && lookTarget != null)
                    GameAudio.PlayAt(woodSteps, lookTarget.position, 0.9f, AudioChannel.SFX, 40f, 3f, false);
            }

            // Kalp atışı ve nefes
            if (!heartDone && t >= BarnScareMath.T_Heart)
            {
                heartDone = true;
                StartCoroutine(Heartbeat());
            }

            yield return null;
        }
        if (camT != null) camT.localPosition = camBase;
    }

    IEnumerator Heartbeat()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.outputAudioMixerGroup = GameAudio.Group(AudioChannel.SFX);
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        if (gasp != null) src.PlayOneShot(gasp, 0.8f);
        if (heartbeat == null) yield break;

        src.clip = heartbeat;
        src.loop = true;
        src.volume = heartVolume;
        src.Play();
        const float hold = 1.5f, fade = 3f;
        float t = 0f;
        while (t < hold + fade)
        {
            t += Time.deltaTime;
            src.volume = heartVolume * (1f - Mathf.Clamp01((t - hold) / fade));
            yield return null;
        }
        src.Stop();
        Destroy(src);
    }

    static bool AllTrue(bool[] a) { foreach (var b in a) if (!b) return false; return true; }

    static void SetColliders(Transform t, bool on)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>()) c.enabled = on;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawCube(areaCenter, areaSize);
    }
}
