using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ana menü ve duraklatma menüsünün ortak görünümü: yazı tabanlı liste, radyo frekans göstergesi,
// parazit katmanı, Ayarlar paneli (hassasiyet + ses) ve seçim/hover sesleri.
// Hangi tuşun ne yaptığına sahibi (MainMenu / PauseMenu) karar verir; bu sınıf sadece görünüm ve gezinme.
public class RadioMenuView : MonoBehaviour
{
    public static readonly Color Idle = new Color(0.55f, 0.55f, 0.55f);

    [Header("Görünüm")]
    public Font font;   // boşsa Resources/Fonts/CourierPrime/CourierPrime-Regular
    public RadioUiAudio radio;
    public int sortingOrder = 200;
    public bool leftShade = true;

    [Header("Ayarlar yazıları")]
    public string settingsLabel = "Settings";
    public string sensitivityLabel = "Sensitivity";
    public string soundLabel = "Sound";
    public string backLabel = "Back";
    public string hint = "W/S  select     Enter  confirm     Esc  back";

    public Canvas Canvas { get; private set; }
    public CanvasGroup Root { get; private set; }
    public CanvasGroup MainList { get; private set; }
    public MenuStatic Static { get; private set; }
    public Image Black { get; private set; }
    public bool InSettings { get; private set; }
    public bool Busy { get; set; }   // geçiş sırasında girdi alma

    CanvasGroup settingsGroup;
    Text tuner, sensValue, volValue, sensLabelText, volLabelText;
    Slider sensSlider, volSlider;
    GameObject firstMain, firstSettings, lastSelected;
    readonly Dictionary<GameObject, MenuText> texts = new Dictionary<GameObject, MenuText>();

    // ------------------------------------------------------------------ kurulum
    public void Build(string canvasName, float tunerY)
    {
        if (font == null) font = Resources.Load<Font>("Fonts/CourierPrime/CourierPrime-Regular");
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject(canvasName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas = go.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        Root = go.AddComponent<CanvasGroup>();

        if (leftShade)
        {
            var shade = NewImage("Shade", go.transform, Color.white);
            Full(shade.rectTransform);
            shade.sprite = Sprite.Create(GradientTex(), new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
            shade.raycastTarget = false;
        }

        var st = new GameObject("Static", typeof(RectTransform));
        st.transform.SetParent(go.transform, false);
        Full((RectTransform)st.transform);
        st.AddComponent<RawImage>();
        Static = st.AddComponent<MenuStatic>();

        tuner = NewText("Tuner", go.transform, "", 22, new Vector2(164f, tunerY));
        tuner.color = new Color(1f, 1f, 1f, 0.35f);

        MainList = Group("Main", go.transform);
        BuildSettings(go.transform);

        var h = NewText("Hint", go.transform, hint, 18, new Vector2(164f, -440f));
        h.color = new Color(1f, 1f, 1f, 0.22f);
        h.rectTransform.sizeDelta = new Vector2(1200f, 40f);

        Black = NewImage("Black", go.transform, Color.clear);
        Full(Black.rectTransform);
        Black.raycastTarget = false;
    }

    void BuildSettings(Transform parent)
    {
        settingsGroup = Group("Settings", parent);
        var head = NewText("Header", settingsGroup.transform, settingsLabel.ToUpperInvariant(), 26, new Vector2(164f, 170f));
        head.color = new Color(1f, 1f, 1f, 0.4f);

        sensLabelText = NewText("SensLabel", settingsGroup.transform, sensitivityLabel, 40, new Vector2(160f, 110f));
        sensSlider = MenuSlider.Create(settingsGroup.transform, new Vector2(170f, 60f), 520f, GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.Sensitivity);
        sensValue = NewText("SensValue", settingsGroup.transform, "", 30, new Vector2(720f, 60f));
        sensSlider.onValueChanged.AddListener(v => { GameSettings.Sensitivity = v; sensValue.text = Num(v); radio?.Tick(); });
        sensValue.text = Num(sensSlider.value);

        volLabelText = NewText("VolLabel", settingsGroup.transform, soundLabel, 40, new Vector2(160f, -10f));
        volSlider = MenuSlider.Create(settingsGroup.transform, new Vector2(170f, -60f), 520f, 0f, 1f, GameSettings.Volume);
        volValue = NewText("VolValue", settingsGroup.transform, "", 30, new Vector2(720f, -60f));
        volSlider.onValueChanged.AddListener(v => { GameSettings.Volume = v; volValue.text = Mathf.RoundToInt(v * 100f) + "%"; radio?.Tick(); });
        volValue.text = Mathf.RoundToInt(volSlider.value * 100f) + "%";

        var back = AddItem(settingsGroup, backLabel, -150f, () => ShowSettings(false));
        firstSettings = sensSlider.gameObject;
        AddHover(sensSlider.gameObject);
        AddHover(volSlider.gameObject);
        Chain(sensSlider, volSlider, back.GetComponent<Selectable>());
    }

    static string Num(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

    // Ana listeye yazı tabanlı bir seçenek ekler. İlk eklenen, liste açılınca seçili gelir.
    public GameObject AddItem(string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        var go = AddItem(MainList, label, y, onClick);
        if (firstMain == null) firstMain = go;
        return go;
    }

    GameObject AddItem(CanvasGroup group, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        var t = NewText(label, group.transform, label, 52, new Vector2(160f, y));
        t.raycastTarget = true;
        var b = t.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (!Busy) { radio?.Click(); onClick(); } });
        var mt = t.gameObject.AddComponent<MenuText>();
        mt.text = t;
        texts[t.gameObject] = mt;
        AddHover(t.gameObject);
        return t.gameObject;
    }

    public Text AddCaption(string s, float y)
    {
        var t = NewText("Caption", MainList.transform, s, 26, new Vector2(164f, y));
        t.color = new Color(1f, 1f, 1f, 0.4f);
        return t;
    }

    void AddHover(GameObject go)
    {
        var trig = go.AddComponent<EventTrigger>();
        var e = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        e.callback.AddListener(_ => { if (!Busy && EventSystem.current) EventSystem.current.SetSelectedGameObject(go); });
        trig.triggers.Add(e);
    }

    // ------------------------------------------------------------------ durum
    public void ShowSettings(bool show)
    {
        InSettings = show;
        Set(MainList, !show);
        Set(settingsGroup, show);
        SelectFirst();
        if (!show) GameSettings.Save();
    }

    public void SelectFirst()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(InSettings ? firstSettings : firstMain);
        lastSelected = null;   // ilk seçimde hover sesi çalmasın
    }

