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
            current = hit.collider.GetComponentInParent<IInteractable>();

        string prompt = current != null ? current.GetPrompt() : null;
        bool show = !string.IsNullOrEmpty(prompt) && Cursor.lockState == CursorLockMode.Locked;

        if (promptRoot != null)
        {
            promptRoot.SetActive(show);
            if (show) promptText.text = "[" + KeyName(interactKey) + "] " + prompt;
        }

        if (current != null && Input.GetKeyDown(interactKey))
            current.Interact(transform);
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
