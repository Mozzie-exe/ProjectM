using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

// Giriş sinematiğini sahnede kurar: pikap, eller, eşyalar, ışıklar, Timeline ve animasyon.
// Modeller: BlenderSource/IntroCinematic/build_intro_models.py
// Tekrar çalıştırmak güvenlidir; eski "IntroCinematic" objesini ve üretilen dosyaları yeniler.
//
// Koordinatlar "araba birimi" (gerçek metre) ve araba yerelidir: +Z ileri, +Y yukarı, -X sürücü tarafı.
// Araba kökü oyun dünyasına uysun diye K kadar büyütülür (kapılar 3.9 m, göz 2.85 m).
public static class IntroCinematicBuilder
{
    const string ModelDir = "Assets/Models/IntroCinematic";
    const string CineDir = "Assets/Cinematics";
    const float K = 1.78f;
    const float FPS = 30f;

    // Sahne yerleşimi (dünya)
    const float RoadY = 11.1f;     // "yol" objesinin üst yüzeyi
    const float ParkX = 80f;
    const float ParkZ = 2.0f;      // kuzey (çiftlik) kenarına yakın
    const float LaneZ = -6f;
    const float V0 = 14f;          // sürüş hızı (m/sn, dünya)
    const float CruiseEnd = 9f, StopTime = 14f;

    // Zamanlama (sn)
    const float T_GrabDocs = 19.0f, T_DocsToLeft = 21.0f, T_GrabFlash = 22.6f;
    const float T_KeyTurnStart = 24.6f, T_KeyTurnEnd = 25.0f, T_LightsOff = 25.0f;
    const float T_DoorStart = 26.7f, T_DoorEnd = 27.9f, T_DomeOn = 26.9f;
    const float T_DoorCloseStart = 32.9f, T_DoorClosed = 33.45f;   // indikten sonra kapıyı iter
    const float T_FlashOn = 31.0f, T_Handoff = 56.2f;
    // Geçen arabalar: pikap karşı şeritten sürüşte yanımızdan geçer; wagon önde etrafı tararken arkadan gelip soldan geçer
    const float T_PickupPass = 6.0f, T_WagonPass = 43.4f;
    // Yürüyüş 1: kapıdan arabanın önüne. Tarama, köpek havlaması (irkilir, arkasını döner), yürüyüş 2: başladığı yere geri.
    const float T_Walk1Start = 34.0f, T_Walk1End = 39.8f, T_Bark = 46.6f, T_WalkStart = 49.6f, T_WalkEnd = 55.6f;
    const float T_Handbrake = 14.6f;   // araba durunca el freni çekilir (0.45 sn)

    static readonly Vector3 Eye = new Vector3(-0.40f, 1.55f, -0.40f);       // oturan sürücünün gözü
    static readonly Vector3 Stand = new Vector3(-1.75f, 1.60f, -1.05f);     // indikten sonra (göz 2.85 m / K)
    static readonly Vector3 Hinge = new Vector3(-0.95f, 1.15f, 0.70f);
    static readonly Vector3 DoorHandle = new Vector3(-0.8125f, 1.12f, 0.30f);
    static readonly Vector3 KeyHead = new Vector3(-0.13f, 1.10f, 0.37f);
    static readonly Vector3 KeyPivot = new Vector3(-0.13f, 1.10f, 0.41f);
    static readonly Vector3 SteerNormalModel = new Vector3(0f, 0.515f, 0.857f);
    static readonly P SeatDocs = new P(new Vector3(0.48f, 0.852f, -0.45f), Quaternion.Euler(0, 15, 0));
    static readonly P SeatFlash = new P(new Vector3(0.36f, 0.875f, -0.22f), Quaternion.Euler(0, 15, 0));   // boyuna yatık: el bileği bükülmeden kavranır
    // Flashlight.cs: holdPosition (0.22,-0.2,0.55), dönüş (-2,-4,0); oyuncu Y ölçeği 1.5
    static readonly P GameplayFlashInView = new P(new Vector3(0.22f, -0.30f, 0.55f) / K, Quaternion.Euler(-2, -4, 0));

    // ------------------------------------------------------------------ poz yardımcıları
    struct P
    {
        public Vector3 p; public Quaternion r;
        public P(Vector3 p, Quaternion r) { this.p = p; this.r = r; }
        public static P operator *(P a, P b) => new P(a.p + a.r * b.p, a.r * b.r);
        public P Inv() { var ir = Quaternion.Inverse(r); return new P(-(ir * p), ir); }
        public static P Lerp(P a, P b, float s) => new P(Vector3.Lerp(a.p, b.p, s), Quaternion.Slerp(a.r, b.r, s));
    }
    static float Smooth(float s) { s = Mathf.Clamp01(s); return s * s * (3 - 2 * s); }

    class Key
    {
        public float t; public bool view; public P pose;
        public bool linear;   // iki ucu da linear olan aralık yavaşlamadan geçilir (sık örneklenmiş hareket)
        public Key(float t, bool view, P pose, bool linear = false) { this.t = t; this.view = view; this.pose = pose; this.linear = linear; }
    }

    // ------------------------------------------------------------------ kafa (kamera)
    static readonly List<Key> headKeys = new List<Key>();
    static readonly List<Vector3> headAng = new List<Vector3>();   // (pitch, yaw, roll), yaw sarılmadan sürekli
    static void HW(float t, P w)
    {
        Vector3 e = w.r.eulerAngles;
        H(t, w.p, Mathf.DeltaAngle(0, e.x), Mathf.DeltaAngle(0, e.y), Mathf.DeltaAngle(0, e.z));
    }
    static void H(float t, Vector3 pos, float pitch, float yaw, float roll = 0)
    {
        headKeys.Add(new Key(t, false, new P(pos, Quaternion.Euler(pitch, yaw, roll))));
        headAng.Add(new Vector3(pitch, yaw, roll));
    }

    // Araba yerelinde zemin y=0 (yol). Gövde x ±1.12, ön tampon z=2.38: yan ve köşe ~0.6 açıktan geçilir.
    static readonly Vector3[] PathFront =
    {
        new Vector3(-1.75f, 0, -1.00f), new Vector3(-1.98f, 0, 0.55f), new Vector3(-1.78f, 0, 2.20f), new Vector3(-1.00f, 0, 3.30f)
    };
    static readonly Vector3[] PathBack =
    {
        new Vector3(-1.00f, 0, 3.30f), new Vector3(-1.70f, 0, 2.45f), new Vector3(-2.00f, 0, 0.80f), new Vector3(-1.75f, 0, -1.05f)
    };
    // Vites topuzu (dikey silindir r 0.020) ve el freni (pivot arkada, tutamak ileri eksenli silindir r 0.019)
    static readonly Vector3 GearKnob = new Vector3(0f, 1.00f, 0.075f);
    static readonly Vector3 HbPivot = new Vector3(0f, 0.815f, -0.50f), HbGrip = new Vector3(0f, 0.82f, -0.27f);
    static Quaternion HbRot(float t) => Quaternion.AngleAxis(-22f * Smooth((t - T_Handbrake) / 0.45f), Vector3.right);   // ön ucu yukarı

    static void BuildHeadKeys()
    {
        headKeys.Clear();
        headAng.Clear();
        var E = Eye;
        H(0.0f, E, 3, 0);
        H(4.5f, E, 2, -5);
        H(5.6f, E, 2, -6);
        H(6.0f, E, 3, -13);                                         // karşıdan gelen pikaba kısa bakış
        H(6.8f, E, 2, -3);
        H(8.0f, E, 2, 4);
        H(11.0f, E, 3, 0);
        H(13.6f, E + new Vector3(0, 0, 0.03f), 7, 0);              // durunca hafif öne
        H(14.3f, E, 6, 8);
        H(14.95f, E + new Vector3(0.04f, -0.04f, 0.02f), 38, 40);   // el frenine bakış (el kadrajın altında görünür)
        H(15.7f, E + new Vector3(0.03f, -0.02f, 0.01f), 22, 30);
        H(17.0f, E + new Vector3(0.09f, -0.04f, 0.04f), 33, 52);   // sağ koltuğa bakış, gövde o yana eğilir
        H(19.0f, E + new Vector3(0.15f, -0.08f, 0.04f), 36, 55);
        H(21.0f, E + new Vector3(0.05f, -0.04f, 0.03f), 30, 40);
        H(22.6f, E + new Vector3(0.15f, -0.08f, 0.04f), 36, 58);
        H(23.9f, E + new Vector3(0.03f, -0.03f, 0.04f), 27, 18);   // kontağa bakış
        H(25.2f, E + new Vector3(0.03f, -0.03f, 0.04f), 26, 20);
        H(26.2f, E, 9, -22);
        H(27.0f, E + new Vector3(-0.03f, 0, 0), 10, -70);          // kapıya bakış
        H(28.0f, E + new Vector3(-0.05f, 0, 0), 8, -80);
        H(29.0f, new Vector3(-0.85f, 1.45f, -0.45f), 14, -85, 2);  // inerken
        H(30.2f, new Vector3(-1.45f, 1.28f, -0.75f), 12, -100);
        H(31.6f, Stand, 14, -50);                                   // ayağa kalktı, fener yanık: açık kapıya döner
        H(32.5f, Stand + new Vector3(-0.04f, -0.05f, 0.14f), 16, -26);   // kapıya dön, uzanmak için öne eğilir
        H(33.1f, Stand + new Vector3(-0.05f, -0.07f, 0.24f), 17, -24);   // iterken (kapı yayının dışında: menteşeye 1.6)
        H(33.6f, Stand + new Vector3(0, -0.03f, 0.05f), 13, -20);        // kapı kapandı, doğrulur
        // Arabanın önüne yürür (fener yolda), önde durup etrafı tarar. Pozitif yaw = sağ (arabanın önünden geçen yol / ağaçlar).
        HW(T_Walk1Start, WalkFront(0f));
        HW(T_Walk1End, WalkFront(1f));
        Vector3 F = WalkFront(1f).p;
        H(40.9f, F, 12, 38);                                        // sağa: arabanın önünden ağaçlara
        H(41.9f, F + new Vector3(0, 0.005f, 0), 7, 18);             // ileri, uzağa
        // Arkadan araba gelir (farları etrafı aydınlatır): sola döner, yanından geçişini ve uzaklaşan stop lambalarını izler
        H(T_WagonPass - 0.8f, F, 8, -15);
        H(T_WagonPass - 0.1f, F + new Vector3(0, -0.01f, 0), 6, -92, -1);
        H(T_WagonPass + 0.7f, F, 7, -32);
        H(T_WagonPass + 1.6f, F, 8, -14);
        H(T_WagonPass + 2.6f, F, 9, -10);
        // Köpek havlaması: irkilir (kafa kısa bir an çöker), omzunun üstünden arkasına döner, dinler
        H(T_Bark + 0.15f, F + new Vector3(0, -0.03f, -0.01f), 4, -30);
        H(T_Bark + 0.40f, F + new Vector3(0, -0.02f, 0), 7, -48, 1);
        H(T_Bark + 1.30f, F + new Vector3(0, -0.01f, 0), 12, -140, 2.5f);
        H(T_Bark + 1.90f, F, 10, -164, 1.5f);
        H(T_Bark + 2.60f, F, 11, -172);
        HW(T_WalkStart, WalkBack(0f));
        // Açılar komşusuna göre sarılır (195 ile -162 arası 360 derecelik tur atmasın)
        for (int i = 1; i < headAng.Count; i++)
        {
            Vector3 a = headAng[i], b = headAng[i - 1];
            for (int c = 0; c < 3; c++) a[c] = b[c] + Mathf.DeltaAngle(b[c], a[c]);
            headAng[i] = a;
        }
    }

