using System.Collections.Generic;
using UnityEngine;

// Kapalı alan (ev, bodrum, kulübe, ahır...). Dinleyici kutunun içindeyken:
//  - odanın yankısı duyulur: AudioReverbZone küre şeklindedir, her karede dinleyiciye en yakın kutu noktasına
//    taşınarak kutuya uydurulur (içeride tam etki, dışarıda edgeFade metrede söner),
//  - dışarıdaki ortam sesleri (AmbienceLoop) kısılıp boğuklaşır,
//  - başka bölgede çalan 3D sesler (GameAudio.PlayAt) duvar arkasından gelir gibi boğuk duyulur.
// Fizik kullanmaz (trigger collider etkileşim ışınlarını engellemesin): kutu bu objenin konum ve dönüşünde,
// size dünya ölçüsünde (ebeveyn ölçeği etkilemez).
public class IndoorZone : MonoBehaviour
{
    public Vector3 size = new Vector3(4f, 3f, 4f);
    [Tooltip("Kutudan çıkarken etkinin söndüğü mesafe (m)")]
    public float edgeFade = 1.5f;
    [Tooltip("İçerideyken dışarıdaki seslerin boğulma miktarı")]
    [Range(0f, 1f)] public float outsideMuffle = 0.7f;
    public AudioReverbPreset reverb = AudioReverbPreset.Room;

    static readonly List<IndoorZone> zones = new List<IndoorZone>();
    AudioReverbZone rz;

    void OnEnable()
    {
        zones.Add(this);
        rz = GetComponentInChildren<AudioReverbZone>(true);
        if (!rz)
        {
            var go = new GameObject("Reverb");
            go.transform.SetParent(transform, false);
            rz = go.AddComponent<AudioReverbZone>();
        }
        rz.reverbPreset = reverb;
        rz.minDistance = 0.25f;
        rz.maxDistance = Mathf.Max(0.3f, edgeFade);
        rz.enabled = true;
    }

    void OnDisable()
    {
        zones.Remove(this);
        if (rz) rz.enabled = false;
    }

    void LateUpdate()
    {
        if (rz && GameAudio.TryListener(out Vector3 lp))
            rz.transform.position = ClosestPoint(lp);
    }

    public Vector3 ClosestPoint(Vector3 p)
    {
        Quaternion r = transform.rotation;
        Vector3 l = Quaternion.Inverse(r) * (p - transform.position), h = size * 0.5f;
        l = new Vector3(Mathf.Clamp(l.x, -h.x, h.x), Mathf.Clamp(l.y, -h.y, h.y), Mathf.Clamp(l.z, -h.z, h.z));
        return transform.position + r * l;
    }

    public bool Contains(Vector3 p) => (ClosestPoint(p) - p).sqrMagnitude < 1e-6f;

    // 1: içeride, 0: kutudan edgeFade kadar uzakta
    public float Weight(Vector3 p)
    {
        float d = Vector3.Distance(p, ClosestPoint(p));
        if (edgeFade <= 0f) return d > 0f ? 0f : 1f;
        return 1f - Mathf.Clamp01(d / edgeFade);
    }

    // Noktayı içeren en küçük bölge (bodrum evin içindeyse bodrum kazanır); dışarıdaysa null
    public static IndoorZone At(Vector3 p)
    {
        IndoorZone best = null;
        float vol = float.MaxValue;
        foreach (var z in zones)
        {
            float v = z.size.x * z.size.y * z.size.z;
            if (v < vol && z.Contains(p)) { best = z; vol = v; }
        }
        return best;
    }

    // Dinleyici buradayken dışarıdaki ortam sesi ne kadar boğuk (0..1)
    public static float MuffleAt(Vector3 p)
    {
        float m = 0f;
        foreach (var z in zones) m = Mathf.Max(m, z.outsideMuffle * z.Weight(p));
        return m;
    }

    // Kaynakla dinleyici arasında duvar var mı: farklı bölgedeyseler (biri dışarı olabilir) boğulma miktarı
    public static float Occlusion(Vector3 listener, Vector3 source)
    {
        IndoorZone a = At(listener), b = At(source);
        if (a == b) return 0f;
        return Mathf.Max(a ? a.outsideMuffle : 0f, b ? b.outsideMuffle : 0f);
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
