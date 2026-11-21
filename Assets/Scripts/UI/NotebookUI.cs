using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tab (veya J) ile not defterini açar: solda bulunan ipuçları, sağda seçilenin görseli ve yorumu.
public class NotebookUI : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.Tab;
    public KeyCode altToggleKey = KeyCode.J;

    GameObject root;
    RectTransform listRoot;
    RawImage image;
    AspectRatioFitter fitter;
    Text detail;
    Text empty;
    readonly List<GameObject> rows = new List<GameObject>();
    bool open;

    void Start()
    {
        Build();
        if (Notebook.Instance != null) Notebook.Instance.EntryAdded += OnEntryAdded;
    }

    void OnDestroy()
    {
        if (Notebook.Instance != null) Notebook.Instance.EntryAdded -= OnEntryAdded;
        if (root != null) Destroy(root.transform.parent.gameObject);
    }

    void OnEntryAdded(Notebook.Entry e)
    {
        HudMessage.Show("Not defterine eklendi: " + e.title + "   [Tab]", 3.5f);
    }

    void Update()
    {
        if (ReadableView.IsOpen) return;
        if (!CanToggle(open, PlayerControlLock.IsLocked)) return;
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(altToggleKey)) SetOpen(!open);
        else if (open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
    }

    // Başka bir şey (sinematik, kapı bakışı) oyuncuyu kilitlemişken not defteri açılmaz; açıksa kapatılabilir
    public static bool CanToggle(bool open, bool locked) => open || !locked;

    void SetOpen(bool value)
    {
        if (open == value) return;
        open = value;
        root.SetActive(open);
        if (open)
        {
            PlayerControlLock.Push();
            Rebuild();
        }
        else PlayerControlLock.Pop();
    }

    void Build()
    {
        var canvas = UiFactory.CreateCanvas("NotebookUI", 115);
        root = UiFactory.Panel("Book", canvas.transform, new Color(0.08f, 0.07f, 0.06f, 0.95f), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero).gameObject;
        UiFactory.Label("Header", root.transform, 40, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.9f), new Vector2(0.6f, 0.98f), Vector2.zero, Vector2.zero).text = "Not Defteri";
        UiFactory.Label("Close", root.transform, 22, TextAnchor.MiddleRight, new Vector2(0.6f, 0.9f), new Vector2(0.97f, 0.98f), Vector2.zero, Vector2.zero).text = "[Tab] Kapat";

        listRoot = UiFactory.Rect("List", root.transform, new Vector2(0.03f, 0.05f), new Vector2(0.32f, 0.88f), Vector2.zero, Vector2.zero);
        var layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperLeft;

        var imgArea = UiFactory.Rect("ImageArea", root.transform, new Vector2(0.35f, 0.25f), new Vector2(0.97f, 0.88f), Vector2.zero, Vector2.zero);
        var imgRt = UiFactory.Rect("Image", imgArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        image = imgRt.gameObject.AddComponent<RawImage>();
        fitter = imgRt.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        detail = UiFactory.Label("Detail", root.transform, 26, TextAnchor.UpperLeft, new Vector2(0.35f, 0.04f), new Vector2(0.97f, 0.23f), Vector2.zero, Vector2.zero);
        detail.fontStyle = FontStyle.Italic;
        empty = UiFactory.Label("Empty", root.transform, 30, TextAnchor.MiddleCenter, new Vector2(0.35f, 0.3f), new Vector2(0.97f, 0.7f), Vector2.zero, Vector2.zero);
        empty.text = "Henüz bir şey bulmadım.";
        root.SetActive(false);
    }

    void Rebuild()
    {
        foreach (var r in rows) Destroy(r);
        rows.Clear();

        var entries = Notebook.Instance != null ? Notebook.Instance.Entries : null;
        int count = entries != null ? entries.Count : 0;
        empty.gameObject.SetActive(count == 0);
        image.enabled = false;
        detail.text = "";

        for (int i = 0; i < count; i++)
        {
            var e = entries[i];
            var btnImg = UiFactory.Panel("Row" + i, listRoot, new Color(0.2f, 0.17f, 0.14f, 0.9f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ((RectTransform)btnImg.transform).sizeDelta = new Vector2(0f, 56f);
            var btn = btnImg.gameObject.AddComponent<Button>();
            var label = UiFactory.Label("Text", btnImg.transform, 26, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-8f, 0f));
            label.text = (i + 1) + ". " + e.title;
            btn.onClick.AddListener(() => Select(e));
            rows.Add(btnImg.gameObject);
        }

        if (count > 0) Select(entries[count - 1]);
    }

    void Select(Notebook.Entry e)
    {
        image.texture = e.image;
        image.enabled = e.image != null;
        if (e.image != null) fitter.aspectRatio = (float)e.image.width / e.image.height;
        detail.text = string.IsNullOrEmpty(e.comment) ? e.text : e.comment;
    }
}
