using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Oyun sonu credits'i. EndCinematic bitince (çarpmadan 1.6 sn sonra, ekran zaten siyah) başlar.
// Ana menüyle aynı görünüm: siyah, radyo paraziti, soldan gölge, Courier Prime, gri yazılar.
// Akış: vaka dosyası kartı daktiloyla yazılır -> isimler yukarı kayar -> son satırda bekler -> kararır ->
// sahne yeniden yüklenir (ana menü). Esc basılı tutulursa geçilir.
// Metin: Resources/Credits/Credits.txt (biçim CreditsMath'te). Kurulum: Tools > End Cinematic > Build.
public class CreditsScreen : MonoBehaviour
{
    public EndCinematic ending;
    public TextAsset content;   // boşsa Resources/Credits/Credits
    public Font font;           // boşsa Courier Prime

    [Header("Müzik")]
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.85f;
    public float musicFadeIn = 2f;

    [Header("Zamanlama (sn)")]
    public float startDelay = 0.4f;
    public float typeSpeed = 24f;        // harf/sn
    public float linePause = 0.45f;
    public float cardHold = 3f, cardFade = 1.2f;
    public float endHold = 4.5f, fadeOut = 2.5f, minScroll = 25f;

    [Header("Geç")]
    public KeyCode skipKey = KeyCode.Escape;
    public float holdToSkip = 1.2f;
    public string skipHint = "Hold Esc  skip";

    const float X = 160f;
    static readonly Color Dim = new Color(1f, 1f, 1f, 0.4f);
    static readonly Color NameColor = new Color(0.8f, 0.8f, 0.78f);

    Canvas canvas;
    RectTransform scroll;
    Image cover;
    Text tuner, hint;
    MenuStatic noise;
    AudioSource source;
    readonly List<(Text t, float baseA)> scrollTexts = new List<(Text, float)>();
    bool running, skipping;
    float skipHeld;
    Coroutine coverFade;

    void OnEnable() { if (ending) ending.Ended += Begin; }
    void OnDisable() { if (ending) ending.Ended -= Begin; }

    public void Begin()
    {
        if (running) return;
        running = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        var data = CreditsMath.Parse(content ? content.text : Resources.Load<TextAsset>("Credits/Credits")?.text);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BuildUi();
        yield return Wait(startDelay);

        float clock = 0f;   // müziğin başından beri
        StartMusic();
        coverFade = StartCoroutine(Fade(cover, 1f, 0f, 1.2f));

        // 1) vaka dosyası
        if (data.Card.Count > 0)
        {
            var card = Group("Card");
            float y = 40f + data.Card.Count * 30f;
            var lines = new List<Text>();
            foreach (var s in data.Card) { lines.Add(NewText(card.transform, "", 34, new Vector2(X, y), NameColor)); y -= 58f; }
            for (int i = 0; i < data.Card.Count && !skipping; i++)
            {
                string s = data.Card[i];
                float t = 0f;
                while (!skipping && CreditsMath.TypedChars(t, typeSpeed, s.Length) < s.Length)
                {
                    lines[i].text = s.Substring(0, CreditsMath.TypedChars(t, typeSpeed, s.Length));
                    t += Time.unscaledDeltaTime; clock += Time.unscaledDeltaTime;
                    yield return null;
                }
                lines[i].text = s;
                float p = 0f;
                while (!skipping && p < linePause) { p += Time.unscaledDeltaTime; clock += Time.unscaledDeltaTime; yield return null; }
            }
            float h = 0f;
            while (!skipping && h < cardHold) { h += Time.unscaledDeltaTime; clock += Time.unscaledDeltaTime; yield return null; }
            float f = 0f;
            while (!skipping && f < cardFade)
            {
                f += Time.unscaledDeltaTime; clock += Time.unscaledDeltaTime;
                card.alpha = 1f - Mathf.SmoothStep(0f, 1f, f / cardFade);
                yield return null;
            }
            card.alpha = 0f;
        }

        // 2) kayan isimler
        if (!skipping && data.Lines.Count > 0)
        {
            float contentH = Layout(data.Lines, out float lastH);
            float viewH = ((RectTransform)canvas.transform).rect.height;
            float dur = CreditsMath.ScrollDuration(music ? music.length : 60f, clock, endHold, fadeOut, minScroll);
            float t = 0f;
            while (!skipping && t < dur)
            {
                t += Time.unscaledDeltaTime;
                scroll.anchoredPosition = new Vector2(0f, CreditsMath.ScrollOffset(t, dur, contentH - lastH * 0.5f, viewH));
                EdgeFade(viewH);
                yield return null;
            }
            float hold = 0f;
            while (!skipping && hold < endHold) { hold += Time.unscaledDeltaTime; yield return null; }
        }

        // 3) kararma ve ana menü
        yield return FadeOutAndReturn(skipping ? 1.2f : fadeOut);
    }

