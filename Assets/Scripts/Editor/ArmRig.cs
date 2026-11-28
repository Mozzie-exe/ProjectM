using UnityEngine;

// Sinematik kolları (giriş ve oyun sonu ortak): iki kemik IK + anatomik bilek.
// El pozu (kavrama noktası + yön) anahtarlardan gelir; kol her karede omuzdan bileğe çözülür.
//  - Dirsek, omuz-bilek ekseni etrafında bileği en az bükecek açıya döner (kareler arası yumuşatılır).
//  - Ön kol elin kendi ekseni etrafındaki dönüşünü taşır (pronasyon); bilek yalnızca bükülür, takla atmaz.
//  - Serbest el (bir şey tutmayan) bilek sınırlarına çekilir; tutan elin pozu değişmez (eşya yerinden kaymaz).
// Eksenler: el yerelinde +z parmaklar, +y başparmak tarafı, avuç sağ elde -x, sol elde +x.
public static class ArmRig
{
    public const float UpperLen = 0.34f, ForeLen = 0.31f;   // build_arms.py ile aynı
    static readonly Vector3 WristR = new Vector3(0.020f, 0f, -0.118f);
    public static Vector3 WristLocal(bool left) => left ? new Vector3(-WristR.x, WristR.y, WristR.z) : WristR;

    // Bilek sınırları (derece): avuca bükülme / geri bükülme / başparmak yanı / serçe parmak yanı
    public const float FlexMax = 75f, ExtMax = 65f, RadialMax = 25f, UlnarMax = 35f;
    const float SwivelMax = 70f, SwivelStep = 5f;
    const float SwivelRate = 5f;                             // kare başına en fazla (30 fps'te 150°/sn): dirsek karşı yana savrulmaz
    const int SmoothRadius = 4;                              // dirsek açısı ±4 karede ortalanır

    // free: 0 = el bir şey tutuyor (poz aynen), 1 = serbest (bilek sınıra çekilir); arası karışım
    public struct Input { public Vector3 shoulder, pole, upHint, handPos; public Quaternion handRot; public float free; }
    public struct Pose { public Vector3 shoulder, elbow, handPos; public Quaternion upper, fore, hand; public float swivel; }

