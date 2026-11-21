using UnityEngine;

// Gaz lambası titremesi: ışıkların şiddeti ve parlayan yüzeylerin (pencere ışığı) rengi yavaşça dalgalanır.
// Kurulum: Tools > Level > Build House Lights (HouseLightsBuilder).
public class LampFlicker : MonoBehaviour
{
    public Light[] lights;
    [Tooltip("Rengi titreşen parlak yüzeyler (URP Unlit, _BaseColor)")]
    public Renderer[] glows;
    public Color glowColor = new Color(2.2f, 1.2f, 0.45f, 1f);
    [Range(0f, 1f)] public float amount = 0.18f;
    public float speed = 1.6f;

    float[] baseIntensity;
    MaterialPropertyBlock mpb;
    float seed;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (lights == null) lights = new Light[0];
        if (glows == null) glows = new Renderer[0];
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            if (lights[i]) baseIntensity[i] = lights[i].intensity;
        mpb = new MaterialPropertyBlock();
        seed = Random.value * 100f;
    }

    void Update()
    {
        float t = Time.time * speed;
        // Yavaş dalga + hızlı kıpırtı: alev gibi, ama göz yormayacak kadar az
        float n = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 3.7f, seed + 5f) * 0.3f;
        float k = 1f + (n - 0.5f) * 2f * amount;
        for (int i = 0; i < lights.Length; i++)
            if (lights[i]) lights[i].intensity = baseIntensity[i] * k;
        mpb.SetColor(BaseColor, glowColor * k);
        foreach (var r in glows)
            if (r) r.SetPropertyBlock(mpb);
    }
}
