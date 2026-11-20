using System.Collections.Generic;
using UnityEngine;

// Kırılabilir cam. Oyuncu bakınca "[E] Camı Kır" yazar, E'ye basınca cam kırılır,
// parçalar oyuncudan uzağa doğru dağılır ve birkaç saniye sonra yok olur.
// Kurulum: bir Cube'a (ince cam paneli) eklenir; Collider ve Renderer gerekir.
[RequireComponent(typeof(Collider))]
public class BreakableGlass : MonoBehaviour, IInteractable
{
    [Header("Yazı")]
    public string breakText = "Camı Kır";

    [Header("Parçalar")]
    [Range(4, 40)] public int shardCount = 18;
    public float shardLifetime = 6f;
    public float pushForce = 2.5f;

    [Tooltip("Açık kalırsa cam kırıldıktan sonra da pencereden geçilemez (görünmez engel kalır).")]
    public bool keepBlocker = true;

    [Header("Ses (boş bırakılırsa otomatik kırılma sesi üretilir)")]
    public AudioClip breakSound;
    [Range(0f, 1f)] public float volume = 0.9f;

    bool broken;
    static AudioClip generatedClip;

    public bool IsBroken => broken;

    public string GetPrompt() => broken ? null : breakText;

    public void Interact(Transform interactor)
    {
        if (broken) return;
        Break(interactor != null ? interactor.position : transform.position - transform.forward);
    }

    public void Break(Vector3 fromPosition)
    {
        if (broken) return;
        broken = true;

        var rend = GetComponent<Renderer>();
        var myCollider = GetComponent<Collider>();
        Material mat = rend != null ? rend.sharedMaterial : null;

        Vector3 size = transform.lossyScale; // birim küp varsayılır
        float w = Mathf.Abs(size.x), h = Mathf.Abs(size.y), t = Mathf.Max(Mathf.Abs(size.z), 0.01f);
        Vector3 right = transform.right, up = transform.up, fwd = transform.forward;
        Vector3 away = Vector3.Dot(transform.position - fromPosition, fwd) >= 0f ? fwd : -fwd;

        var shardColliders = new List<Collider>(shardCount);
        for (int i = 0; i < shardCount; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "CamKirigi";
            s.layer = 2; // Ignore Raycast: etkileşim ışınını engellemesin

            float sw = Random.Range(0.10f, 0.28f) * w;
            float sh = Random.Range(0.08f, 0.24f) * h;
            s.transform.position = transform.position
                                   + right * Random.Range(-0.42f, 0.42f) * w
                                   + up * Random.Range(-0.42f, 0.42f) * h;
            s.transform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            s.transform.localScale = new Vector3(sw, sh, t);

            var r = s.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var c = s.GetComponent<Collider>();
            if (myCollider != null) Physics.IgnoreCollision(c, myCollider);
            foreach (var other in shardColliders) Physics.IgnoreCollision(c, other);
            shardColliders.Add(c);

            var rb = s.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.AddForce(away * Random.Range(0.4f, 1f) * pushForce + Random.insideUnitSphere * 0.8f, ForceMode.VelocityChange);
            rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.VelocityChange);

            Destroy(s, shardLifetime + Random.Range(0f, 2f));
        }

        if (rend != null) rend.enabled = false;
        if (!keepBlocker && myCollider != null) myCollider.enabled = false;

        AudioClip clip = breakSound != null ? breakSound : GetGeneratedClip();
        if (clip != null) GameAudio.PlayAt(clip, transform.position, volume, AudioChannel.SFX);
    }

    // Ses dosyası yoksa: kısa bir çatlama + dağılan cam şıngırtıları
    static AudioClip GetGeneratedClip()
    {
        if (generatedClip != null) return generatedClip;

        const int rate = 44100;
        int len = (int)(rate * 1.1f);
        var data = new float[len];
        var rng = new System.Random(1234);

        // İlk çatlama (filtrelenmiş gürültü patlaması)
        float prev = 0f;
        for (int i = 0; i < len; i++)
        {
            float tt = i / (float)rate;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            float hp = n - prev; prev = n; // tiz gürültü
            data[i] += hp * Mathf.Exp(-tt * 28f) * 0.9f;
        }

        // Şıngırtılar
        for (int k = 0; k < 26; k++)
        {
            float start = (float)(rng.NextDouble() * 0.75);
            float freq = 2500f + (float)rng.NextDouble() * 6000f;
            float amp = 0.12f + (float)rng.NextDouble() * 0.2f;
            float decay = 18f + (float)rng.NextDouble() * 30f;
            int s0 = (int)(start * rate);
            for (int i = s0; i < len; i++)
            {
                float dt = (i - s0) / (float)rate;
                float env = Mathf.Exp(-dt * decay);
                if (env < 0.001f) break;
                data[i] += Mathf.Sin(2f * Mathf.PI * freq * dt) * env * amp;
            }
        }

        float max = 0f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max > 0f) for (int i = 0; i < len; i++) data[i] = data[i] / max * 0.9f;

        generatedClip = AudioClip.Create("GlassBreak_Generated", len, 1, rate, false);
        generatedClip.SetData(data, 0);
        return generatedClip;
    }
}
