using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sallanan sandalye: bakip E'ye basinca oturulur, otururken sandalye hafifce one-arkaya sallanir.
// Tekrar E'ye basinca kalkilir. Sandalyenin collider'i olan objesine eklenir.
public class RockingChairSeat : MonoBehaviour, IInteractable
{
    [Header("Oturma")]
    public string sitPrompt = "Otur";
    public string standPrompt = "Kalk";
    public KeyCode standKey = KeyCode.E;
    [Tooltip("Oturunca gozun (kameranin) duracagi nokta - sandalyenin yerel ekseninde")]
    public Vector3 seatEyeLocal = new Vector3(0f, 1.07f, -0.12f);
    [Tooltip("Sandalyenin onu (yerel). Oturunca oyuncu bu yone doner.")]
    public Vector3 seatForwardLocal = Vector3.forward;
    public float sitDuration = 0.7f;
    public float standDuration = 0.5f;

    [Header("Sallanma")]
    [Tooltip("Derece. Kucuk tut ki cok sallanmasin.")]
    public float rockAngle = 3.5f;
    [Tooltip("Bir ileri-geri sallanmanin suresi (sn)")]
    public float rockPeriod = 2.8f;
    [Tooltip("Sallanma ekseninin sandalye tabanindan yuksekligi (yerel)")]
    public float pivotHeight = 0.3f;
    [Tooltip("Yan eksen (yerel). Sandalye bu eksen etrafinda sallanir.")]
    public Vector3 rockAxisLocal = Vector3.right;
    [Tooltip("Sallanmaya baslama / durma yumusakligi (sn)")]
    public float rockBlend = 1.2f;
    [Range(0f, 1f)] public float cameraTilt = 0.6f;

    [Header("Gicirti (opsiyonel)")]
    public AudioClip[] creaks;
    public float creakVolume = 0.35f;

    bool seated, busy;
    int standFrame = -100;
    Vector3 basePos;
    Quaternion baseRot;
    float rockAmount, phase, angle, lastVel;

    Transform player, cam, camHolder;
    CharacterController cc;
    PlayerMovement move;
    PlayerInteractor interactor;
    HeadBob[] bobs;
    bool[] bobWas;
    bool moveWas, interWas;
    Vector3 standPos, camOffset;
    Quaternion camBaseRot;
    bool camRotSaved;

    GameObject ui;

    public bool IsSeated { get { return seated; } }

    void Awake()
    {
        basePos = transform.position;
        baseRot = transform.rotation;
    }

    public string GetPrompt()
    {
        if (seated || busy || Time.frameCount - standFrame < 3) return null;
        return sitPrompt;
    }

    public void Interact(Transform who)
    {
        if (seated || busy || Time.frameCount - standFrame < 3) return;
        var c = who.GetComponentInParent<CharacterController>();
        if (c == null) return;
        StartCoroutine(Sit(c, who));
    }

    Vector3 SeatPlayerPos()
    {
        return transform.TransformPoint(seatEyeLocal) - camOffset;
    }

