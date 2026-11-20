using UnityEngine;

// Far parlaması: gece karşıdan gelen arabanın farları uzaktan da "far" diye okunsun.
// Kameraya dönük additive quad; sadece farın önünden bakınca görünür, uzaklaştıkça biraz büyür
// (küçük bir disk uzakta tek piksele inmesin).
[RequireComponent(typeof(MeshRenderer))]
public class HeadlightGlare : MonoBehaviour
{
    public float size = 0.5f;
    [Tooltip("Metre başına büyüme (uzakta da görünsün)")]
    public float distanceGrow = 0.025f;
    public Color color = new Color(1f, 0.9f, 0.7f, 1f);

    MeshRenderer mr;
    MaterialPropertyBlock mpb;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        Camera cam = ActiveCamera();
        if (!cam) return;
        Vector3 toCam = cam.transform.position - transform.position;
        float dist = toCam.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toCam / dist;
        // Arabanın önüne bakan yön (ebeveyn +Z): yandan/arkadan bakınca söner
        Vector3 fwd = transform.parent ? transform.parent.forward : transform.forward;
        float facing = Mathf.Clamp01((Vector3.Dot(fwd, dir) - 0.15f) / 0.6f);
        transform.rotation = Quaternion.LookRotation(-dir, cam.transform.up);
        float s = size + dist * distanceGrow;
        Vector3 ps = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(s / ps.x, s / ps.y, 1f);
        mpb.SetColor(BaseColor, color * facing);
        mr.SetPropertyBlock(mpb);
    }

    static Camera cached;
    static Camera ActiveCamera()
    {
        if (cached && cached.isActiveAndEnabled) return cached;
        cached = null;
        foreach (var c in Camera.allCameras)
            if (!cached || c.depth > cached.depth) cached = c;
        return cached;
    }
}