    static Vector3 PathPoint(Vector3[] path, float u)
    {
        int n = path.Length - 1;
        float f = Mathf.Clamp01(u) * n;
        int i = Mathf.Min((int)f, n - 1);
        float s = f - i;
        Vector3 p0 = path[Mathf.Max(i - 1, 0)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(i + 2, n)];
        return 0.5f * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s + (-p0 + 3 * p1 - 3 * p2 + p3) * s * s * s);
    }

    static float PathYaw(Vector3[] path, float u)
    {
        Vector3 d = PathPoint(path, Mathf.Min(u + 0.02f, 1f)) - PathPoint(path, Mathf.Max(u - 0.02f, 0f));
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    // Yürüyüş: yavaş başlar, yavaş durur; adımda kafa iner-kalkar. Bakış başta yawFrom'dan yol yönüne geçer,
    // sonda yawTo'ya döner (NaN: yol yönünde kalır). Yürürken fener yerde (pitch 14).
    static P WalkAlong(Vector3[] path, float u, float yawFrom, float pitchFrom, float yawTo, float pitchTo)
    {
        float e = Smooth(u) * 0.5f + u * 0.5f;
        Vector3 pos = PathPoint(path, e);
        float dist = 0f;
        for (int i = 1; i <= 40; i++) dist += (PathPoint(path, e * i / 40f) - PathPoint(path, e * (i - 1) / 40f)).magnitude;
        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
        float phase = dist / 0.70f * Mathf.PI;                 // adım boyu 0.7
        float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.028f * env;
        pos.y = Stand.y - 0.014f * env + bob;
        float lead = yawFrom + Mathf.DeltaAngle(yawFrom, PathYaw(path, Mathf.Min(e + 0.06f, 1f)));
        float yaw = Mathf.Lerp(yawFrom, lead, Smooth(u * 4f));
        if (!float.IsNaN(yawTo)) yaw = Mathf.Lerp(yaw, yaw + Mathf.DeltaAngle(yaw, yawTo), Smooth((u - 0.8f) / 0.2f));
        float pitch = Mathf.Lerp(pitchFrom, 14f, Smooth(u * 3f));
        pitch = Mathf.Lerp(pitch, pitchTo, Smooth((u - 0.8f) / 0.2f));
        float roll = Mathf.Sin(phase * 0.5f) * 0.6f * env;
        return new P(pos, Quaternion.Euler(pitch, yaw, roll));
    }
    static P WalkFront(float u) => WalkAlong(PathFront, u, -20f, 13f, 10f, 10f);
    // Geri dönüş: başladığı yere (kapının yanı) varır, havlamanın geldiği arkaya bakar; devirde yatay
    static P WalkBack(float u) => WalkAlong(PathBack, u, -172f, 11f, float.NaN, 0f);

    static P HeadAt(float t)
    {
        if (t >= T_WalkStart)
            return WalkBack(Mathf.Clamp01((t - T_WalkStart) / (T_WalkEnd - T_WalkStart)));
        if (t >= T_Walk1Start && t <= T_Walk1End)
            return WalkFront((t - T_Walk1Start) / (T_Walk1End - T_Walk1Start));
        P pose = SampleHead(t);
        if (t < StopTime) // yol titreşimi
            pose.p.y += (Mathf.Sin(t * 13f) * 0.004f + Mathf.Sin(t * 7.3f) * 0.003f) * (1f - Smooth((t - 12f) / 2f));
        return pose;
    }

    // Kafa: zaman ağırlıklı Catmull-Rom (Hermite). Her anahtarda durup kalkmaz; hız sürekli değişir.
    // Uç anahtarlarda hız sıfır. Dönüş açıları (pitch, yaw, roll) ayrı ayrı yumuşatılır.
    static P SampleHead(float t)
    {
        int n = headKeys.Count;
        if (t <= headKeys[0].t) return headKeys[0].pose;
        if (t >= headKeys[n - 1].t) return headKeys[n - 1].pose;
        int i = 0;
        while (t > headKeys[i + 1].t) i++;
        float t0 = headKeys[i].t, t1 = headKeys[i + 1].t, h = t1 - t0, s = (t - t0) / h;
        float h00 = 2 * s * s * s - 3 * s * s + 1, h10 = s * s * s - 2 * s * s + s, h01 = -2 * s * s * s + 3 * s * s, h11 = s * s * s - s * s;
        Vector3 pos = h00 * headKeys[i].pose.p + h10 * h * HeadTan(i, true) + h01 * headKeys[i + 1].pose.p + h11 * h * HeadTan(i + 1, true);
        Vector3 ang = h00 * headAng[i] + h10 * h * HeadTan(i, false) + h01 * headAng[i + 1] + h11 * h * HeadTan(i + 1, false);
        return new P(pos, Quaternion.Euler(ang));
    }
    static Vector3 HeadTan(int i, bool pos)
    {
        if (i == 0 || i == headKeys.Count - 1) return Vector3.zero;
        Vector3 a = pos ? headKeys[i - 1].pose.p : headAng[i - 1], b = pos ? headKeys[i + 1].pose.p : headAng[i + 1];
        Vector3 c = pos ? headKeys[i].pose.p : headAng[i];
        Vector3 m = (b - a) / (headKeys[i + 1].t - headKeys[i - 1].t);
        // Yön değiştiren eksende teğet sıfır: anahtarı aşıp geri dönmesin
        for (int k = 0; k < 3; k++)
            if ((c[k] - a[k]) * (b[k] - c[k]) <= 0f) m[k] = 0f;
        return m * 0.85f;
    }

    static P SampleKeys(List<Key> keys, float t)
    {
        if (t <= keys[0].t) return Resolve(keys[0], t);
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (t <= keys[i + 1].t)
            {
                float u = (t - keys[i].t) / Mathf.Max(keys[i + 1].t - keys[i].t, 1e-4f);
                float s = keys[i].linear && keys[i + 1].linear ? u : Smooth(u);
                return P.Lerp(Resolve(keys[i], t), Resolve(keys[i + 1], t), s);
            }
        }
        return Resolve(keys[keys.Count - 1], t);
    }

    // Görüşe bağlı anahtarlar o anki kafa pozuna göre çözülür, böylece eller kafayla birlikte gezer.
    static P Resolve(Key k, float t) => k.view ? HeadAt(t) * k.pose : k.pose;

    // ------------------------------------------------------------------ araba yolu (dünya)
    static float Distance(float t)
    {
        if (t <= CruiseEnd) return V0 * t;
        float tt = Mathf.Min(t, StopTime) - CruiseEnd, dur = StopTime - CruiseEnd;
        return V0 * CruiseEnd + V0 * (tt - tt * tt / (2 * dur));
    }
    static float TotalDistance => Distance(StopTime);
    static Vector3 CarPos(float t)
    {
        float x = ParkX + TotalDistance - Distance(t);
        // Şerit değişimi zamana değil yola bağlı: araba yavaşlarken yana kaymaz, yay düzgün kalır (direksiyon ~±60°)
        float lane0 = Distance(7.5f), lane1 = Distance(13.6f);
        float z = Mathf.Lerp(LaneZ, ParkZ, Smooth((Distance(t) - lane0) / (lane1 - lane0)));
        float y = RoadY + (t < StopTime ? Mathf.Sin(t * 9f) * 0.012f * (1 - Smooth((t - 12f) / 2f)) : 0f);
        return new Vector3(x, y, z);
    }
    static float CarDelta(float t) // ileri yönden sağa sapma (derece)
    {
        Vector3 a = CarPos(t - 0.05f), b = CarPos(t + 0.05f);
        float dx = a.x - b.x, dz = b.z - a.z;
        return dx < 0.01f ? 0f : Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
    }
    static float FinalDelta;
    static P CarWorld(float t)
    {
        float delta = t < StopTime - 0.2f ? CarDelta(t) : FinalDelta;
        float nod = t > 13.4f && t < 14.6f ? Mathf.Sin((t - 13.4f) / 1.2f * Mathf.PI) * 1.2f : 0f;
        return new P(CarPos(t), Quaternion.Euler(nod, -90f + delta, 0f));
    }

    // ------------------------------------------------------------------ eller ve kollar
    // El poz anahtarları (kavrama merkezi + yön) yazılır; kol her karede iki kemikli IK ile
    // omuzdan bileğe çözülür. Elin ileri yönü dirsekten kavrama noktasına bakar (bilek doğal düz kalır).
    static readonly List<Key> handL = new List<Key>(), handR = new List<Key>();
    static P docsInR, docsInL, flashInR;

    // build_arms.py ile aynı ölçüler (araba yereli ≈ metre)
    const float UpperLen = 0.34f, ForeLen = 0.31f;
    static readonly Vector3 WristR = new Vector3(0.020f, 0f, -0.118f);
    static readonly Vector3 PinchR = new Vector3(-0.022f, 0.042f, 0.024f);   // başparmak ile işaret parmağı arası
    static readonly string[] HandShapes = { "Grip", "Relax", "Pinch", "Open", "Hold" };
    static Vector3 WristLocal(bool left) => left ? new Vector3(-WristR.x, WristR.y, WristR.z) : WristR;

    static float YawOf(Quaternion q) { var f = q * Vector3.forward; return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }

    // Gövde: otururken kafanın dönüşünün ancak çeyreği kadar döner, ayağa kalkınca kafayla aynı yöne bakar.
    static Quaternion Torso(float t)
    {
        float yaw = YawOf(HeadAt(t).r);
        float stand = Smooth((t - 28.4f) / 2.6f);
        return Quaternion.Euler(0, Mathf.LerpAngle(yaw * 0.25f, yaw, stand), 0);
    }
    static Vector3 ShoulderAt(float t, bool left) =>
        HeadAt(t).p + Torso(t) * new Vector3(left ? -0.20f : 0.20f, -0.32f, 0f);
    // Dirsek dışa, aşağı ve geriye bakar (yana uzanırken aşağı değil geri gider)
    // Sağ kol yolcu koltuğuna uzanırken dirsek geriye ve hafif yukarı gider (koltuğun içine düşmesin).
    static Vector3 Pole(float t, bool left)
    {
        Vector3 basePole = new Vector3(left ? -0.5f : 0.5f, -0.7f, -0.6f);
        // Otururken sol dirsek dışa (koltuk ile kapı arasına), minderin içine düşmesin
        if (left) basePole = Vector3.Lerp(basePole, new Vector3(-0.72f, -0.35f, -0.5f), Smooth((t - 16.4f) / 0.6f) * (1f - Smooth((t - 28.4f) / 2.6f)));
        float reach = left ? 0f : Smooth((t - 16.6f) / 0.8f) * (1f - Smooth((t - 23.0f) / 0.6f));
        return Torso(t) * Vector3.Lerp(basePole, new Vector3(0.3f, 0.3f, -1f), reach);
    }

    // İki kemik IK. Bilek uzanamayacak kadar uzaktaysa omuz öne kayar (gövde öne eğilir).
    static void SolveArm(Vector3 s, Vector3 w, Vector3 pole, out Vector3 shoulder, out Vector3 elbow)
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
        shoulder = s;
        elbow = s + dir * (UpperLen * cosA) + perp.normalized * (UpperLen * Mathf.Sqrt(1f - cosA * cosA));
    }

    // make(ref): ref noktasından bakan el pozu. ref önce omuz, sonra çözülen dirsek olur (birkaç tur).
    static P Solve(float t, bool left, Func<Vector3, P> make)
    {
        Vector3 s = ShoulderAt(t, left), pole = Pole(t, left);
        P h = make(s);
        for (int i = 0; i < 4; i++)
        {
            SolveArm(s, h.p + h.r * WristLocal(left), pole, out _, out var elbow);
            h = make(elbow);
        }
        return h;
    }

    static P UpperAt(float t, bool left)
    {
        P h = SampleKeys(left ? handL : handR, t);
        SolveArm(ShoulderAt(t, left), h.p + h.r * WristLocal(left), Pole(t, left), out var s, out var e);
        // Yukarı ipucu geriye eğik: kol aşağı sarkarken de (ileri = aşağı) dönüş kararlı kalır
        return new P(s, Quaternion.LookRotation(e - s, Torso(t) * new Vector3(0f, 0.5f, -1f)));
    }
    static P ForeAt(float t, bool left)
    {
        P h = SampleKeys(left ? handL : handR, t);
        Vector3 w = h.p + h.r * WristLocal(left);
        SolveArm(ShoulderAt(t, left), w, Pole(t, left), out _, out var e);
        return new P(e, Quaternion.LookRotation(w - e, h.r * Vector3.up));
    }

    // Kavrama: "axis" (direksiyon jantı, fener, kapı kolu) elin Y ekseninden geçer, parmaklar ref'ten uzağa bakar.
    static P Grip(Vector3 pos, Vector3 axis, Vector3 from)
    {
        Vector3 f = Vector3.ProjectOnPlane(pos - from, axis);
        return new P(pos, Quaternion.LookRotation(f, axis));
    }
    // Yumuşak kavrama: bilek ön kolla hizaya yaklaşır, nesne ekseni yumruğun içinde biraz eğik kalabilir (direksiyon).
    static P GripSoft(Vector3 pos, Vector3 axis, Vector3 from, float straight)
    {
        Vector3 f = Vector3.Slerp(Vector3.ProjectOnPlane(pos - from, axis).normalized, (pos - from).normalized, straight);
        return new P(pos, Quaternion.LookRotation(f, Vector3.ProjectOnPlane(axis, f)));
    }
    // Gevşek yumruk (Hold): kavranan silindirin (r≈0.021) ekseni el yerelinde bu noktadan +Y boyunca geçer.
    static Vector3 HoldC(bool left) => new Vector3(left ? 0.018f : -0.018f, 0f, -0.032f);
    // Hold ile kavrayış: c = silindir ekseni üzerinde nokta, palmDir = eksenden avuca doğru.
    static P HoldPose(Vector3 c, Vector3 axis, Vector3 palmDir, bool left)
    {
        Vector3 y = axis.normalized;
        Vector3 x = Vector3.ProjectOnPlane(left ? -palmDir : palmDir, y).normalized;
        var r = Quaternion.LookRotation(Vector3.Cross(x, y), y);
        return new P(c - r * HoldC(left), r);
    }
    // Avuç bir yüzeye bakar (faceDir = avuçtan yüzeye). Parmaklar ref'ten uzağa, yüzeye paralel.
    static P PalmFacing(Vector3 pos, Vector3 faceDir, Vector3 from, bool left)
    {
        Vector3 x = left ? faceDir.normalized : -faceDir.normalized;
        Vector3 z = Vector3.ProjectOnPlane(pos - from, x).normalized;
        return new P(pos, Quaternion.LookRotation(z, Vector3.Cross(z, x)));
    }
    // Hold el modelinin köşe noktaları (el yereli): kavrayış açısı seçerken koltuğa girmesin diye
    static Vector3[] holdVerts;
    static Vector3[] HoldVerts()
    {
        if (holdVerts != null) return holdVerts;
        var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsFbx).transform.Find("Hand_R_Hold").GetComponent<MeshFilter>().sharedMesh;
        var v = mesh.vertices;
        var q = Quaternion.Euler(0, 180, 0);
        var list = new List<Vector3>();
        for (int i = 0; i < v.Length; i += 3) list.Add(q * v[i]);
        return holdVerts = list.ToArray();
    }

    // Serbest el: ileri yön tam ref'ten ele, başparmak yaklaşık axisHint yönünde.
    static P GripArm(Vector3 pos, Vector3 axisHint, Vector3 from)
    {
        Vector3 f = pos - from;
        return new P(pos, Quaternion.LookRotation(f, Vector3.ProjectOnPlane(axisHint, f)));
    }

    // Fener elde: fenerin kendi ekseni etrafındaki dönüşü görünmez; bileği en düz tutan açı seçilir.
    // flashView: fenerin kafaya göre pozu (son karede oyundaki fenerle birebir aynı).
    static P HoldFlash(float t, P flashView) => HoldFlashCar(t, HeadAt(t) * flashView);
    static P HoldFlashCar(float t, P flashCar)
    {
        P best = default;
        Vector3 s = ShoulderAt(t, false), pole = Pole(t, false);
        float bestBend = float.MaxValue;
        for (int roll = 0; roll < 360; roll += 10)
        {
            P flash = flashCar * new P(Vector3.zero, Quaternion.AngleAxis(roll, Vector3.forward));
            P hand = flash * flashInR.Inv();
            Vector3 w = hand.p + hand.r * WristLocal(false);
            SolveArm(s, w, pole, out _, out var e);
            float bend = Vector3.Angle(w - e, hand.r * Vector3.forward);
            if (bend < bestBend) { bestBend = bend; best = hand; }
        }
        return best;
    }

    // Görüşe bağlı noktalar (kafa çerçevesinden araba çerçevesine)
    static Vector3 HV(float t, Vector3 v) { var h = HeadAt(t); return h.p + h.r * v; }
    static Vector3 HD(float t, Vector3 d) => HeadAt(t).r * d;

    static void KeyA(List<Key> keys, float t, bool left, Func<Vector3, P> make) =>
        keys.Add(new Key(t, false, Solve(t, left, make)));
    static void KeyV(List<Key> keys, float t, bool left, Func<Vector3, P> make) =>
        keys.Add(new Key(t, true, HeadAt(t).Inv() * Solve(t, left, make)));
    static void KeyFixed(List<Key> keys, float t, P pose) => keys.Add(new Key(t, false, pose));

    const float T_DocsTuck = 26.0f;
    static readonly Vector3 LeftHangView = new Vector3(-0.24f, -0.62f, 0.22f);   // sol el yanda, kadraj dışında (ayaktayken)
    // Oturuyorken sol el sol uyluğun üstünde (minder 0.85 + uyluk ~0.13), araba çerçevesinde
    static readonly Vector3 LeftLap = new Vector3(-0.50f, 1.00f, -0.02f);
    // Göğüs çerçevesinde, sol koltuk altı ile gövde arası (kol uzansa da yerinde kalır)
    static readonly P DocsTuckInChest = new P(new Vector3(-0.14f, -0.12f, 0.04f), Quaternion.Euler(0, 0, 90));
    static P ChestAt(float t) => new P(HeadAt(t).p + Torso(t) * new Vector3(0f, -0.32f, 0f), Torso(t));

    // Simit üzerinde saat 10 ve 2 (yataydan 50°): eller kadraja girer. Merkez (-0.40, 1.26, 0.20), r 0.19, simit 31° yatık.
    static readonly Vector3 WheelGripL = new Vector3(-0.522f, 1.3845f, 0.2753f), WheelGripR = new Vector3(-0.278f, 1.3845f, 0.2753f);
    static readonly Vector3 WheelTanL = new Vector3(0.766f, 0.551f, 0.333f), WheelTanR = new Vector3(-0.766f, 0.551f, 0.333f);
    static readonly Vector3 WheelCenter = new Vector3(-0.40f, 1.26f, 0.20f);

    static P DoorRot(float t)
    {
        float open = Smooth((t - T_DoorStart) / (T_DoorEnd - T_DoorStart));
        float c = Mathf.Clamp01((t - T_DoorCloseStart) / (T_DoorClosed - T_DoorCloseStart));
        float a = open * (1f - c * c) * 65f; // kapanışta hızlanır (çarparak kapanır)
        return new P(Vector3.zero, Quaternion.AngleAxis(a, Vector3.up));
    }

    // Kapının dış yüzünde (x = -0.92), arka tarafa yakın itme noktası
    // Oyuncu açık kapının arkasında (iç tarafında) durur: el kapının arka kenarından dolanır, avuç dış yüze yaslanır.
    // Kapı arka kenarı z=-0.595; bilek kenarın dışında kalır, kol kapının içinden geçmez. Parmaklar menteşeye doğru.
    static readonly Vector3 DoorPush = new Vector3(-0.92f, 1.02f, -0.50f);
    static Func<Vector3, P> PushDoor(float t) => from =>
    {
        var q = DoorRot(t).r;
        Vector3 n = q * Vector3.left;                          // kapının dışa bakan normali
        Vector3 pos = Hinge + q * (DoorPush - Hinge) + n * 0.011f;   // açık el başparmağı avuçtan ~1 cm taşar
        Vector3 z = (q * new Vector3(0f, 0.25f, 1f)).normalized;
        Vector3 x = -n;
        return new P(pos, Quaternion.LookRotation(z, Vector3.Cross(z, x)));
    };
    // İç kapı kolu yatay bir çubuk (araba ileri ekseni boyunca); gevşek yumruk altından kavrar:
    // avuç kabin tarafında, parmaklar çubuğun altından kapı tarafına sarılır, kapıyla birlikte döner.
    // Kavrama ekseni çubuktan 8 mm içeride: parmak uçları kapı döşemesine (x=-0.850) girmesin.
    static Func<Vector3, P> AtHandle(float t) => from =>
    {
        var q = DoorRot(t).r;
        Vector3 c = Hinge + q * (DoorHandle + new Vector3(0.008f, 0f, 0f) - Hinge);
        Vector3 n = q * Vector3.left;
        return HoldPose(c, q * Vector3.back, -(n + Vector3.up * 0.3f), true);
    };
    // Anahtar başı dikey bir dil: başparmak solunda, işaret parmağı sağında, avuç aşağı.
    static P KeyPinch(Vector3 from)
    {
        Vector3 y = Vector3.left;
        var r = Quaternion.LookRotation(Vector3.ProjectOnPlane(KeyHead - from, y), y);
        return new P(KeyHead - r * PinchR, r);
    }
    static P KeyTwist(P hand, float deg)
    {
        var q = Quaternion.AngleAxis(-deg, Vector3.forward);
        return new P(KeyPivot + q * (hand.p - KeyPivot), q * hand.r);
    }

    static void BuildHandKeys()
    {
        handL.Clear(); handR.Clear();

        // Evrak: avuç aşağı, dosyanın üstüne kapanır
        Vector3 docsAxis = SeatDocs.r * Vector3.forward;
        Vector3 docsAt = SeatDocs.p + new Vector3(0, 0.035f, 0);
        // Fener: kavrama merkezi gövdenin ortası; elde tutulan pozlar HoldFlash ile (son kare oyundaki fener)
        Vector3 flAxis = SeatFlash.r * Vector3.forward;
        Vector3 flBody = SeatFlash.p + SeatFlash.r * new Vector3(0, 0, -0.03f);
        Vector3 docsAxisV = new Vector3(1f, 0f, 0.3f), upV = new Vector3(0f, 1f, 0.2f);

        // Direksiyon dönünce kavrama noktası da simitle döner (araba yerelinde eksen = modelin 180° çevrilmiş normali)
        Func<float, bool, Func<Vector3, P>> wheelAt = (t, left) => f =>
        {
            Quaternion q = Quaternion.AngleAxis(SteerWheelDeg(t), Quaternion.Euler(0, 180, 0) * SteerNormalModel);
            Vector3 g = left ? WheelGripL : WheelGripR, tan = left ? WheelTanL : WheelTanR;
            return GripSoft(WheelCenter + q * (g - WheelCenter), q * tan, f, 0.7f);
        };
        Action<List<Key>, bool, float> wheelKeys = (keys, left, until) =>
        {
            for (float t = 0f; t < until - 0.01f; t += 0.1f)
                keys.Add(new Key(t, false, Solve(t, left, wheelAt(t, left)), true));
            keys.Add(new Key(until, false, Solve(until, left, wheelAt(until, left)), true));
        };
        // Evrak: açık el, avuç aşağı dosyanın üstüne yatar (başparmak avuçtan ~1 cm taşar, ona göre yükseklik)
        Vector3 docsUp = SeatDocs.r * Vector3.up;
        Vector3 docsTop = SeatDocs.p + docsUp * (0.020f + 0.011f);
        // El dosyanın yakın kenarına (dirseğe doğru 7 cm) yatar: ön kol dosyaya değmez
        Func<Vector3, P> grabDocs = f => PalmFacing(docsTop - Vector3.ProjectOnPlane(docsTop - f, docsUp).normalized * 0.07f, -docsUp, f, false);
        Func<Vector3, P> aboveDocs = f => { var g = grabDocs(f); return new P(g.p + new Vector3(-0.02f, 0.09f, -0.02f), g.r); };
        // Fener: gevşek yumruk; koltuğa girmeyen ve bileği en az büken açı aranır
        Func<Vector3, P> grabFlash = f =>
        {
            P best = default; float bestCost = float.MaxValue;
            Vector3 toElbow = f - flBody;
            for (int a = 0; a < 360; a += 5)
            {
                Vector3 palmDir = Quaternion.AngleAxis(a, flAxis) * Vector3.up;
                P h = HoldPose(flBody, flAxis, palmDir, false);
                float pen = 0f;
                foreach (var v in HoldVerts()) pen += Mathf.Max(0f, 0.857f - (h.p + h.r * v).y);
                float cost = pen * 2000f + Vector3.Angle(h.r * Vector3.forward, -toElbow) * 0.05f
                             + Mathf.Max(0f, -Vector3.Dot(palmDir, Vector3.up)) * 20f;   // avuç alttan gelmesin
                if (cost < bestCost) { bestCost = cost; best = h; }
            }
            return best;
        };
        Func<Vector3, P> aboveFlash = f => { var g = grabFlash(f); return new P(g.p + g.r * new Vector3(0.07f, 0f, -0.02f) + new Vector3(0, 0.03f, 0), g.r); };
        P flashLowView = new P(GameplayFlashInView.p + new Vector3(0.07f, -0.10f, 0.08f), GameplayFlashInView.r);   // sağda: kapıya uzanan sol kol fenere değmesin
        Action<float, P> flashKey = (t, fv) => handR.Add(new Key(t, true, HeadAt(t).Inv() * HoldFlash(t, fv)));
        // Evrak sağ elde: avuç aşağı (hafif ileri), dosya avucun altında
        Func<float, Vector3, Func<Vector3, P>> docsV = (t, v) => f => PalmFacing(HV(t, v), HD(t, new Vector3(0f, -1f, 0.15f)), f, false);
        // Evrak sol elde: avuç yukarı, dosya avucun üstünde
        Func<float, Vector3, Func<Vector3, P>> docsUpL = (t, v) => f => PalmFacing(HV(t, v), HD(t, new Vector3(0f, 1f, -0.1f)), f, true);
        Func<float, Vector3, Func<Vector3, P>> freeL = (t, v) => f => GripArm(HV(t, v), HD(t, upV), f);
        Func<float, Vector3, Func<Vector3, P>> freeR = freeL;
        Func<Vector3, P> lapL = f => PalmFacing(LeftLap, new Vector3(0.25f, -1f, 0f), f, true);
        // Kapıyla birlikte hareket eden el: sık, doğrusal anahtarlar (el kapının arkasında kalmasın)
        Action<List<Key>, bool, float, float, Func<float, Func<Vector3, P>>> dense = (keys, left, t0, t1, at) =>
        {
            for (float t = t0; t < t1 - 0.01f; t += 0.05f) keys.Add(new Key(t, false, Solve(t, left, at(t)), true));
            keys.Add(new Key(t1, false, Solve(t1, left, at(t1)), true));
        };

        // ---- sağ el: direksiyon -> evrak -> sol ele ver -> fener -> kontak -> fener önde
        // Sağ el sürüşte vites topuzunda; durunca el frenini çeker, sonra evraka uzanır
        Func<Vector3, P> gear = f => HoldPose(GearKnob, Vector3.up, new Vector3(-0.75f, 0f, -0.65f), false);
        Func<float, Func<Vector3, P>> hbAt = t => f =>
        {
            Quaternion q = HbRot(t);
            return HoldPose(HbPivot + q * (HbGrip - HbPivot), q * Vector3.forward, q * new Vector3(-0.5f, 1f, 0f), false);
        };
        KeyA(handR, 0f, false, gear);
        KeyA(handR, 13.9f, false, gear);
        KeyA(handR, 14.3f, false, f => { var g = hbAt(14.3f)(f); return new P(g.p + new Vector3(-0.03f, 0.06f, 0f), g.r); });
        dense(handR, false, 14.5f, 15.15f, hbAt);
        KeyA(handR, 15.55f, false, f => { var g = hbAt(15.55f)(f); return new P(g.p + new Vector3(-0.04f, 0.08f, 0f), g.r); });
        KeyA(handR, 17.6f, false, aboveDocs);
        KeyA(handR, 18.6f, false, grabDocs);
        KeyA(handR, T_GrabDocs, false, grabDocs);
        docsInR = Solve(T_GrabDocs, false, grabDocs).Inv() * SeatDocs;
        KeyV(handR, 20.2f, false, docsV(20.2f, new Vector3(0.10f, -0.17f, 0.50f)));
        KeyV(handR, T_DocsToLeft, false, docsV(T_DocsToLeft, new Vector3(0.02f, -0.18f, 0.45f)));
        KeyV(handR, 21.6f, false, freeR(21.6f, new Vector3(0.17f, -0.10f, 0.42f)));   // bıraktıktan sonra yukarı-sağa çekilir
        KeyA(handR, 22.1f, false, aboveFlash);
        KeyA(handR, T_GrabFlash, false, grabFlash);
        flashInR = Solve(T_GrabFlash, false, grabFlash).Inv() * SeatFlash;
        // Kavrar kavramaz kaldırır: fenerin altına dolanan parmaklar minderde kalmasın
        KeyA(handR, T_GrabFlash + 0.15f, false, f => { var g = grabFlash(f); return new P(g.p + new Vector3(-0.01f, 0.05f, 0f), g.r); });
        flashKey(23.4f, flashLowView);
        P keyR = Solve(T_KeyTurnStart, false, KeyPinch);
        KeyFixed(handR, 24.2f, new P(keyR.p + new Vector3(0.01f, -0.02f, -0.06f), keyR.r));
        KeyFixed(handR, T_KeyTurnStart, keyR);
        KeyFixed(handR, T_KeyTurnEnd, KeyTwist(keyR, 35f));
        KeyFixed(handR, 25.25f, KeyTwist(keyR, 35f));
        flashKey(26.0f, flashLowView);
        // Kapıyı açarken kafa sola döner; görüşe bağlı fener de sola savrulup sol kolun yoluna girerdi.
        // Bu arada fener sağ uyluğun üstünde, araba çerçevesinde durur.
        P flashThigh = new P(new Vector3(-0.27f, 1.07f, 0.02f), Quaternion.LookRotation(new Vector3(-0.15f, -0.25f, 1f)));
        handR.Add(new Key(26.5f, false, HoldFlashCar(26.5f, flashThigh)));
        handR.Add(new Key(28.2f, false, HoldFlashCar(28.2f, flashThigh)));
        flashKey(30.0f, flashLowView);
        // Kapıyı iterken fener aşağıya, ayağının önüne: ışık uzanan sol kola yakından vurup parlatmasın
        P flashDownView = new P(flashLowView.p, flashLowView.r * Quaternion.Euler(32f, -12f, 0f));
        flashKey(32.3f, flashDownView);
        flashKey(33.7f, flashDownView);
        flashKey(34.3f, GameplayFlashInView);
        flashKey(T_Handoff, GameplayFlashInView);

        // ---- sol el: direksiyon -> kucak -> evrakı al -> kapı kolu -> evrak önde -> kapıyı it
        wheelKeys(handL, true, 16.4f);
        KeyA(handL, 17.4f, true, lapL);
        KeyA(handL, 20.1f, true, lapL);
        // Evrakı alttan, avuç yukarı alır: el pozu evrakın o anki yerinden (sağ elin altında)
        // Sol avuç dosyanın yakın yarısının altında: dosya parmaklar boyunca uzanır, ön kol dosyanın altına girmez.
        // drop: dosyanın o anki yerinin ne kadar altında (yaklaşırken dosya sağ elde hâlâ hareket ediyor)
        Func<float, float, Func<Vector3, P>> takeDocs = (t, drop) => f =>
        {
            P d = SampleKeys(handR, t) * docsInR;
            Vector3 up = d.r * Vector3.up, c = d.p - up * (0.011f + drop);
            return PalmFacing(c - Vector3.ProjectOnPlane(c - f, up).normalized * 0.10f, up, f, true);
        };
        KeyA(handL, T_DocsToLeft - 0.45f, true, takeDocs(T_DocsToLeft - 0.45f, 0.13f));
        KeyA(handL, T_DocsToLeft, true, takeDocs(T_DocsToLeft, 0f));
        KeyV(handL, 22.0f, true, docsUpL(22.0f, new Vector3(-0.17f, -0.27f, 0.46f)));   // dirsek minderin üstünde kalsın diye yüksekte
        KeyV(handL, 25.4f, true, docsUpL(25.4f, new Vector3(-0.17f, -0.27f, 0.46f)));
        // Kapıyı açmadan önce evrakı koltuğunun altına sıkıştırır (el kadraj altındayken), sol el boşalır
        KeyA(handL, T_DocsTuck, true, lapL);
        // Kola kabin tarafından gelir (döşemeden geçmesin)
        KeyA(handL, T_DoorStart - 0.35f, true, f => { var g = AtHandle(T_DoorStart)(f); return new P(g.p + new Vector3(0.07f, 0.05f, -0.03f), g.r); });
        dense(handL, true, T_DoorStart - 0.1f, 27.3f, AtHandle);
        KeyA(handL, 28.0f, true, lapL);
        KeyV(handL, 31.2f, true, freeL(31.2f, LeftHangView));
        KeyV(handL, 32.2f, true, freeL(32.2f, LeftHangView));
        // Kapının arka kenarının dışından dolanarak gelir (kapının içinden geçmesin)
        KeyA(handL, T_DoorCloseStart - 0.3f, true, f =>
        {
            var g = PushDoor(T_DoorCloseStart - 0.3f)(f); var q = DoorRot(T_DoorCloseStart - 0.3f).r;
            return new P(g.p + q * new Vector3(-0.07f, 0f, -0.16f), g.r);
        });
        dense(handL, true, T_DoorCloseStart - 0.05f, T_DoorCloseStart + 0.3f, PushDoor);
        KeyV(handL, 33.9f, true, freeL(33.9f, LeftHangView));
        KeyV(handL, T_Handoff, true, freeL(T_Handoff, LeftHangView));

        // Evrak sağ elden sol ele geçerken yerinden zıplamasın
        P rAt = SampleKeys(handR, T_DocsToLeft), lAt = SampleKeys(handL, T_DocsToLeft);
        docsInL = lAt.Inv() * (rAt * docsInR);
    }

    // Parmak pozları: aynı orijinli el modelleri arasında geçiş (eldeki eşya kaymaz)
    static void HandShapeCurves(AnimationClip clip, string hand, params (float t, string shape)[] steps)
    {
        foreach (var s in HandShapes)
        {
            var on = new List<(float, bool)>();
            foreach (var st in steps) on.Add((st.t, st.shape == s));
            Active(clip, hand + "/" + s, on.ToArray());
        }
    }

    // ------------------------------------------------------------------ kurulum
    [MenuItem("Tools/Intro Cinematic/Build")]
    public static void Build()
    {
        FinalDelta = CarDelta(StopTime - 0.2f);
        BuildHeadKeys();
        BuildHandKeys();

        SetupMaterials();

        var old = GameObject.Find("IntroCinematic");
        if (old) UnityEngine.Object.DestroyImmediate(old);

        var root = new GameObject("IntroCinematic");
        var car = new GameObject("IntroCar").transform;
        car.SetParent(root.transform, false);
        car.localScale = Vector3.one * K;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/IC_Car4x4.fbx"));
        model.name = "Model";
        model.transform.SetParent(car, false);
        model.transform.localRotation = Quaternion.Euler(0, 180, 0);
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            if (r.name.Contains("Glow")) r.shadowCastingMode = ShadowCastingMode.Off;

        var animator = car.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        var col = car.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.95f, 0.05f);
        col.size = new Vector3(2.0f, 1.9f, 4.6f);

        // Işıklar (menzil ölçekten etkilenmez, dünya birimi)
        var headGlow = model.transform.Find("HeadlightGlow");
        foreach (float x in new[] { -0.53f, 0.53f })
            MakeLight("Headlight", headGlow, car, new Vector3(x, 0.885f, 2.2f), Quaternion.Euler(7, 0, 0),
                LightType.Spot, new Color(1f, 0.92f, 0.75f), 90f, 80f, 55f, LightShadows.None);
        MakeLight("TailLight", model.transform.Find("TaillightGlow"), car, new Vector3(0, 0.95f, -2.1f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.1f, 0.05f), 1.5f, 5f, 0, LightShadows.None);
        MakeLight("DashLight", model.transform.Find("GaugeGlow"), car, new Vector3(-0.1f, 1.30f, 0.25f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.62f, 0.3f), 0.6f, 2.0f, 0, LightShadows.None);
        var dome = new GameObject("DomeLight").transform;
        dome.SetParent(car, false);
        MakeLight("Light", dome, car, new Vector3(0, 1.78f, -0.2f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.85f, 0.6f), 5.0f, 6.5f, 0, LightShadows.None);

        // Koltuktaki eşyalar
        MeshGO("SeatDocs", ModelDir + "/IC_Props.fbx", "Documents", car, SeatDocs);
        MeshGO("SeatFlashlight", ModelDir + "/IC_Props.fbx", "Flashlight", car, SeatFlash);

        // Kafa ve kamera
        var head = new GameObject("Head").transform;
        head.SetParent(car, false);
        var camGO = new GameObject("CinematicCamera");
        camGO.transform.SetParent(head, false);
        var cam = camGO.AddComponent<Camera>();
        var playerGO = GameObject.Find("Player");
        var playerCam = playerGO.transform.Find("CamHolder/Camera").GetComponent<Camera>();
        cam.CopyFrom(playerCam);
        camGO.transform.localPosition = Vector3.zero; // CopyFrom konumu da kopyalar
        camGO.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = playerCam.GetUniversalAdditionalCameraData().renderPostProcessing;
        cam.enabled = false; // oyunda IntroCinematic açar

        // Eller ve eldeki eşyalar
        var hL = HandGO("HandL", "L", car);
        var hR = HandGO("HandR", "R", car);
        var arms = new[]
        {
            MeshGO("UpperL", ArmsFbx, "UpperArm", car, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("ForeL", ArmsFbx, "Forearm", car, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("UpperR", ArmsFbx, "UpperArm", car, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("ForeR", ArmsFbx, "Forearm", car, new P(Vector3.zero, Quaternion.identity)),
        };
        var docsR = MeshGO("DocsR", ModelDir + "/IC_Props.fbx", "Documents", hR.transform, docsInR);
        var docsL = MeshGO("DocsL", ModelDir + "/IC_Props.fbx", "Documents", hL.transform, docsInL);
        var chest = new GameObject("Chest");
        chest.transform.SetParent(car, false);
        ApplyPose(chest.transform, ChestAt(0f), false);
        var docsTuck = MeshGO("DocsTuck", ModelDir + "/IC_Props.fbx", "Documents", chest.transform, DocsTuckInChest);
        var flashR = MeshGO("FlashR", ModelDir + "/IC_Props.fbx", "Flashlight", hR.transform, flashInR);
        var beam = new GameObject("Beam").transform;
        beam.SetParent(flashR.transform, false);
        beam.localPosition = new Vector3(0, 0, 0.13f);
        // Flashlight.cs ile aynı değerler
        AddLight(beam.gameObject, LightType.Spot, new Color(1f, 0.95f, 0.85f), 120f, 70f, 70f, LightShadows.Soft);
        var spill = new GameObject("Spill");
        spill.transform.SetParent(beam, false);
        AddLight(spill, LightType.Spot, new Color(1f, 0.95f, 0.85f), 15f, 20f, 130f, LightShadows.None);
        foreach (var g in new[] { hL, hR, docsR, docsL, docsTuck, flashR, arms[0], arms[1], arms[2], arms[3] })
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                mr.shadowCastingMode = ShadowCastingMode.Off; // fener kendi ışığını kesmesin

        // Animasyon + Timeline
        var clip = BuildClip();
        var timeline = BuildTimeline(clip, out var track);
        SetupAudioImport();
        BuildAudio(timeline);
        var director = root.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.SetGenericBinding(track, animator);

        // Ses izleri mixer'ın Cinematic grubuna giden kaynaklara bağlansın
        var cineGroup = GameAudio.Group(AudioChannel.Cinematic);
        if (!cineGroup) Debug.LogWarning("Cinematic mixer grubu yok; önce Tools > Audio > Build Mixer çalıştır.");
        foreach (var t in timeline.GetOutputTracks())
        {
            if (!(t is AudioTrack)) continue;
            var srcGO = new GameObject("Audio_" + t.name);
            srcGO.transform.SetParent(root.transform, false);
            var src = srcGO.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = cineGroup;
            director.SetGenericBinding(t, src);
        }
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.playOnAwake = true;

        // Editörde ilk kareyi göster
        ApplyPose(car, CarWorld(0f), true);
        ApplyPose(head, HeadAt(0f), false);
        ApplyPose(hL.transform, SampleKeys(handL, 0f), false);
        ApplyPose(hR.transform, SampleKeys(handR, 0f), false);
        ApplyPose(arms[0].transform, UpperAt(0f, true), false);
        ApplyPose(arms[1].transform, ForeAt(0f, true), false);
        ApplyPose(arms[2].transform, UpperAt(0f, false), false);
        ApplyPose(arms[3].transform, ForeAt(0f, false), false);
        foreach (var h in new[] { hL, hR })
            foreach (var s in HandShapes) h.transform.Find(s).gameObject.SetActive(s == "Grip");
        docsR.SetActive(false); docsL.SetActive(false); docsTuck.SetActive(false); flashR.SetActive(false);
        beam.gameObject.SetActive(false); dome.gameObject.SetActive(false);

        var fade = SetupFade();

        // Oyuna bağlantı
        var ic = root.AddComponent<IntroCinematic>();
        ic.director = director;
        ic.cinematicCamera = cam;
        ic.head = head;
        ic.hideOnHandoff = new[] { hL, hR, arms[0], arms[1], arms[2], arms[3], chest };
        ic.player = playerGO.transform;
        ic.playerCamera = playerCam;
        var camHolder = playerGO.transform.Find("CamHolder");
        var off = new List<Behaviour>
        {
            camHolder.GetComponent<MouseLook>(),
            playerGO.GetComponent<PlayerMovement>(),
            playerGO.GetComponent<HeadBob>(),
            camHolder.GetComponent<HeadBob>(),
            playerCam.GetComponent<PlayerInteractor>(),
        };
        off.RemoveAll(b => b == null);
        ic.disableDuringCinematic = off.ToArray();
        ic.flashlight = playerCam.GetComponent<Flashlight>();
        ic.handoffTime = T_Handoff;
        ic.fade = fade;

        // Gece ortam sesi: sinematikten bağımsız, oyunda da çalmaya devam eder
        var oldAmb = GameObject.Find("NightAmbience");
        if (oldAmb) UnityEngine.Object.DestroyImmediate(oldAmb);
        var amb = new GameObject("NightAmbience");
        var crickets = Ambience(amb.transform, "Crickets", "Ambience_Crickets", 0.55f, 1.0f, 26.0f, 2.5f, new Vector2(0.98f, 1.02f));
        var wind = Ambience(amb.transform, "Wind", "Ambience_Wind", 0.22f, 0.3f, 7.4f, 1.5f, new Vector2(0.85f, 1.05f));
        ic.ambience = new[] { crickets, wind };
        ic.ambienceOpenStart = T_DoorStart;
        ic.ambienceOpenEnd = T_DoorEnd;
        Undo.RegisterCreatedObjectUndo(amb, "Build Intro Cinematic");

        // Oyundaki fener de aynı tık sesini kullansın (boşsa)
        if (ic.flashlight && !ic.flashlight.clickSound)
        {
            Undo.RecordObject(ic.flashlight, "Flashlight click");
            ic.flashlight.clickSound = Sfx("Flashlight_Click");
            EditorUtility.SetDirty(ic.flashlight);
        }

        Undo.RegisterCreatedObjectUndo(root, "Build Intro Cinematic");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("Intro cinematic kuruldu. Süre: " + T_Handoff + " sn. Timeline: " + CineDir + "/IntroCinematic.playable");
    }

    static void ApplyPose(Transform t, P p, bool world)
    {
        if (world) { t.position = p.p; t.rotation = p.r; }
        else { t.localPosition = p.p; t.localRotation = p.r; }
    }

    static string ArmsFbx => ModelDir + "/IC_Arms.fbx";

    // El: pozu animasyonla sürülen boş obje + her parmak pozu için bir model (yalnızca biri açık)
    static GameObject HandGO(string name, string side, Transform car)
    {
        var go = new GameObject(name);
        go.transform.SetParent(car, false);
        foreach (var s in HandShapes)
            MeshGO(s, ArmsFbx, "Hand_" + side + "_" + s, go.transform, new P(Vector3.zero, Quaternion.identity));
        return go;
    }

    static GameObject MeshGO(string name, string fbx, string child, Transform parent, P local)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx).transform.Find(child);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ApplyPose(go.transform, local, false);
        var m = new GameObject("Mesh");
        m.transform.SetParent(go.transform, false);
        m.transform.localRotation = Quaternion.Euler(0, 180, 0); // Blender -> Unity yönü
        m.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        m.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
        return go;
    }

    static void MakeLight(string name, Transform parent, Transform car, Vector3 carPos, Quaternion carRot,
        LightType type, Color c, float intensity, float range, float angle, LightShadows sh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = car.TransformPoint(carPos);
        go.transform.rotation = car.rotation * carRot;
        AddLight(go, type, c, intensity, range, angle, sh);
    }

    static void AddLight(GameObject go, LightType type, Color c, float intensity, float range, float angle, LightShadows sh)
    {
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        if (type == LightType.Spot) { l.spotAngle = angle; l.innerSpotAngle = angle * 0.6f; }
        l.shadows = sh;
        l.shadowBias = 0.02f;
        l.shadowNormalBias = 0.3f;
        l.renderMode = LightRenderMode.ForcePixel;
    }

    static Image SetupFade()
    {
        var canvas = GameObject.Find("Canvas");
        var existing = canvas.transform.Find("IntroFade");
        if (existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        var go = new GameObject("IntroFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        var img = go.GetComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    // ------------------------------------------------------------------ animasyon klibi
    static AnimationClip BuildClip()
    {
        var clip = new AnimationClip { frameRate = FPS };
        int n = Mathf.CeilToInt(T_Handoff * FPS) + 1;

        AddPoseCurves(clip, "", n, CarWorld);
        AddPoseCurves(clip, "Head", n, HeadAt);
        AddPoseCurves(clip, "HandL", n, t => SampleKeys(handL, t));
        AddPoseCurves(clip, "HandR", n, t => SampleKeys(handR, t));
        AddPoseCurves(clip, "Chest", n, ChestAt);
        AddPoseCurves(clip, "UpperL", n, t => UpperAt(t, true));
        AddPoseCurves(clip, "ForeL", n, t => ForeAt(t, true));
        AddPoseCurves(clip, "UpperR", n, t => UpperAt(t, false));
        AddPoseCurves(clip, "ForeR", n, t => ForeAt(t, false));
        HandShapeCurves(clip, "HandR", (0f, "Hold"), (14.05f, "Open"), (14.5f, "Hold"), (15.3f, "Relax"), (18.1f, "Open"), (21.15f, "Relax"), (22.05f, "Open"), (22.6f, "Hold"), (24.05f, "Pinch"), (25.35f, "Hold"));
        HandShapeCurves(clip, "HandL", (0f, "Grip"), (16.7f, "Relax"), (20.6f, "Open"), (26.05f, "Relax"), (26.3f, "Open"), (26.6f, "Hold"), (27.45f, "Relax"), (32.5f, "Open"), (33.6f, "Relax"));

        float r = 0.40f * K;
        Func<float, Quaternion> spin = t => Quaternion.AngleAxis(-Distance(t) / r * Mathf.Rad2Deg, Vector3.right);
        AddRotCurves(clip, "Model/Wheel_FL", n, t => Quaternion.AngleAxis(Steer(t), Vector3.up) * spin(t));
        AddRotCurves(clip, "Model/Wheel_FR", n, t => Quaternion.AngleAxis(Steer(t), Vector3.up) * spin(t));
        AddRotCurves(clip, "Model/Wheel_RL", n, spin);
        AddRotCurves(clip, "Model/Wheel_RR", n, spin);
        AddRotCurves(clip, "Model/SteeringWheel", n, t => Quaternion.AngleAxis(SteerWheelDeg(t), SteerNormalModel));
        AddRotCurves(clip, "Model/Door_L", n, t => DoorRot(t).r);
        Quaternion m180 = Quaternion.Euler(0, 180, 0);
        AddRotCurves(clip, "Model/Handbrake", n, t => Quaternion.Inverse(m180) * HbRot(t) * m180);
        AddRotCurves(clip, "Model/IgnitionKey", n, t => Quaternion.AngleAxis(Smooth((t - T_KeyTurnStart) / (T_KeyTurnEnd - T_KeyTurnStart)) * 35f, Vector3.forward));

        Active(clip, "Model/HeadlightGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "Model/TaillightGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "Model/GaugeGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "DomeLight", (0f, false), (T_DomeOn, true), (T_DoorClosed, false));
        Active(clip, "SeatDocs", (0f, true), (T_GrabDocs, false));
        Active(clip, "HandR/DocsR", (0f, false), (T_GrabDocs, true), (T_DocsToLeft, false));
        Active(clip, "HandL/DocsL", (0f, false), (T_DocsToLeft, true), (T_DocsTuck, false));
        Active(clip, "Chest/DocsTuck", (0f, false), (T_DocsTuck, true));
        Active(clip, "SeatFlashlight", (0f, true), (T_GrabFlash, false));
        Active(clip, "HandR/FlashR", (0f, false), (T_GrabFlash, true));
        Active(clip, "HandR/FlashR/Beam", (0f, false), (T_FlashOn, true));

        clip.EnsureQuaternionContinuity();
        MakeFolder(CineDir);
        string path = CineDir + "/IntroCinematic_Anim.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // Ön teker açısı (derece): yolun eğriliğinden (hıza bağlı değil), Ackermann: atan(dingil arası * eğrilik).
    // Araba yavaşlarken açı savrulmasın diye ±0.5 sn ağırlıklı ortalanır; durunca düz kalır.
    const float SteerRatio = 15f;                  // direksiyon / teker
    static float SteerRaw(float t)
    {
        if (t > 13.6f) return 0f;                  // şerit değişimi 13.6'da biter
        float ds = Distance(t + 0.15f) - Distance(t - 0.15f);
        if (ds < 0.05f) return 0f;
        float k = (CarDelta(t + 0.15f) - CarDelta(t - 0.15f)) * Mathf.Deg2Rad / ds;   // 1/m (dünya)
        return Mathf.Atan(2.56f * K * k) * Mathf.Rad2Deg;
    }
    static float Steer(float t)
    {
        float sum = 0f, w = 0f;
        for (float o = -0.5f; o <= 0.501f; o += 0.05f) { float k = 1f - Mathf.Abs(o); sum += SteerRaw(t + o) * k; w += k; }
        return Mathf.Clamp(sum / w, -35f, 35f);
    }
    static float SteerWheelDeg(float t) => Steer(t) * SteerRatio;

    static void AddPoseCurves(AnimationClip clip, string path, int n, Func<float, P> f)
    {
        var pos = new Vector3[n]; var rot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            var p = f(i / FPS);
            pos[i] = p.p; rot[i] = p.r;
        }
        clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", Curve(n, i => pos[i].x));
        clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", Curve(n, i => pos[i].y));
        clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", Curve(n, i => pos[i].z));
        WriteRot(clip, path, rot);
    }

    static void AddRotCurves(AnimationClip clip, string path, int n, Func<float, Quaternion> f)
    {
        var rot = new Quaternion[n];
        for (int i = 0; i < n; i++) rot[i] = f(i / FPS);
        WriteRot(clip, path, rot);
    }

    static void WriteRot(AnimationClip clip, string path, Quaternion[] rot)
    {
        for (int i = 1; i < rot.Length; i++)
            if (Quaternion.Dot(rot[i - 1], rot[i]) < 0) rot[i] = new Quaternion(-rot[i].x, -rot[i].y, -rot[i].z, -rot[i].w);
        int n = rot.Length;
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", Curve(n, i => rot[i].x));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", Curve(n, i => rot[i].y));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", Curve(n, i => rot[i].z));
        clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", Curve(n, i => rot[i].w));
    }

    static AnimationCurve Curve(int n, Func<int, float> v)
    {
        var keys = new Keyframe[n];
        float dt = 1f / FPS;
        for (int i = 0; i < n; i++)
        {
            float prev = v(Mathf.Max(i - 1, 0)), next = v(Mathf.Min(i + 1, n - 1));
            float tan = (next - prev) / (dt * ((i == 0 || i == n - 1) ? 1f : 2f));
            keys[i] = new Keyframe(i * dt, v(i), tan, tan);
        }
        return new AnimationCurve(keys);
    }

    static void Active(AnimationClip clip, string path, params (float t, bool on)[] steps)
    {
        var curve = new AnimationCurve();
        foreach (var s in steps) curve.AddKey(new Keyframe(s.t, s.on ? 1 : 0, float.PositiveInfinity, float.PositiveInfinity));
        curve.AddKey(new Keyframe(T_Handoff, steps[steps.Length - 1].on ? 1 : 0, float.PositiveInfinity, float.PositiveInfinity));
        clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);
    }

    static TimelineAsset BuildTimeline(AnimationClip clip, out AnimationTrack track)
    {
        string path = CineDir + "/IntroCinematic.playable";
        AssetDatabase.DeleteAsset(path);
        var tl = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(tl, path);
        track = tl.CreateTrack<AnimationTrack>(null, "Araba, Kamera ve Eller");
        track.trackOffset = TrackOffset.ApplyTransformOffsets;
        track.position = Vector3.zero;
        track.rotation = Quaternion.identity;
        var tc = track.CreateClip(clip);
        tc.displayName = "Giris";
        tc.start = 0;
        tc.duration = T_Handoff;
        ((AnimationPlayableAsset)tc.asset).removeStartOffset = false;
        EditorUtility.SetDirty(tl);
        AssetDatabase.SaveAssets();
        return tl;
    }

    // ------------------------------------------------------------------ ses
    const string SfxDir = "Assets/Sfx/Intro";

    static AudioClip Sfx(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + name + ".mp3");

    static void SetupAudioImport()
    {
        foreach (var n in new[] { "Car_ArriveStopDoor", "Footsteps_Gravel", "Flashlight_Click", "Dog_DistantBark", "Ambience_Wind", "Ambience_Crickets" })
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(SfxDir + "/" + n + ".mp3");
            if (!imp) continue;
            bool longClip = n.StartsWith("Ambience") || n.StartsWith("Footsteps") || n.StartsWith("Car");
            var s = imp.defaultSampleSettings;
            var want = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            if (s.loadType == want && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
            s.loadType = want;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    static AmbienceLoop Ambience(Transform parent, string name, string clip, float vol, float loopStart, float loopEnd, float xfade, Vector2 pitch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var a = go.AddComponent<AmbienceLoop>(); // AudioSource + AudioLowPassFilter'ı RequireComponent ekler
        a.clip = Sfx(clip);
        a.volume = vol;
        a.loopStart = loopStart;
        a.loopEnd = loopEnd;
        a.crossfade = xfade;
        a.pitchRange = pitch;
        a.muffle = 1f;
        return a;
    }

    static void AudioClipOn(AudioTrack tr, AudioClip c, double start, double clipIn, double dur, double vol, double easeIn = 0, double easeOut = 0)
    {
        var tc = tr.CreateClip(c);
        tc.start = start;
        tc.clipIn = clipIn;
        tc.duration = dur;
        tc.easeInDuration = easeIn;
        tc.easeOutDuration = easeOut;
        var so = new SerializedObject(tc.asset);
        so.FindProperty("m_ClipProperties.volume").floatValue = (float)vol;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Kaynak: Car_ArriveStopDoor.mp3 -> 3-9.8 sn motor, ~9.9 sn kontak kapanışı, 11.1-11.4 kapı açılışı, 13.9 sn kapı çarpması
    static void BuildAudio(TimelineAsset tl)
    {
        var car = Sfx("Car_ArriveStopDoor");
        var engA = tl.CreateTrack<AudioTrack>(null, "Motor A");
        var engB = tl.CreateTrack<AudioTrack>(null, "Motor B");
        var door = tl.CreateTrack<AudioTrack>(null, "Kapi");
        var feet = tl.CreateTrack<AudioTrack>(null, "Ayak Sesleri");
        var fx = tl.CreateTrack<AudioTrack>(null, "Fener ve Kopek");

        // Motor: 3.5-9.5 sn arası düzgün bölümü iki izde çapraz geçişle döndür; durunca rölantide kıs
        const double segIn = 3.5, segLen = 6.0, overlap = 0.6;
        double engineOff = T_KeyTurnEnd - 0.5; // kontak sesi bu andan 0.4 sn sonra
        double t = 0; int i = 0;
        while (t < engineOff)
        {
            double len = System.Math.Min(segLen, engineOff + 0.3 - t);
            float vol = t + len * 0.5 < StopTime ? 0.75f : 0.45f;
            AudioClipOn(i % 2 == 0 ? engA : engB, car, t, i == 0 ? 3.0 : segIn, len, vol, i == 0 ? 0 : overlap, overlap);
            i++;
            if (len < segLen) break; // son (kırpılmış) parça
            t += len - overlap;
        }
        // Kontak kapanışı (motor sönmesi)
        AudioClipOn(i % 2 == 0 ? engA : engB, car, engineOff, 9.3, 1.2, 0.55, 0.3, 0.3);

        // Kapı açılışı ve kapanışı
        AudioClipOn(door, car, T_DoorStart - 0.2, 10.9, 1.1, 0.9, 0, 0.2);
        AudioClipOn(door, car, T_DoorClosed - 0.2, 13.7, 1.6, 1.0, 0, 0.4);

        // Ayak sesleri: araçtan iniş, kapıya dönüş, yürüyüş
        var steps = Sfx("Footsteps_Gravel");
        AudioClipOn(feet, steps, 29.0, 0.6, 2.6, 0.7, 0, 0.3);
        AudioClipOn(feet, steps, 32.4, 3.4, 1.2, 0.5, 0, 0.3);
        AudioClipOn(feet, steps, T_Walk1Start, 6.0, T_Walk1End - T_Walk1Start + 0.2, 0.7, 0.2, 0.5);
        AudioClipOn(feet, steps, T_WalkStart, 6.0, T_WalkEnd - T_WalkStart + 0.2, 0.8, 0.2, 0.5);

        // Fener tıkı ve uzaktan köpek havlaması (etrafa bakarken)
        AudioClipOn(fx, Sfx("Flashlight_Click"), T_FlashOn - 0.03, 0, 0.8, 0.8);
        AudioClipOn(fx, Sfx("Dog_DistantBark"), T_Bark, 0, 6.3, 0.35, 0, 0.5);

        EditorUtility.SetDirty(tl);
        AssetDatabase.SaveAssets();
    }

    // ------------------------------------------------------------------ malzemeler
    static void SetupMaterials()
    {
        MakeFolder(ModelDir + "/Materials");
        var mats = new Dictionary<string, Material>
        {
            ["IC_CarPaint"] = Mat("IC_CarPaint", "IC_CarPaint", Color.white, 0.5f, 0.15f, 0.25f),
            ["IC_DarkPlastic"] = Mat("IC_DarkPlastic", "IC_DarkPlastic", Color.white, 1f, 0f, 0.2f),
            ["IC_SeatFabric"] = Mat("IC_SeatFabric", "IC_SeatFabric", Color.white, 2f, 0f, 0.05f),
            ["IC_Rubber"] = Mat("IC_Rubber", "IC_Rubber", Color.white, 1f, 0f, 0.1f),
            ["IC_Metal"] = Mat("IC_Metal", "IC_Metal", Color.white, 1f, 0.8f, 0.45f),
            ["IC_Grille"] = Mat("IC_Grille", "IC_Grille", Color.white, 2f, 0.6f, 0.35f),
            ["IC_Leather"] = Mat("IC_Leather", "IC_Leather", Color.white, 4f, 0f, 0.3f),
            ["IC_Paper"] = Mat("IC_Paper", "IC_Paper", Color.white, 1f, 0f, 0.1f),
            ["IC_Folder"] = Mat("IC_Folder", "IC_Folder", Color.white, 1f, 0f, 0.1f),
            ["IC_Skin"] = Mat("IC_Skin", "IC_Skin", Color.white, 4f, 0f, 0.3f),
            ["IC_Sleeve"] = Mat("IC_Sleeve", "IC_Sleeve", new Color(0.5f, 0.58f, 0.78f), 2f, 0f, 0.1f), // koyu lacivert ceket
            ["IC_Glass"] = Mat("IC_Glass", null, new Color(0.55f, 0.6f, 0.62f, 0.14f), 1f, 0f, 0.92f, alpha: true),
            ["IC_LampLens"] = Mat("IC_LampLens", null, new Color(0.75f, 0.75f, 0.72f), 1f, 0.2f, 0.85f),
            ["IC_TailLens"] = Mat("IC_TailLens", null, new Color(0.35f, 0.04f, 0.03f), 1f, 0f, 0.8f),
            ["IC_HeadlightGlow"] = Mat("IC_HeadlightGlow", null, new Color(1f, 0.95f, 0.85f), 1f, 0f, 0.8f, emission: new Color(1f, 0.92f, 0.78f) * 4f),
            ["IC_TaillightGlow"] = Mat("IC_TaillightGlow", null, new Color(0.6f, 0.05f, 0.03f), 1f, 0f, 0.8f, emission: new Color(1f, 0.08f, 0.04f) * 2f),
            ["IC_GaugeFace"] = Mat("IC_GaugeFace", "IC_GaugeFace", Color.white, 1f, 0f, 0.6f),
            // Işıma haritası = kadran dokusu: çizgiler ve ibre parlar, zemin karanlık kalır
            ["IC_GaugeGlow"] = Mat("IC_GaugeGlow", "IC_GaugeGlow", Color.white, 1f, 0f, 0.6f, emission: Color.white * 1.5f),
            // 4x4 araba
            ["IC_Flare"] = Mat("IC_Flare", "IC_Flare", Color.white, 1f, 0f, 0.15f),
            ["IC_SoftTop"] = Mat("IC_SoftTop", "IC_SoftTop", Color.white, 1f, 0f, 0.05f),
            ["IC_Chrome"] = Mat("IC_Chrome", "IC_Chrome", Color.white, 1f, 1f, 0.7f),
            ["IC_Rim"] = Mat("IC_Rim", "IC_Rim", Color.white, 2f, 0.6f, 0.35f),
            // Flashlight.cs'teki fener renkleri
            ["IC_FlashBody"] = Mat("IC_FlashBody", null, new Color(0.08f, 0.08f, 0.09f), 1f, 0.7f, 0.6f),
            ["IC_FlashLens"] = Mat("IC_FlashLens", null, new Color(0.9f, 0.9f, 0.8f), 1f, 0f, 0.5f),
        };

        foreach (var fbx in new[] { "IC_Car4x4", "IC_Props", "IC_Arms" })
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(ModelDir + "/" + fbx + ".fbx");
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.SaveAndReimport();
        }
    }

    static Material Mat(string name, string tex, Color color, float tile, float metallic, float smooth,
        bool alpha = false, Color? emission = null)
    {
        string path = ModelDir + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        if (tex != null)
        {
            string tp = ModelDir + "/Textures/" + tex + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
            if (ti.maxTextureSize != 512) { ti.maxTextureSize = 512; ti.SaveAndReimport(); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            m.SetTextureScale("_BaseMap", Vector2.one * tile);
        }
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.SetTexture("_EmissionMap", m.GetTexture("_BaseMap"));
            m.SetTextureScale("_EmissionMap", m.GetTextureScale("_BaseMap"));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        if (alpha)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        MakeFolder(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }
}
