using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ana menü: Başla / Ayarlar / Çık. Oyunla aynı sahnede durur; Başla'ya basınca parazitle kararır
// ve giriş sinematiğini (araba) başlatır. Arayüz koddan kurulur (PlayerInteractor ile aynı tarz).
public class MainMenu : MonoBehaviour
{
    [Header("Bağlantılar")]
    public IntroCinematic intro;
    public MenuBackdrop backdrop;
    public RadioUiAudio radio;
    public Font font;   // boşsa Resources/Fonts/CourierPrime/CourierPrime-Regular

    [Header("Yazılar")]
    public string gameTitle = "";
    public string startLabel = "Start";
    public string settingsLabel = "Settings";
    public string quitLabel = "Quit";
    public string sensitivityLabel = "Sensitivity";
    public string soundLabel = "Sound";
    public string backLabel = "Back";

    [Header("Geçiş")]
    public float transitionTime = 1.9f;
    [Range(0f, 1f)] public float ambienceHushInMenu = 0.55f;

    Canvas canvas;
    CanvasGroup mainGroup, settingsGroup, rootGroup;
    Image black;
    MenuStatic staticFx;
    Text tuner;
    readonly Dictionary<GameObject, MenuText> texts = new Dictionary<GameObject, MenuText>();
    GameObject firstMain, firstSettings, lastSelected;
    Slider sensSlider, volSlider;
    Text sensValue, volValue, sensLabelText, volLabelText;
    bool inSettings, starting;

    void Start()
    {
        if (font == null) font = Resources.Load<Font>("Fonts/CourierPrime/CourierPrime-Regular");
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        GameSettings.ApplyAll();
        Build();
        ShowSettings(false, true);
        if (intro != null && intro.ambience != null)
            foreach (var a in intro.ambience) if (a) a.hush = ambienceHushInMenu;
        StartCoroutine(FadeFromBlack());
    }

