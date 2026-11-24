using UnityEngine;
using UnityEngine.UI;

// Ekranın üstünde hafif TV/radyo parazit dokusu: her kare rastgele kayar. Geçişte yoğunlaşır.
[RequireComponent(typeof(RawImage))]
public class MenuStatic : MonoBehaviour
{
    [Range(0f, 1f)] public float alpha = 0.045f;
    public int size = 256;

    RawImage img;
    float boost;

    void Awake()
    {
        img = GetComponent<RawImage>();
        // gri tonlu RGBA (R8 doku UI'da renk kaymasına yol açıyordu)
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[size * size];
        var r = new System.Random(3);
        for (int i = 0; i < px.Length; i++) { byte v = (byte)r.Next(0, 256); px[i] = new Color32(v, v, v, 255); }
        tex.SetPixels32(px);
        tex.Apply();
        img.texture = tex;
        img.raycastTarget = false;
    }

    public void SetBoost(float b) => boost = b;

    void Update()
    {
        float s = Screen.height / 360f;
        img.uvRect = new Rect(Random.value, Random.value, Screen.width / (size * s * 1.2f), Screen.height / (size * s * 1.2f));
        img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + boost));
    }
}
