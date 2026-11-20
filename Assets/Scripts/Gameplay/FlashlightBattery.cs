using UnityEngine;

// Fener pili: açıkken azalır, %20'nin altında titrer ve konisi daralır. Hiç bitmez (ölüm yok, sadece gerilim).
[RequireComponent(typeof(Flashlight))]
public class FlashlightBattery : MonoBehaviour
{
    [Range(0f, 1f)] public float charge = 1f;
    public float secondsPerFullCharge = 900f;  // açık kaldığı sürece 15 dakikada boşalır
    [Range(0f, 1f)] public float lowThreshold = 0.2f;
    [Range(0f, 1f)] public float minimumCharge = 0.05f;
    public float lowConeMultiplier = 0.6f;

    Flashlight flashlight;
    float baseIntensity;
    float baseAngle;
    float flickerTimer;
    float flickerValue = 1f;
    bool forcedFlicker;

    public float Charge => charge;

    void Start()
    {
        flashlight = GetComponent<Flashlight>();
    }

    // Bodrum merdiveni gibi yerlerde dışarıdan titretmek için
    public void SetForcedFlicker(bool on) => forcedFlicker = on;

    public void Refill()
    {
        charge = 1f;
        HudMessage.Show("Yedek pili feneri taktım.");
    }

    void LateUpdate()
    {
        var light = flashlight != null ? flashlight.spotLight : null;
        if (light == null) return;
        if (baseIntensity <= 0f) { baseIntensity = flashlight.intensity; baseAngle = flashlight.spotAngle; }

        if (flashlight.IsOn)
            charge = Mathf.Max(minimumCharge, charge - Time.deltaTime / secondsPerFullCharge);

        bool low = charge < lowThreshold || forcedFlicker;
        float k = low ? Mathf.InverseLerp(minimumCharge, lowThreshold, charge) : 1f;
        if (forcedFlicker) k = Mathf.Min(k, 0.5f);

        if (low)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = Random.Range(0.03f, 0.25f);
                flickerValue = Random.value < 0.25f ? Random.Range(0.05f, 0.4f) : Random.Range(0.75f, 1f);
            }
        }
        else flickerValue = 1f;

        light.intensity = baseIntensity * Mathf.Lerp(0.45f, 1f, k) * flickerValue;
        light.spotAngle = baseAngle * Mathf.Lerp(lowConeMultiplier, 1f, k);
        light.innerSpotAngle = light.spotAngle * 0.6f;
    }
}
