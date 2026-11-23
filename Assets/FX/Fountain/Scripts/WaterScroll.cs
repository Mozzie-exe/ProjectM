using UnityEngine;

/// <summary>
/// Su efektleri icin doku kaydirma: akan su perdeleri asagi akar, havuz yuzeyi dalgalanir,
/// kopuk halkasi yavasca doner. Materyal assetini degistirmez (MaterialPropertyBlock kullanir).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class WaterScroll : MonoBehaviour
{
    [Tooltip("Doku kaydirma hizi (UV/saniye). Akan su icin Y negatif.")]
    public Vector2 speed = new Vector2(0f, -1.2f);
    public Vector2 tiling = Vector2.one;
    [Tooltip("Ikinci katman hizi (dalga icin hafif farkli yon)")]
    public float wobble = 0f;
    [Tooltip("Yerel Y ekseni etrafinda donme (derece/saniye)")]
    public float rotateSpeed = 0f;

    static readonly int BaseST = Shader.PropertyToID("_BaseMap_ST");
    Renderer rend;
    MaterialPropertyBlock mpb;
    Quaternion baseRot;

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseRot = transform.localRotation;
    }

    void Update()
    {
        float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        Vector2 off = speed * t;
        if (wobble != 0f) off += new Vector2(Mathf.Sin(t * 0.7f), Mathf.Cos(t * 0.53f)) * wobble;
        off.x -= Mathf.Floor(off.x);
        off.y -= Mathf.Floor(off.y);
        rend.GetPropertyBlock(mpb);
        mpb.SetVector(BaseST, new Vector4(tiling.x, tiling.y, off.x, off.y));
        rend.SetPropertyBlock(mpb);
        if (rotateSpeed != 0f && Application.isPlaying)
            transform.localRotation = baseRot * Quaternion.Euler(0f, rotateSpeed * t, 0f);
    }

    void OnDisable()
    {
        if (rend) rend.SetPropertyBlock(null);
    }
}
