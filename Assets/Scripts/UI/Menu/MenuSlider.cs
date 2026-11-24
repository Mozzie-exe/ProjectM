using UnityEngine;
using UnityEngine.UI;

// İnce çizgi şeklinde, yazı temalı kaydırıcı. Değer değişince tık sesi çalar.
public static class MenuSlider
{
    public static Slider Create(Transform parent, Vector2 pos, float width, float min, float max, float value)
    {
        var root = new GameObject("Slider", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
        root.pivot = new Vector2(0f, 0.5f);
        root.anchoredPosition = pos;
        root.sizeDelta = new Vector2(width, 30f);

        var bg = Img("Line", root, new Color(1f, 1f, 1f, 0.18f));
        Stretch(bg.rectTransform, 13f, 13f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(root, false);
        Stretch(fillArea, 13f, 13f);
        var fill = Img("Fill", fillArea, new Color(0.92f, 0.92f, 0.9f, 0.85f));
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

        var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(root, false);
        Stretch(handleArea, 0f, 0f);
        var handle = Img("Handle", handleArea, new Color(0.95f, 0.95f, 0.92f, 1f));
        handle.rectTransform.sizeDelta = new Vector2(6f, 22f);

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.value = value;
        var colors = s.colors;
        colors.normalColor = new Color(0.6f, 0.6f, 0.6f);
        colors.highlightedColor = colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        s.colors = colors;
        return s;
    }

    static Image Img(string n, Transform p, Color c)
    {
        var img = new GameObject(n, typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(p, false);
        img.color = c;
        return img;
    }

    static void Stretch(RectTransform r, float top, float bottom)
    {
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 1f);
        r.offsetMin = new Vector2(0f, bottom);
        r.offsetMax = new Vector2(0f, -top);
    }
}
