using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Duraklatma ekranındaki "radyo" görüntüsü: oyun kamerası daha düşük çözünürlüğe iner (iri pikseller),
// siyah-beyaz + grenli profil devreye girer ve görüntü ara sıra yatay olarak kayar (sinyal bozulması).
public class PauseScreenFx : MonoBehaviour
{
    public Camera playerCamera;
    public RawImage cameraFilter;           // Canvas/CameraFilter: render texture'ı ekrana basan görüntü
    public VolumeProfile blackAndWhite;     // Assets/Settings/MenuProfile_BW
    public Vector2Int lowRes = new Vector2Int(240, 135);

    [Header("Sinyal bozulması")]
    public float tearChance = 0.06f;        // kare başına kayma olasılığı
    public float tearAmount = 0.012f;

    Volume volume;
    RenderTexture low, original;
    Rect baseUv;
    float amount, target;
    bool swapped;

    void Awake()
    {
        var go = new GameObject("PauseVolume_BW");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = blackAndWhite;
        volume.weight = 0f;
        volume.enabled = false;
    }

    public void SetActive(bool on)
    {
        target = on ? 1f : 0f;
        if (on) Swap(true);
        volume.enabled = true;
    }

    void Update()
    {
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime * 5f);
        volume.weight = amount;
        if (amount <= 0f && target <= 0f)
        {
            volume.enabled = false;
            Swap(false);
            return;
        }
        if (swapped && cameraFilter)
        {
            var uv = baseUv;
            if (Random.value < tearChance * amount) uv.x += (Random.value - 0.5f) * tearAmount * 2f;
            cameraFilter.uvRect = uv;
        }
    }

    void Swap(bool on)
    {
        if (!playerCamera || !cameraFilter || on == swapped) return;
        if (on)
        {
            original = playerCamera.targetTexture;
            if (original == null) return;
            if (low == null)
            {
                low = new RenderTexture(lowRes.x, lowRes.y, original.depthStencilFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None ? 0 : 24, original.format)
                { filterMode = FilterMode.Point, name = "PauseLowRes" };
            }
            baseUv = cameraFilter.uvRect;
            playerCamera.targetTexture = low;
            cameraFilter.texture = low;
        }
        else
        {
            playerCamera.targetTexture = original;
            cameraFilter.texture = original;
            cameraFilter.uvRect = baseUv;
        }
        swapped = on;
    }

    void OnDestroy()
    {
        Swap(false);
        if (low) { low.Release(); Destroy(low); }
    }
}
