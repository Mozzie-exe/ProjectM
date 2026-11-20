using UnityEngine;
using UnityEngine.UI;

// Kameraya eklenir. Bakılan şey etkileşimliyse ekranda "[E] Kapıyı Aç" gibi yazı gösterir.
public class PlayerInteractor : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 4f;
    public LayerMask interactMask = ~0;

    [Header("Yazı")]
    public int fontSize = 30;
    public Vector2 promptOffset = new Vector2(0f, -70f); // ekran ortasına göre

    IInteractable current;
    GameObject promptRoot;
    Text promptText;

    void Start()
    {
        BuildUI();
    }

    void Update()
    {
        current = null;

        // Oyuncunun kendi kapsülünün içinden başladığı için ona çarpmaz
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            current = hit.collider.GetComponentInParent<IInteractable>();
            // Mobilyanın kendi büyük collider'ı içindeki çekmece/kapak gibi şeyleri de bul
            if (current == null) current = FindInteractableInside(hit);
        }

        string prompt = current != null ? current.GetPrompt() : null;
        bool show = !string.IsNullOrEmpty(prompt) && Cursor.lockState == CursorLockMode.Locked;

        // "E basılı tut" etkileşimleri (kazı)
        var hold = current as IHoldInteractable;
        bool holding = hold != null && hold.CanHold(transform) && Input.GetKey(interactKey);
        if (hold != currentHold || !holding) ResetHold();
        if (holding)
        {
            currentHold = hold;
            holdTimer += Time.deltaTime;
            float t = Mathf.Clamp01(holdTimer / Mathf.Max(0.01f, hold.HoldDuration));
            hold.OnHoldProgress(t);
            if (show) prompt += "  " + ProgressBar(t);
            if (t >= 1f)
            {
                hold.OnHoldComplete(transform);
                currentHold = null;
                holdTimer = 0f;
            }
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(show);
            if (show) promptText.text = "[" + KeyName(interactKey) + "] " + prompt;
        }

        if (current != null && Input.GetKeyDown(interactKey))
            current.Interact(transform);
    }

    // Işın önce mobilyanın gövde collider'ına çarptıysa, onun çocuklarından etkileşimli olanı (çekmece vb.) ara
    IInteractable FindInteractableInside(RaycastHit first)
    {
        Transform root = first.collider.transform;
        var hits = Physics.RaycastAll(transform.position, transform.forward, interactDistance, interactMask, QueryTriggerInteraction.Ignore);
        IInteractable found = null;
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider == first.collider || !h.collider.transform.IsChildOf(root)) continue;
            var i = h.collider.GetComponentInParent<IInteractable>();
            if (i != null && h.distance < best) { best = h.distance; found = i; }
        }
        return found;
    }

    IHoldInteractable currentHold;
    float holdTimer;

    void ResetHold()
    {
        if (currentHold != null) currentHold.OnHoldProgress(0f);
        currentHold = null;
        holdTimer = 0f;
    }

    static string ProgressBar(float t)
    {
        int n = Mathf.RoundToInt(t * 12f);
        return "[" + new string('|', n) + new string('.', 12 - n) + "]";
    }

    void OnDisable()
    {
        ResetHold();
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    static string KeyName(KeyCode k)
    {
        string s = k.ToString();
        return s.Length == 1 ? s : s.Replace("Alpha", "");
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("InteractionUI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        promptRoot = new GameObject("Prompt");
        promptRoot.transform.SetParent(canvasGO.transform, false);
        var rt = promptRoot.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = promptOffset;
        rt.sizeDelta = new Vector2(800f, 60f);

        promptText = promptRoot.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = fontSize;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.raycastTarget = false;

        var outline = promptRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        promptRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (promptRoot != null) Destroy(promptRoot.transform.parent.gameObject);
    }
}