    void Update()
    {
        if (starting) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var es = EventSystem.current;
        var sel = es ? es.currentSelectedGameObject : null;
        if (sel == null && es != null)
        {
            // fare boşluğa tıklayınca seçim kaybolmasın; klavye ile devam edilebilsin
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0)) es.SetSelectedGameObject(inSettings ? firstSettings : firstMain);
        }
        if (sel != lastSelected)
        {
            foreach (var kv in texts) kv.Value.Highlighted = kv.Key == sel;
            if (sel != null && lastSelected != null) radio?.Hover();
            lastSelected = sel;
            if (sensLabelText) sensLabelText.color = sel == sensSlider.gameObject ? Color.white : new Color(0.55f, 0.55f, 0.55f);
            if (volLabelText) volLabelText.color = sel == volSlider.gameObject ? Color.white : new Color(0.55f, 0.55f, 0.55f);
        }

        if (inSettings && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
            ShowSettings(false, false);

        if (tuner != null)
        {
            float f = 87.5f + Mathf.PerlinNoise(Time.unscaledTime * 0.07f, 3.1f) * 20.5f;
            tuner.text = f.ToString("0.0", CultureInfo.InvariantCulture) + " MHz" + (Random.value < 0.02f ? "  ..." : "");
        }
    }

    // ------------------------------------------------------------------ kurulum
    void Build()
    {
        var go = new GameObject("MainMenuUI", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        rootGroup = go.AddComponent<CanvasGroup>();

        // sol taraf okunurluk için soldan sağa kararma
        var shade = NewImage("Shade", go.transform, Color.white);
        Full(shade.rectTransform);
        shade.sprite = Sprite.Create(GradientTex(), new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
        shade.raycastTarget = false;

        var st = new GameObject("Static", typeof(RectTransform));
        st.transform.SetParent(go.transform, false);
        Full((RectTransform)st.transform);
        st.AddComponent<RawImage>();
        staticFx = st.AddComponent<MenuStatic>();

        if (!string.IsNullOrEmpty(gameTitle))
        {
            var title = NewText("Title", go.transform, gameTitle, 84, new Vector2(160f, 300f));
            title.color = new Color(0.92f, 0.92f, 0.9f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 120f);
        }
        tuner = NewText("Tuner", go.transform, "", 22, new Vector2(164f, gameTitle.Length > 0 ? 225f : 250f));
        tuner.color = new Color(1f, 1f, 1f, 0.35f);

        // ana liste
        mainGroup = Group("Main", go.transform);
        var start = Item(mainGroup.transform, startLabel, 120f, OnStart);
        Item(mainGroup.transform, settingsLabel, 50f, () => ShowSettings(true, false));
        Item(mainGroup.transform, quitLabel, -20f, OnQuit);
        firstMain = start;

        // ayarlar
        settingsGroup = Group("Settings", go.transform);
        var head = NewText("Header", settingsGroup.transform, settingsLabel.ToUpperInvariant(), 26, new Vector2(164f, 170f));
        head.color = new Color(1f, 1f, 1f, 0.4f);

        sensLabelText = NewText("SensLabel", settingsGroup.transform, sensitivityLabel, 40, new Vector2(160f, 110f));
        sensSlider = MenuSlider.Create(settingsGroup.transform, new Vector2(170f, 60f), 520f, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.Sensitivity);
        sensValue = NewText("SensValue", settingsGroup.transform, "", 30, new Vector2(720f, 60f));
        sensSlider.onValueChanged.AddListener(v => { GameSettings.Sensitivity = v; sensValue.text = v.ToString("0.0", CultureInfo.InvariantCulture); radio?.Tick(); });
        sensValue.text = sensSlider.value.ToString("0.0", CultureInfo.InvariantCulture);

        volLabelText = NewText("VolLabel", settingsGroup.transform, soundLabel, 40, new Vector2(160f, -10f));
        volSlider = MenuSlider.Create(settingsGroup.transform, new Vector2(170f, -60f), 520f, 0f, 1f, GameSettings.Volume);
        volValue = NewText("VolValue", settingsGroup.transform, "", 30, new Vector2(720f, -60f));
        volSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; volValue.text = Mathf.RoundToInt(v * 100f) + "%"; radio?.Tick(); });
        volValue.text = Mathf.RoundToInt(volSlider.value * 100f) + "%";

        var back = Item(settingsGroup.transform, backLabel, -150f, () => ShowSettings(false, false));
        firstSettings = sensSlider.gameObject;
        foreach (var s in new[] { sensSlider, volSlider }) AddHover(s.gameObject);

        // yukarı/aşağı gezinme sırası
        Chain(sensSlider, volSlider, back.GetComponent<Selectable>());

        var hint = NewText("Hint", go.transform, "W/S  select     Enter  confirm     Esc  back", 18, new Vector2(164f, -440f));
        hint.color = new Color(1f, 1f, 1f, 0.22f);
        hint.rectTransform.sizeDelta = new Vector2(1200f, 40f);

        black = NewImage("Black", go.transform, Color.black);
        Full(black.rectTransform);
        black.raycastTarget = false;
    }

    static void Chain(params Selectable[] list)
    {
        for (int i = 0; i < list.Length; i++)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnUp = list[(i - 1 + list.Length) % list.Length];
            n.selectOnDown = list[(i + 1) % list.Length];
            if (list[i] is Slider) { n.selectOnLeft = null; n.selectOnRight = null; }
            list[i].navigation = n;
        }
    }

    GameObject Item(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        var t = NewText(label, parent, label, 52, new Vector2(160f, y));
        t.raycastTarget = true;
        var b = t.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (!starting) { radio?.Click(); onClick(); } });
        var mt = t.gameObject.AddComponent<MenuText>();
        mt.text = t;
        texts[t.gameObject] = mt;
        AddHover(t.gameObject);
        return t.gameObject;
    }

    void AddHover(GameObject go)
    {
        var trig = go.AddComponent<EventTrigger>();
        var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => { if (!starting && EventSystem.current) EventSystem.current.SetSelectedGameObject(go); });
        trig.triggers.Add(e);
    }

    CanvasGroup Group(string n, Transform parent)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Full((RectTransform)g.transform);
        return g.AddComponent<CanvasGroup>();
    }

    Text NewText(string n, Transform parent, string s, int size, Vector2 pos)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(700f, size * 1.6f);
        var t = g.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.text = s;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.color = new Color(0.55f, 0.55f, 0.55f);
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(string n, Transform parent, Color c)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var i = g.AddComponent<Image>();
        i.color = c;
        return i;
    }

    static void Full(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Texture2D GradientTex()
    {
        var t = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++)
        {
            float u = x / 255f;
            float a = Mathf.Lerp(0.88f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.62f, u)));
            t.SetPixel(x, 0, new Color(0f, 0f, 0f, a));
        }
        t.Apply();
        return t;
    }

    // ------------------------------------------------------------------ akış
    void ShowSettings(bool show, bool instant)
    {
        inSettings = show;
        Set(mainGroup, !show);
        Set(settingsGroup, show);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(show ? firstSettings : firstMain);
        if (!show) GameSettings.Save();
    }

    static void Set(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    IEnumerator FadeFromBlack()
    {
        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.unscaledDeltaTime;
            black.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t / 1.4f));
            yield return null;
        }
        black.color = Color.clear;
    }

    void OnStart()
    {
        if (starting) return;
        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        starting = true;
        GameSettings.Save();
        rootGroup.interactable = false;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        radio?.Transition();
        radio?.SetStaticLevel(1.6f);

        float t = 0f;
        while (t < transitionTime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / transitionTime;
            mainGroup.alpha = 1f - Mathf.Clamp01(k * 1.6f);
            staticFx.SetBoost(Mathf.Sin(k * Mathf.PI) * 0.35f + k * 0.15f);
            black.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k)));
            yield return null;
        }
        black.color = Color.black;

        if (backdrop) backdrop.Shutdown();
        radio?.SetStaticLevel(0f);
        if (intro) intro.Begin();          // sinematik kendi karartmasından açılır

        yield return new WaitForSecondsRealtime(0.25f);
        Destroy(canvas.gameObject);
        yield return new WaitForSecondsRealtime(2f);
        Destroy(gameObject);
    }

    void OnQuit()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
