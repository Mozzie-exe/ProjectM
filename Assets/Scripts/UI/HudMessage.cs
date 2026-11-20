using UnityEngine;
using UnityEngine.UI;

// Ekranın altında kısa süreli yazı ("Bu anahtar uymuyor", "Kürek alındı" ...).
public class HudMessage : MonoBehaviour
{
    static HudMessage instance;

    Text text;
    CanvasGroup group;
    float hideAt;

    public static void Show(string message, float duration = 3f)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (instance == null) Create();
        instance.text.text = message;
        instance.hideAt = Time.unscaledTime + duration;
        instance.group.alpha = 1f;
    }

    static void Create()
    {
        var canvas = UiFactory.CreateCanvas("HudMessageUI", 110);
        instance = canvas.gameObject.AddComponent<HudMessage>();
        instance.group = canvas.gameObject.AddComponent<CanvasGroup>();
        instance.group.blocksRaycasts = false;
        instance.group.alpha = 0f;
        instance.text = UiFactory.Label("Message", canvas.transform, 32, TextAnchor.MiddleCenter,
            new Vector2(0.15f, 0.12f), new Vector2(0.85f, 0.2f), Vector2.zero, Vector2.zero);
        instance.text.fontStyle = FontStyle.Italic;
    }

    void Update()
    {
        if (Time.unscaledTime > hideAt)
            group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 1.5f);
    }
}
