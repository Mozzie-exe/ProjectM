using UnityEngine;
using UnityEngine.Rendering;

// Menü arkasındaki 3D sahne: siyah-beyaz, ağır kamera kayması ve evi tarayan bir fener ışığı.
// Kamera oyuncu kameranın ayarlarını (piksel render texture, post-process) kopyalar.
public class MenuBackdrop : MonoBehaviour
{
    public Camera menuCamera;
    public Camera copySettingsFrom;
    public Volume blackAndWhite;          // global, öncelikli; sadece menüdeyken açık
    public Light sweepLight;              // evi tarayan fener

    [Header("Kamera kayması")]
    public Vector3 driftAmount = new Vector3(0.8f, 0.25f, 0.4f);
    public float driftSpeed = 0.05f;

    [Header("Fener")]
    public Transform sweepTarget;
    public float sweepAngle = 14f;
    public float sweepSpeed = 0.35f;

    Vector3 basePos;
    Quaternion baseRot, lightBase;

    void Awake()
    {
        if (copySettingsFrom)
        {
            var keepPos = menuCamera.transform.position;
            var keepRot = menuCamera.transform.rotation;
            menuCamera.CopyFrom(copySettingsFrom);
            menuCamera.transform.SetPositionAndRotation(keepPos, keepRot);
        }
        menuCamera.depth = 50f;
        basePos = menuCamera.transform.position;
        baseRot = menuCamera.transform.rotation;
        if (sweepLight) lightBase = sweepLight.transform.rotation;
    }

    void Update()
    {
        float t = Time.unscaledTime;
        var tr = menuCamera.transform;
        tr.position = basePos + new Vector3(
            Mathf.Sin(t * driftSpeed * 6.28f) * driftAmount.x,
            Mathf.Sin(t * driftSpeed * 4.1f) * driftAmount.y,
            Mathf.Sin(t * driftSpeed * 3.3f) * driftAmount.z);
        tr.rotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.21f) * 0.4f, Mathf.Sin(t * 0.13f) * 0.8f, 0f);

        if (sweepLight)
        {
            float yaw = Mathf.Sin(t * sweepSpeed) * sweepAngle + Mathf.Sin(t * sweepSpeed * 2.7f) * 2f;
            float pitch = Mathf.Sin(t * sweepSpeed * 1.6f) * 3f;
            sweepLight.transform.rotation = lightBase * Quaternion.Euler(pitch, yaw, 0f);
            // titreyen fener
            sweepLight.intensity = Mathf.Lerp(sweepLight.intensity, Random.value < 0.03f ? baseIntensity * 0.4f : baseIntensity, 0.3f);
        }
    }

    float baseIntensity = -1f;
    void Start()
    {
        if (sweepLight) baseIntensity = sweepLight.intensity;
    }

    public void Shutdown()
    {
        if (blackAndWhite) blackAndWhite.gameObject.SetActive(false);
        if (sweepLight) sweepLight.gameObject.SetActive(false);
        menuCamera.gameObject.SetActive(false);
        enabled = false;
    }
}