    void Update()
    {
        if (!running || canvas == null) return;
        if (tuner)
        {
            float f = 87.5f + Mathf.PerlinNoise(Time.unscaledTime * 0.07f, 3.1f) * 20.5f;
            tuner.text = f.ToString("0.0", CultureInfo.InvariantCulture) + " MHz";
        }
        if (skipping) return;
        skipHeld = Input.GetKey(skipKey) ? skipHeld + Time.unscaledDeltaTime : 0f;
        if (hint) hint.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.22f, 0.85f, skipHeld / holdToSkip));
        if (skipHeld >= holdToSkip) skipping = true;
    }

    // ------------------------------------------------------------------ ses
    void StartMusic()
    {
        RestoreChannel(AudioChannel.Music);   // EndCinematic müziği ve diğer kanalları susturmuştu
        if (!music) return;
        source = gameObject.AddComponent<AudioSource>();
        source.clip = music;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = GameAudio.Group(AudioChannel.Music);
        source.Play();
        StartCoroutine(Ramp(0f, musicVolume, musicFadeIn));
    }

    IEnumerator Ramp(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur && source)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        if (source) source.volume = to;
    }

    static void RestoreChannel(AudioChannel id)
    {
        var s = GameAudio.Settings;
        if (!s || !s.mixer || s.channels == null) return;
        foreach (var c in s.channels)
            if (c != null && c.id == id && !string.IsNullOrEmpty(c.param))
                s.mixer.SetFloat(c.param, GameAudioSettings.LinearToDb(c.level));
    }

    IEnumerator FadeOutAndReturn(float dur)
    {
        if (coverFade != null) StopCoroutine(coverFade);
        float a0 = cover.color.a;
        float from = source ? source.volume : 0f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            cover.color = new Color(0f, 0f, 0f, Mathf.Lerp(a0, 1f, Mathf.SmoothStep(0f, 1f, k)));
            if (source) source.volume = from * (1f - k);
            if (noise) noise.SetBoost(Mathf.Sin(k * Mathf.PI) * 0.1f);
            yield return null;
        }
        yield return Wait(0.6f);
        ReturnToMenu();
    }

    // Sahne baştan yüklenir: ana menü açılır. Susturulan kanallar ve oyuncu kilidi yeni sahnede eski haline döner.
    static void ReturnToMenu()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        PlayerControlLock.ResetState();
        SceneManager.sceneLoaded += RestoreAudioAfterLoad;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static void RestoreAudioAfterLoad(Scene s, LoadSceneMode m)
    {
        SceneManager.sceneLoaded -= RestoreAudioAfterLoad;
        GameAudio.Settings?.ApplyAll();
        GameSettings.ApplyAll();
    }

    // ------------------------------------------------------------------ görünüm
    void BuildUi()
    {
        if (font == null) font = Resources.Load<Font>("Fonts/CourierPrime/CourierPrime-Regular");
        var go = new GameObject("CreditsUI", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Full(NewImage("Background", go.transform, Color.black).rectTransform);

        var shade = NewImage("Shade", go.transform, Color.white);
        Full(shade.rectTransform);
        shade.sprite = Sprite.Create(ShadeTex(), new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));

        var holder = new GameObject("Scroll", typeof(RectTransform));
        holder.transform.SetParent(go.transform, false);
        scroll = (RectTransform)holder.transform;
        scroll.anchorMin = scroll.anchorMax = new Vector2(0f, 0f);
        scroll.pivot = new Vector2(0f, 1f);
        scroll.sizeDelta = new Vector2(1400f, 10f);
        scroll.anchoredPosition = Vector2.zero;

        var st = new GameObject("Static", typeof(RectTransform));
        st.transform.SetParent(go.transform, false);
        Full((RectTransform)st.transform);
        var raw = st.AddComponent<RawImage>();
        raw.raycastTarget = false;
        noise = st.AddComponent<MenuStatic>();
        noise.alpha = 0.006f;   // menüde sahnenin üstünde; burada saf siyahın üstünde daha belirgin

        tuner = NewText(go.transform, "", 22, new Vector2(164f, 450f), new Color(1f, 1f, 1f, 0.35f));
        hint = NewText(go.transform, skipHint, 18, new Vector2(164f, -440f), new Color(1f, 1f, 1f, 0.22f));

        cover = NewImage("Cover", go.transform, Color.black);
        Full(cover.rectTransform);
    }

    CanvasGroup Group(string n)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(canvas.transform, false);
        Full((RectTransform)g.transform);
        g.transform.SetSiblingIndex(cover.transform.GetSiblingIndex());   // kararmanın altında
        return g.AddComponent<CanvasGroup>();
    }

    // Satırları yukarıdan aşağı dizer; toplam yüksekliği ve son satırın yüksekliğini verir
    float Layout(List<CreditsLine> lines, out float lastH)
    {
        float y = 0f;
        lastH = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            float h;
            switch (l.kind)
            {
                case CreditsKind.Title:
                    if (i > 0) y -= 60f;
                    h = 110f; Add(l.text, 64, new Color(0.92f, 0.92f, 0.9f), y, h); break;
                case CreditsKind.Heading:
                    h = 48f; Add(l.text.ToUpper(new CultureInfo("tr-TR")), 24, Dim, y, h); break;
                case CreditsKind.Name:
                    h = 60f; Add(l.text, 40, NameColor, y, h); break;
                default:
                    h = 64f; break;
            }
            y -= h;
            lastH = h;
        }
        return -y;
    }

    void Add(string s, int size, Color c, float top, float h)
    {
        var t = NewText(scroll, s, size, Vector2.zero, c);
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(X, top);
        r.sizeDelta = new Vector2(1400f, h);
        scrollTexts.Add((t, c.a));
    }

    // Ekranın üst ve alt kenarına yaklaşan satırlar yumuşakça söner (alttaki ipucu ve üstteki frekans yazısının
    // üstünden geçmesinler)
    void EdgeFade(float viewH)
    {
        const float band = 160f, bottom = 150f, top = 150f;
        foreach (var (t, a) in scrollTexts)
        {
            var r = t.rectTransform;
            float mid = scroll.anchoredPosition.y + r.anchoredPosition.y - r.sizeDelta.y * 0.5f;   // ekran altından yükseklik
            float k = Mathf.Clamp01(Mathf.Min((mid - bottom) / band, (viewH - top - mid) / band));
            var c = t.color; c.a = a * k; t.color = c;
        }
    }

    Text NewText(Transform parent, string s, int size, Vector2 pos, Color c)
    {
        var g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(1400f, size * 1.6f);
        var t = g.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.text = s;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.color = c;
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(string n, Transform parent, Color c)
    {
        var g = new GameObject(n, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        var i = g.AddComponent<Image>();
        i.color = c;
        i.raycastTarget = false;
        return i;
    }

    static void Full(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // Ana menüdeki soldan gölgeyle aynı eğri
    static Texture2D ShadeTex()
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

    static IEnumerator Fade(Image img, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)));
            yield return null;
        }
        img.color = new Color(0f, 0f, 0f, to);
    }

    static IEnumerator Wait(float s)
    {
        float t = 0f;
        while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
