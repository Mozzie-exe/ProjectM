using UnityEngine;

// Rüzgarda hafifçe oynayan, arada çarpan kapı (ahırın aralık sürgülü kapısı).
public class WindRattle : MonoBehaviour
{
    public Vector3 localAxis = Vector3.right;   // kayma yönü (sürgülü kapı) 
    public float amplitude = 0.12f;             // metre
    public float speed = 0.35f;
    public float gustChancePerSecond = 0.06f;
    public AudioClip bangSound;

    Vector3 basePos;
    float seed;
    float gust;

    void Start()
    {
        basePos = transform.localPosition;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
        if (gust <= 0f && Random.value < gustChancePerSecond * Time.deltaTime)
        {
            gust = 1f;
            if (bangSound != null) AudioSource.PlayClipAtPoint(bangSound, transform.position);
        }
        gust = Mathf.MoveTowards(gust, 0f, Time.deltaTime * 2.5f);
        float offset = n * amplitude - Mathf.Sin(gust * Mathf.PI) * amplitude * 1.5f;
        transform.localPosition = basePos + localAxis.normalized * offset;
    }
}
