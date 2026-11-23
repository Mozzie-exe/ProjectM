using UnityEngine;
using UnityEngine.UI;

// Oyun içi arayüzleri koddan kurmak için küçük yardımcılar (PlayerInteractor ile aynı tarz).
public static class UiFactory
{
    static Font font, handFont;

    // Arayüzün genel yazısı: daktilo (Resources/Fonts/CourierPrime, OFL)
    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("Fonts/CourierPrime/CourierPrime-Regular");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // Karakterin kendi notları ve düşünceleri: el yazısı (Resources/Fonts/Caveat, OFL)
    public static Font HandFont
    {
        get
        {
            if (handFont == null) handFont = Resources.Load<Font>("Fonts/Caveat/Caveat-Variable");
            if (handFont == null) handFont = DefaultFont;
            return handFont;
        }
    }

    // El yazısı aynı punto değerinde daha küçük görünür
    public const float HandScale = 1.3f;
    public static void UseHand(Text t)
    {
        t.font = HandFont;
        t.fontStyle = FontStyle.Normal;
        t.fontSize = Mathf.RoundToInt(t.fontSize * HandScale);
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(string name, Transform parent, int size, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = DefaultFont;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = new Color(0.92f, 0.9f, 0.85f);
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }
}
