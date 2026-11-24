using UnityEngine;
using UnityEngine.UI;

// Yazı efektleri: seçiliyken beyaz ve hafif titreyen, değilken soluk gri. Ara sıra sinyal kopması gibi kısa titreme.
public class MenuText : MonoBehaviour
{
    public Text text;
    public Color idle = new Color(0.55f, 0.55f, 0.55f, 1f);
    public Color active = new Color(0.95f, 0.95f, 0.92f, 1f);
    public float jitter = 1.6f;

    public bool Highlighted { get; set; }

    RectTransform rt;
    Vector2 basePos;
    float glitch, nextGlitch;
    float h;

    void Awake()
    {
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
        nextGlitch = Random.Range(2f, 7f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        h = Mathf.MoveTowards(h, Highlighted ? 1f : 0f, dt * 6f);

        nextGlitch -= dt;
        if (nextGlitch <= 0f)
        {
            glitch = Random.Range(0.06f, 0.18f);
            nextGlitch = Random.Range(3f, 9f);
        }
        glitch = Mathf.Max(0f, glitch - dt);

        var c = Color.Lerp(idle, active, h);
        if (glitch > 0f) c.a *= Random.value < 0.5f ? 0.25f : 1f;
        text.color = c;

        float jx = h > 0.5f || glitch > 0f ? (Mathf.PerlinNoise(Time.unscaledTime * 18f, basePos.y) - 0.5f) * jitter * 2f : 0f;
        rt.anchoredPosition = basePos + new Vector2(h * 14f + jx, 0f);
    }
}
