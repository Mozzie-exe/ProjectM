using UnityEngine;

// Ahır gerilim olayının zamanlaması ve eğrileri (t = tetik anından beri geçen süre, saniye).
public static class BarnScareMath
{
    public const float T_Creak = 0f;
    public const float T_Pull = 0.3f, PullDuration = 0.6f;     // kamera samanlığa çekilir
    public const float T_Bales = 0.8f, BaleStagger = 0.12f;    // balyalar sırayla devrilir
    public const float T_Steps = 1.0f;                          // samanlıkta uzaklaşan ayak sesi (isteğe bağlı)
    public const float T_Heart = 1.6f;
    public const float T_Release = 2.4f;                       // kontrol geri gelir
    public const float TipDuration = 0.35f;
    public const float Gravity = 9.81f;
    public const float DoorSlideDuration = 1.2f, DoorSlideDistance = 2.5f;

    // Kameranın samanlığa bakma ağırlığı: çekiş boyunca 0→1, bırakılana kadar 1.
    public static float LookWeight(float t)
    {
        if (t < T_Pull || t >= T_Release) return 0f;
        return Smooth((t - T_Pull) / PullDuration);
    }

    public static float SlideOffset(float t, float duration, float distance) =>
        Smooth(t / Mathf.Max(0.0001f, duration)) * distance;

    // Balya kenardan devrilirken açısı (yavaş başlar, hızlanır).
    public static float TipAngle(float tLocal)
    {
        float s = Mathf.Clamp01(tLocal / TipDuration);
        return s * s * 90f;
    }

    // Balyanın aşağı düştüğü mesafe; devrilmenin yarısında serbest düşüş başlar, yere değince durur.
    public static float FallDrop(float tLocal, float height)
    {
        float f = tLocal - TipDuration * 0.5f;
        if (f <= 0f) return 0f;
        return Mathf.Min(height, 0.5f * Gravity * f * f);
    }

    public static bool CanTrigger(bool doorOpen, bool alreadyFired) => doorOpen && !alreadyFired;

    static float Smooth(float s) { s = Mathf.Clamp01(s); return s * s * (3f - 2f * s); }
}