    IEnumerator Sit(CharacterController c, Transform camera)
    {
        busy = true;
        cc = c; player = c.transform; cam = camera; camHolder = cam.parent;
        move = player.GetComponent<PlayerMovement>();
        interactor = cam.GetComponent<PlayerInteractor>();
        bobs = player.GetComponentsInChildren<HeadBob>();

        moveWas = move != null && move.enabled;
        if (move != null) move.enabled = false;
        bobWas = new bool[bobs.Length];
        for (int i = 0; i < bobs.Length; i++) { bobWas[i] = bobs[i].enabled; bobs[i].enabled = false; }
        interWas = interactor != null && interactor.enabled;
        if (interactor != null) interactor.enabled = false;
        cc.enabled = false;

        standPos = player.position;
        camOffset = cam.position - player.position;
        camBaseRot = cam.localRotation;
        camRotSaved = true;

        Vector3 fwd = baseRot * seatForwardLocal; fwd.y = 0f;
        Quaternion rFrom = player.rotation;
        Quaternion rTo = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        Vector3 pFrom = player.position;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, sitDuration);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            // hafif bir yay: once biraz alcal, sonra koltuga otur
            Vector3 p = Vector3.Lerp(pFrom, SeatPlayerPos(), e);
            p.y -= Mathf.Sin(e * Mathf.PI) * 0.15f;
            player.position = p;
            player.rotation = Quaternion.Slerp(rFrom, rTo, e);
            yield return null;
        }

        seated = true;
        busy = false;
        ShowUI(true);
    }

    IEnumerator Stand()
    {
        busy = true;
        seated = false;
        ShowUI(false);

        Vector3 pFrom = player.position;
        Vector3 pTo = standPos;
        if ((pTo - basePos).sqrMagnitude > 9f * transform.lossyScale.x)
        {
            Vector3 fwd = baseRot * seatForwardLocal; fwd.y = 0f;
            pTo = basePos + fwd.normalized * 1.6f + Vector3.up * (standPos.y - basePos.y);
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, standDuration);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            player.position = Vector3.Lerp(pFrom, pTo, e);
            yield return null;
        }

        if (cam != null && camRotSaved) { cam.localRotation = camBaseRot; camRotSaved = false; }
        cc.enabled = true;
        if (move != null) move.enabled = moveWas;
        for (int i = 0; i < bobs.Length; i++) if (bobs[i] != null) bobs[i].enabled = bobWas[i];
        standFrame = Time.frameCount;
        yield return null; // ayni karede tekrar oturmasin
        if (interactor != null) interactor.enabled = interWas;
        busy = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (seated && !busy)
        {
            if (Input.GetKeyDown(standKey) && Cursor.lockState == CursorLockMode.Locked)
                StartCoroutine(Stand());
        }

        // sallanma: oturunca yavasca baslar, kalkinca yavasca durur
        rockAmount = Mathf.MoveTowards(rockAmount, seated ? 1f : 0f, dt / Mathf.Max(0.01f, rockBlend));
        if (rockAmount > 0f)
        {
            phase += dt * Mathf.PI * 2f / Mathf.Max(0.1f, rockPeriod);
            float amp = rockAngle * Mathf.SmoothStep(0f, 1f, rockAmount);
            angle = Mathf.Sin(phase) * amp;

            float vel = Mathf.Cos(phase);
            if (seated && creaks != null && creaks.Length > 0 && Mathf.Sign(vel) != Mathf.Sign(lastVel))
            {
                var clip = creaks[Random.Range(0, creaks.Length)];
                if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position, creakVolume);
            }
            lastVel = vel;
        }
        else
        {
            angle = 0f;
            phase = 0f;
        }
        ApplyRock(angle);

        // oyuncu sandalyeyle birlikte hareket etsin
        if (seated && !busy && player != null)
            player.position = SeatPlayerPos();
    }

    void LateUpdate()
    {
        // Kameranin kendisine kucuk bir sallanma egimi (MouseLook CamHolder'i dondurur, kameraya dokunmaz)
        if (cam != null && camRotSaved)
            cam.localRotation = camBaseRot * Quaternion.Euler(angle * cameraTilt, 0f, 0f);
    }

    void ApplyRock(float a)
    {
        Vector3 axis = baseRot * rockAxisLocal.normalized;
        Vector3 pivot = basePos + (baseRot * Vector3.up) * pivotHeight * transform.lossyScale.y;
        Quaternion q = Quaternion.AngleAxis(a, axis);
        transform.SetPositionAndRotation(pivot + q * (basePos - pivot), q * baseRot);
    }

    void ShowUI(bool on)
    {
        if (on && ui == null) BuildUI();
        if (ui != null) ui.SetActive(on);
    }

    void BuildUI()
    {
        ui = new GameObject("RockingChairUI");
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var go = new GameObject("Prompt");
        go.transform.SetParent(ui.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(800f, 60f);
        var text = go.AddComponent<Text>();
        text.font = UiFactory.DefaultFont;
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        string k = standKey.ToString();
        text.text = "[" + k + "] " + standPrompt;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    void OnDisable()
    {
        // sahne degisirken / obje kapanirken oyuncuyu serbest birak
        if ((seated || busy) && player != null)
        {
            StopAllCoroutines();
            if (cam != null && camRotSaved) { cam.localRotation = camBaseRot; camRotSaved = false; }
            if (cc != null) cc.enabled = true;
            if (move != null) move.enabled = moveWas;
            if (bobs != null) for (int i = 0; i < bobs.Length; i++) if (bobs[i] != null) bobs[i].enabled = bobWas[i];
            if (interactor != null) interactor.enabled = interWas;
            seated = false; busy = false;
        }
        if (ui != null) ui.SetActive(false);
        transform.SetPositionAndRotation(basePos, baseRot);
    }

    void OnDestroy()
    {
        if (ui != null) Destroy(ui);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(seatEyeLocal), 0.1f);
        Vector3 pivot = transform.position + transform.up * pivotHeight * transform.lossyScale.y;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pivot - transform.TransformDirection(rockAxisLocal) * 0.6f, pivot + transform.TransformDirection(rockAxisLocal) * 0.6f);
    }
}