    public void SetVisible(bool on)
    {
        Canvas.gameObject.SetActive(on);
        if (on) { ShowSettings(false); RefreshHighlight(); }
        else if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    void Update()
    {
        if (Canvas == null || !Canvas.gameObject.activeInHierarchy || Busy) return;

        var es = EventSystem.current;
        var sel = es ? es.currentSelectedGameObject : null;
        // fare boşluğa tıklayınca seçim kaybolur; klavyeye dokununca geri gelsin
        if (sel == null && es != null && Input.anyKeyDown && !Input.GetMouseButtonDown(0)) SelectFirst();
        if (sel != lastSelected)
        {
            if (sel != null && lastSelected != null) radio?.Hover();
            lastSelected = sel;
            RefreshHighlight();
        }

        if (tuner != null)
        {
            float f = 87.5f + Mathf.PerlinNoise(Time.unscaledTime * 0.07f, 3.1f) * 20.5f;
            tuner.text = f.ToString("0.0", CultureInfo.InvariantCulture) + " MHz" + (Random.value < 0.02f ? "  ..." : "");
        }
    }

    void RefreshHighlight()
    {
        var sel = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        foreach (var kv in texts) kv.Value.Highlighted = kv.Key == sel;
        sensLabelText.color = sel == sensSlider.gameObject ? Color.white : Idle;
        volLabelText.color = sel == volSlider.gameObject ? Color.white : Idle;
    }

    // ------------------------------------------------------------------ yardımcılar
    static void Chain(params Selectable[] list)
    {
        for (int i = 0; i < list.Length; i++)
        {
            var n = new Navigation { mode = Navigation.Mode.Explicit };
            n.selectOnUp = list[(i - 1 + list.Length) % list.Length];
            n.selectOnDown = list[(i + 1) % list.Length];
            list[i].navigation = n;
        }
    }

    static void Set(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    static CanvasGroup Group(string n, Transform parent)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        Full((RectTransform)g.transform);
        return g.AddComponent<CanvasGroup>();
    }

    public Text NewText(string n, Transform parent, string s, int size, Vector2 pos)
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
        t.color = Idle;
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
}
