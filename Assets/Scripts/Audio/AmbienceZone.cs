using System.Collections.Generic;
using UnityEngine;

// Açık havada bölgeye özel ortam sesi (ör. tarlada kargalar). Dinleyici kutuya girdikçe kendi sesi (own) açılır,
// duck listesindeki ortam sesleri (cırcır böceği) duckLevel'a iner. Kenarda edgeFade metrelik yumuşak geçiş.
// AmbienceLoop her karede GainFor ile çarpanını sorar.
public class AmbienceZone : MonoBehaviour
{
    public Vector3 size = new Vector3(50f, 10f, 50f);
    public float edgeFade = 10f;
    [Tooltip("Bu bölgenin sesi; dışarıda susar")]
    public AmbienceLoop own;
    [Tooltip("Bölgedeyken kısılan ortam sesleri")]
    public AmbienceLoop[] duck;
    [Range(0f, 1f)] public float duckLevel = 0.3f;

    static readonly List<AmbienceZone> zones = new List<AmbienceZone>();

    void OnEnable() => zones.Add(this);
    void OnDisable() => zones.Remove(this);

    public float Weight(Vector3 p)
    {
        float d = Vector3.Distance(p, IndoorZone.BoxClosestPoint(transform, size, p));
        return edgeFade > 0f ? 1f - Mathf.Clamp01(d / edgeFade) : (d > 0f ? 0f : 1f);
    }

    public static float GainFor(AmbienceLoop loop, Vector3 listener)
    {
        float g = 1f;
        foreach (var z in zones)
        {
            if (z.own == loop) g *= z.Weight(listener);
            else if (z.duck != null && System.Array.IndexOf(z.duck, loop) >= 0) g *= Mathf.Lerp(1f, z.duckLevel, z.Weight(listener));
        }
        return g;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