    // İki kemik IK. Bilek uzanamayacak kadar uzaktaysa omuz öne kayar (gövde öne eğilir).
    // swivel: dirsek, pole'dan omuz-bilek ekseni etrafında bu kadar döner.
    public static Vector3 Elbow(Vector3 s, Vector3 w, Vector3 pole, float swivel, out Vector3 shoulder)
    {
        float reach = (UpperLen + ForeLen) * 0.995f;
        Vector3 d = w - s;
        if (d.magnitude > reach) s = w - d.normalized * reach;
        d = w - s;
        float len = Mathf.Max(d.magnitude, Mathf.Abs(UpperLen - ForeLen) + 0.01f);
        Vector3 dir = d.normalized;
        float cosA = Mathf.Clamp((UpperLen * UpperLen + len * len - ForeLen * ForeLen) / (2 * UpperLen * len), -1f, 1f);
        Vector3 perp = Vector3.ProjectOnPlane(pole, dir);
        if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.down, dir);
        perp = Quaternion.AngleAxis(swivel, dir) * perp.normalized;
        shoulder = s;
        Vector3 e = s + dir * (UpperLen * cosA) + perp * (UpperLen * Mathf.Sqrt(1f - cosA * cosA));
        // kısa mesafede (len sınırlandıysa) ön kol boyu tutsun
        return w + (e - w).normalized * ForeLen;
    }

    // Ön kol: elin dönüşünü en kısa yoldan ön kol eksenine çevirir. Elin yukarı ekseni ön kola
    // paralel olsa da (eski LookRotation'ın takla attığı yer) sürekli kalır.
    public static Quaternion Fore(Vector3 foreDir, Quaternion hand) =>
        Quaternion.FromToRotation(hand * Vector3.forward, foreDir) * hand;

    // (bükülme, sapma) derece; avuca doğru ve başparmak yanına doğru pozitif.
    public static Vector2 WristAngles(Quaternion fore, Quaternion hand, bool left)
    {
        Vector3 v = Quaternion.Inverse(fore) * (hand * Vector3.forward);
        float palm = left ? v.x : -v.x;
        return new Vector2(Mathf.Atan2(palm, v.z) * Mathf.Rad2Deg, Mathf.Atan2(v.y, v.z) * Mathf.Rad2Deg);
    }

    public static bool WithinLimits(Vector2 a) =>
        a.x <= FlexMax + 0.5f && a.x >= -ExtMax - 0.5f && a.y <= RadialMax + 0.5f && a.y >= -UlnarMax - 0.5f;

    static float Excess(Vector2 a) =>
        Mathf.Max(0f, a.x - FlexMax) + Mathf.Max(0f, -ExtMax - a.x) + Mathf.Max(0f, a.y - RadialMax) + Mathf.Max(0f, -UlnarMax - a.y);

    // Sınır dışındaki eli, kendi ekseni etrafındaki dönüşünü koruyarak ön kolla hizaya doğru en az döndürür.
    public static Quaternion ClampHand(Quaternion fore, Quaternion hand, bool left)
    {
        if (WithinLimits(WristAngles(fore, hand, left))) return hand;
        Quaternion straight = Fore(fore * Vector3.forward, hand);
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 16; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (WithinLimits(WristAngles(fore, Quaternion.Slerp(hand, straight, mid), left))) hi = mid; else lo = mid;
        }
        return Quaternion.Slerp(hand, straight, hi);
    }

    static float Cost(Vector3 s, Vector3 w, Vector3 pole, Quaternion hand, bool left, float sw, float prevSw, bool hasPrev)
    {
        Vector3 e = Elbow(s, w, pole, sw, out var sh);
        Quaternion f = Fore((w - e).normalized, hand);
        Vector2 a = WristAngles(f, hand, left);
        float bend = Vector3.Angle(f * Vector3.forward, hand * Vector3.forward);
        float ex = Excess(a);
        float c = ex * ex * 0.5f + Mathf.Max(0f, bend - 15f) * 0.5f + Mathf.Abs(sw) * 0.08f;
        c += Mathf.Max(0f, e.y - sh.y - 0.05f) * 400f;   // dirsek omuzdan yukarı kalkmasın
        if (hasPrev) c += (sw - prevSw) * (sw - prevSw) * 0.02f;
        return c;
    }

    // Dirsek düzlemi önceki kareden taşınır. Omuz-bilek ekseni pole yönüne yaklaştıkça pole tanımsızlaşır
    // (sarkan kol); o zaman önceki yön korunur, pole belirginleşince ona kare başına en fazla PoleRate dönülür.
    const float PoleRate = 6f;
    static Vector3 CarryPole(Vector3 pole, Vector3 dir, Vector3 prev)
    {
        Vector3 raw = Vector3.ProjectOnPlane(pole, dir);
        float strength = raw.magnitude / Mathf.Max(pole.magnitude, 1e-6f);
        if (prev == Vector3.zero)
            return raw.sqrMagnitude > 1e-8f ? raw.normalized : Vector3.ProjectOnPlane(Vector3.down, dir).normalized;
        Vector3 carried = Vector3.ProjectOnPlane(prev, dir);
        if (carried.sqrMagnitude < 1e-8f) carried = raw.sqrMagnitude > 1e-8f ? raw : Vector3.ProjectOnPlane(Vector3.down, dir);
        carried.Normalize();
        if (raw.sqrMagnitude < 1e-8f) return carried;
        Vector3 target = Vector3.Slerp(carried, raw.normalized, Mathf.Clamp01((strength - 0.15f) / 0.35f));
        return Vector3.RotateTowards(carried, target, PoleRate * Mathf.Deg2Rad, 0f);
    }

    public static Pose[] Bake(Input[] inp, bool left)
    {
        int n = inp.Length;
        var sw = new float[n];
        var wrist = new Vector3[n];
        var pole = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var x = inp[i];
            wrist[i] = x.handPos + x.handRot * WristLocal(left);
            pole[i] = CarryPole(x.pole, (wrist[i] - x.shoulder).normalized, i > 0 ? pole[i - 1] : Vector3.zero);
            float best = 0f, bestC = float.MaxValue;
            for (float a = -SwivelMax; a <= SwivelMax + 0.01f; a += SwivelStep)
            {
                float c = Cost(x.shoulder, wrist[i], pole[i], x.handRot, left, a, i > 0 ? sw[i - 1] : 0f, i > 0);
                if (c < bestC) { bestC = c; best = a; }
            }
            sw[i] = i > 0 ? sw[i - 1] + Mathf.Clamp(best - sw[i - 1], -SwivelRate, SwivelRate) : best;
        }
        var sm = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f; int k = 0;
            for (int j = Mathf.Max(0, i - SmoothRadius); j <= Mathf.Min(n - 1, i + SmoothRadius); j++) { sum += sw[j]; k++; }
            sm[i] = sum / k;
        }

        var o = new Pose[n];
        Vector3 prevUp = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            var x = inp[i];
            Vector3 e = Elbow(x.shoulder, wrist[i], pole[i], sm[i], out var sh);
            Vector3 d = (wrist[i] - e).normalized;
            Quaternion hand = x.handRot;
            Vector3 hp = x.handPos;
            if (x.free > 0f)
            {
                hand = Quaternion.Slerp(hand, ClampHand(Fore(d, hand), hand, left), Mathf.Clamp01(x.free));
                hp = wrist[i] - hand * WristLocal(left);
            }
            // Üst kol: yukarı ekseni önceki kareden taşınır (takla yok), yavaşça gövde ipucuna çekilir
            Vector3 du = (e - sh).normalized;
            Vector3 hint = Vector3.ProjectOnPlane(x.upHint, du);
            Vector3 up = prevUp == Vector3.zero ? hint : Vector3.ProjectOnPlane(prevUp, du);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, du);
            up = up.normalized;
            if (hint.sqrMagnitude > 1e-4f) up = Vector3.Slerp(up, hint.normalized, 0.1f);
            prevUp = up;
            o[i] = new Pose
            {
                shoulder = sh, elbow = e, handPos = hp, hand = hand, swivel = sm[i],
                upper = Quaternion.LookRotation(du, up), fore = Fore(d, hand)
            };
        }
        return o;
    }
}
