using UnityEngine;
using UnityEngine.UI;

// Not, fotoğraf, harita gibi şeyleri ekranda büyük gösterir. E / Esc / sağ tık ile kapanır, R ile arkası çevrilir.
public class ReadableView : MonoBehaviour
{
    static ReadableView instance;

    public static bool IsOpen => instance != null && instance.root.activeSelf;

    GameObject root;
    RawImage image;
    AspectRatioFitter fitter;
    Text title;
    Text caption;
    Text hint;

    Notebook.Entry current;
    bool showingBack;
    int openedFrame;
    bool popNextFrame;

    public static void Show(Notebook.Entry entry)
    {
        if (entry == null) return;
        if (instance == null) Create();
        instance.Open(entry);
    }

    static void Create()
    {
        var canvas = UiFactory.CreateCanvas("ReadableUI", 120);
        instance = canvas.gameObject.AddComponent<ReadableView>();
        var v = instance;

        v.root = UiFactory.Panel("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

        var imgArea = UiFactory.Rect("ImageArea", v.root.transform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.9f), Vector2.zero, Vector2.zero);
        var imgRt = UiFactory.Rect("Image", imgArea, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        v.image = imgRt.gameObject.AddComponent<RawImage>();
        v.image.raycastTarget = false;
        v.fitter = imgRt.gameObject.AddComponent<AspectRatioFitter>();
        v.fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        v.title = UiFactory.Label("Title", v.root.transform, 34, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.91f), new Vector2(0.9f, 0.98f), Vector2.zero, Vector2.zero);
        v.caption = UiFactory.Label("Caption", v.root.transform, 28, TextAnchor.UpperCenter, new Vector2(0.15f, 0.07f), new Vector2(0.85f, 0.19f), Vector2.zero, Vector2.zero);
        UiFactory.UseHand(v.caption);
        v.hint = UiFactory.Label("Hint", v.root.transform, 22, TextAnchor.LowerCenter, new Vector2(0.1f, 0.01f), new Vector2(0.9f, 0.06f), Vector2.zero, Vector2.zero);
        v.hint.color = new Color(0.7f, 0.7f, 0.7f);
        v.root.SetActive(false);
    }

    void Open(Notebook.Entry entry)
    {
        bool wasOpen = root.activeSelf;
        current = entry;
        showingBack = false;
        Refresh();
        root.SetActive(true);
        openedFrame = Time.frameCount;
        if (!wasOpen) PlayerControlLock.Push();
    }

    void Refresh()
    {
        Texture2D tex = showingBack && current.backImage != null ? current.backImage : current.image;
        image.texture = tex;
        image.enabled = tex != null;
        if (tex != null) fitter.aspectRatio = (float)tex.width / tex.height;
        title.text = current.title;
        caption.text = string.IsNullOrEmpty(current.comment) ? current.text : current.comment;
        hint.text = current.backImage != null ? "[R] Çevir    [E] Kapat" : "[E] Kapat";
    }

    void Update()
    {
        if (popNextFrame)
        {
            popNextFrame = false;
            PlayerControlLock.Pop();
        }

        if (!root.activeSelf || Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.R) && current.backImage != null)
        {
            showingBack = !showingBack;
            Refresh();
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            root.SetActive(false);
            popNextFrame = true; // aynı karede E tekrar etkileşim başlatmasın
        }
    }
}
