using UnityEngine;

// Oyun sonu sinematiğinin saf hesapları (test edilebilir, sahneye bağlı değil).
public static class EndCinematicMath
{
    // Kapı bakışı: cesede döner (1.1 sn), bakar (0.6 sn), eski bakışa döner (1.3 sn)
    public const float DoorLookDuration = 3f;
    const float LookIn = 1.1f, LookHold = 1.7f;
    public static float DoorLookWeight(float t)
    {
        if (t <= LookIn) return Smooth(t / LookIn);
        if (t <= LookHold) return 1f;
        return 1f - Smooth((t - LookHold) / (DoorLookDuration - LookHold));
    }

    // E'ye basıldığı andaki oyuncu bakışından sinematiğin başlangıç pozuna geçiş
    public const float StartBlendDuration = 1.2f;
    public static float StartBlend(float t) => Smooth(t / StartBlendDuration);

    // Dolabın devrilme açısı (derece); t: devrilme başından beri sn. Ağır cisim: yavaş başlar, hızlanır.
    public static float CabinetFallAngle(float t)
    {
        float u = Mathf.Clamp01(t / 0.8f);
        return 35f * u * u;
    }

    // Ceset sessizliğinde susturulan kanallar (Cinematic açık kalır: dolap sesleri oradan)
    public static readonly AudioChannel[] HushChannels = { AudioChannel.Music, AudioChannel.SFX, AudioChannel.Ambience, AudioChannel.UI };

    static float Smooth(float s) { s = Mathf.Clamp01(s); return s * s * (3f - 2f * s); }
}
