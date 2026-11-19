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
    const float T_FlashOn = 35.0f, T_WalkStart = 44.2f, T_WalkEnd = 51.0f, T_Handoff = 51.6f;

    static readonly Vector3 Eye = new Vector3(-0.40f, 1.55f, -0.40f);       // oturan sürücünün gözü
    static readonly Vector3 Stand = new Vector3(-1.75f, 1.60f, -1.05f);     // indikten sonra (göz 2.85 m / K)
    static readonly Vector3 Hinge = new Vector3(-0.95f, 1.15f, 0.70f);
    static readonly Vector3 DoorHandle = new Vector3(-0.89f, 1.24f, 0.25f);
    static readonly Vector3 KeyHead = new Vector3(-0.13f, 1.10f, 0.37f);
    static readonly Vector3 KeyPivot = new Vector3(-0.13f, 1.10f, 0.41f);
    static readonly Vector3 SteerNormalModel = new Vector3(0f, 0.515f, 0.857f);
    static readonly P SeatDocs = new P(new Vector3(0.48f, 0.952f, -0.45f), Quaternion.Euler(0, 15, 0));
    static readonly P SeatFlash = new P(new Vector3(0.22f, 0.975f, -0.25f), Quaternion.Euler(0, 70, 0));
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
        public Key(float t, bool view, P pose) { this.t = t; this.view = view; this.pose = pose; }
    }

    // ------------------------------------------------------------------ kafa (kamera)
    static readonly List<Key> headKeys = new List<Key>();
    static void H(float t, Vector3 pos, float pitch, float yaw, float roll = 0) =>
        headKeys.Add(new Key(t, false, new P(pos, Quaternion.Euler(pitch, yaw, roll))));

    static readonly Vector3[] WalkPath =
    {
        new Vector3(-1.75f, 0, -1.05f), new Vector3(-2.05f, 0, -2.6f), new Vector3(-1.45f, 0, -3.85f),
        new Vector3(-0.40f, 0, -4.40f), new Vector3(0.55f, 0, -4.25f)
    };

    static void BuildHeadKeys()
    {
        headKeys.Clear();
        var E = Eye;
        H(0.0f, E, 3, 0);
        H(4.5f, E, 2, -5);
        H(8.0f, E, 2, 4);
        H(11.0f, E, 3, 0);
        H(13.6f, E + new Vector3(0, 0, 0.03f), 7, 0);              // durunca hafif öne
        H(14.4f, E, 2, 0);
        H(15.6f, E, 3, 2);
        H(17.0f, E + new Vector3(0.06f, -0.03f, 0.04f), 33, 52);   // sağ koltuğa bakış
        H(19.0f, E + new Vector3(0.08f, -0.05f, 0.05f), 36, 55);
        H(21.0f, E + new Vector3(0.04f, -0.04f, 0.03f), 30, 40);
        H(22.6f, E + new Vector3(0.08f, -0.05f, 0.05f), 36, 58);
        H(23.9f, E + new Vector3(0.03f, -0.03f, 0.04f), 27, 18);   // kontağa bakış
        H(25.2f, E + new Vector3(0.03f, -0.03f, 0.04f), 26, 20);
        H(26.2f, E, 8, -10);
        H(26.8f, E + new Vector3(-0.03f, 0, 0), 10, -70);          // kapıya bakış
        H(28.0f, E + new Vector3(-0.05f, 0, 0), 8, -80);
        H(29.0f, new Vector3(-0.85f, 1.45f, -0.45f), 14, -85, 2);  // inerken
        H(30.2f, new Vector3(-1.45f, 1.28f, -0.75f), 12, -100);
        H(31.6f, Stand, 2, -112);                                   // ayağa kalktı
        H(32.5f, Stand + new Vector3(0, -0.04f, 0.02f), 14, -38);   // kapıya dön
        H(33.6f, Stand + new Vector3(0, -0.03f, 0), 10, -42);       // kapı kapandı
        H(34.4f, Stand, 2, -100);
        H(35.4f, Stand, 0, -108);
        H(38.2f, Stand, -1, -55);                                   // yavaşça sola (yolun ilerisi)
        H(39.2f, Stand, -1, -52);
        H(42.8f, Stand, 1, -150);                                   // yavaşça sağa (arkada kalan yol)
        H(44.0f, Stand, 0, -162);
        headKeys.Add(new Key(T_WalkStart, false, WalkPose(0)));
    }

    static Vector3 PathPoint(float u)
    {
        int n = WalkPath.Length - 1;
        float f = Mathf.Clamp01(u) * n;
        int i = Mathf.Min((int)f, n - 1);
        float s = f - i;
        Vector3 p0 = WalkPath[Mathf.Max(i - 1, 0)], p1 = WalkPath[i], p2 = WalkPath[i + 1], p3 = WalkPath[Mathf.Min(i + 2, n)];
        return 0.5f * ((2 * p1) + (-p0 + p2) * s + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s + (-p0 + 3 * p1 - 3 * p2 + p3) * s * s * s);
    }

    static float PathYaw(float u)
    {
        Vector3 d = PathPoint(Mathf.Min(u + 0.02f, 1f)) - PathPoint(Mathf.Max(u - 0.02f, 0f));
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;   // -169 .. 81
        if (yaw > 0) yaw -= 360f;                              // dönüş tek yöne aksın (-169 .. -279)
        return yaw;
    }

    // Yürüyüş: yolda araba arkasından dolanır, sonunda çiftliğe (kuzey) dümdüz bakar.
    static P WalkPose(float u)
    {
        float e = Smooth(u) * 0.5f + u * 0.5f;                 // yavaş başla, yavaş dur
        Vector3 pos = PathPoint(e);
        float dist = 0f;
        for (int i = 1; i <= 40; i++) dist += (PathPoint(e * i / 40f) - PathPoint(e * (i - 1) / 40f)).magnitude;
        float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
        float phase = dist / 0.70f * Mathf.PI;                 // adım boyu 0.7 m
        float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.028f * env;
        pos.y = Stand.y - 0.014f * env + bob;
        float yawLead = PathYaw(Mathf.Min(e + 0.06f, 1f));
        float yaw = Mathf.Lerp(-162f, yawLead, Smooth(u * 4f));
        yaw = Mathf.Lerp(yaw, -270f, Smooth((u - 0.75f) / 0.25f));
        float pitch = Mathf.Lerp(0f, 3f, env) * (1f - Smooth((u - 0.8f) / 0.2f));
        float roll = Mathf.Sin(phase * 0.5f) * 0.6f * env;
        return new P(pos, Quaternion.Euler(pitch, yaw, roll));
    }

    static P HeadAt(float t)
    {
        if (t >= T_WalkStart)
            return WalkPose(Mathf.Clamp01((t - T_WalkStart) / (T_WalkEnd - T_WalkStart)));
        P pose = SampleKeys(headKeys, t);
        if (t < StopTime) // yol titreşimi
            pose.p.y += (Mathf.Sin(t * 13f) * 0.004f + Mathf.Sin(t * 7.3f) * 0.003f) * (1f - Smooth((t - 12f) / 2f));
        return pose;
    }

    static P SampleKeys(List<Key> keys, float t)
    {
        if (t <= keys[0].t) return Resolve(keys[0], t);
        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (t <= keys[i + 1].t)
            {
                float s = Smooth((t - keys[i].t) / Mathf.Max(keys[i + 1].t - keys[i].t, 1e-4f));
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
        float z = Mathf.Lerp(LaneZ, ParkZ, Smooth((t - CruiseEnd) / (13.6f - CruiseEnd)));
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

    // ------------------------------------------------------------------ eller
    static readonly List<Key> handL = new List<Key>(), handR = new List<Key>();
    static P docsInR, docsInL, flashInR;

    // Omuzlar: oturur hâlde araba yerelinde, ayakta/her an için görüşe göre
    // (Gözün epey altında tutulur ki kollar ekranın altından gelsin, kameranın önünden geçmesin.)
    static readonly Vector3 ShoulderR = new Vector3(-0.18f, 1.08f, -0.40f), ShoulderL = new Vector3(-0.62f, 1.08f, -0.40f);
    static readonly Vector3 ShoulderViewR = new Vector3(0.22f, -0.48f, -0.05f), ShoulderViewL = new Vector3(-0.22f, -0.48f, -0.05f);

    // El, "axis" eksenli bir şeyi kavrar; parmaklar omuzdan uzanan çizgi yönünde olur, böylece kol omuza doğru gider.
    static P Grip(Vector3 pos, Vector3 axis, Vector3 shoulder)
    {
        Vector3 f = pos - shoulder;
        f -= Vector3.Project(f, axis);
        return new P(pos, Quaternion.LookRotation(f, axis));
    }

    // Kol öncelikli: önkol tam omza gider, kavrama ekseni ona göre yaklaşık tutulur (direksiyon, kol, evrak).
    static P GripArm(Vector3 pos, Vector3 axisHint, Vector3 shoulder)
    {
        Vector3 f = pos - shoulder;
        return new P(pos, Quaternion.LookRotation(f, axisHint - Vector3.Project(axisHint, f)));
    }

    static P WheelGrip(bool left)
    {
        Vector3 pos = left ? new Vector3(-0.5645f, 1.341f, 0.349f) : new Vector3(-0.2355f, 1.341f, 0.349f);
        Vector3 tangent = left ? new Vector3(0.5f, 0.742f, 0.446f) : new Vector3(-0.5f, 0.742f, 0.446f);
        return GripArm(pos, tangent, left ? ShoulderL : ShoulderR);
    }
    static P DoorRot(float t)
    {
        float open = Smooth((t - T_DoorStart) / (T_DoorEnd - T_DoorStart));
        float c = Mathf.Clamp01((t - T_DoorCloseStart) / (T_DoorClosed - T_DoorCloseStart));
        float a = open * (1f - c * c) * 65f; // kapanışta hızlanır (çarparak kapanır)
        return new P(Vector3.zero, Quaternion.AngleAxis(a, Vector3.up));
    }

    // Kapının dış yüzünde, arka üst köşeye yakın itme noktası
    static readonly Vector3 DoorPush = new Vector3(-0.99f, 1.42f, -0.45f);
    static P PushDoor(float t)
    {
        var q = DoorRot(t).r;
        Vector3 pos = Hinge + q * (DoorPush - Hinge);
        P head = HeadAt(t);
        return GripArm(pos, Vector3.up, head.p + head.r * ShoulderViewL);
    }
    static P AtHandle(float t)
    {
        var q = DoorRot(t).r;
        Vector3 pos = Hinge + q * (DoorHandle - Hinge);
        return GripArm(pos, Vector3.up, ShoulderL);
    }
    static P KeyTwist(P hand, float deg)
    {
        var q = Quaternion.AngleAxis(-deg, Vector3.forward);
        return new P(KeyPivot + q * (hand.p - KeyPivot), q * hand.r);
    }

    static void BuildHandKeys()
    {
        handL.Clear(); handR.Clear();
        P wheelL = WheelGrip(true), wheelR = WheelGrip(false);

        // Sağ el: evrak
        Vector3 docsAxis = SeatDocs.r * Vector3.forward;
        P grabDocs = GripArm(SeatDocs.p + new Vector3(0, 0.035f, 0), docsAxis, ShoulderR);
        P aboveDocs = new P(grabDocs.p + new Vector3(-0.02f, 0.10f, -0.03f), grabDocs.r);
        docsInR = grabDocs.Inv() * SeatDocs;

        // Sağ el: fener (kavrama merkezi gövdenin ortası, el Y ekseni fener ekseni)
        Vector3 flAxis = SeatFlash.r * Vector3.forward;
        Vector3 flBody = SeatFlash.p + SeatFlash.r * new Vector3(0, 0, -0.03f);
        P grabFlash = Grip(flBody, flAxis, ShoulderR);
        P aboveFlash = new P(grabFlash.p + new Vector3(0, 0.10f, -0.03f), grabFlash.r);
        flashInR = grabFlash.Inv() * SeatFlash;

        // Oyundaki fenerle aynı yerde ve yönde biten el pozu (fener kendi ekseninde dönebilir, görünmez)
        Vector3 axisV = GameplayFlashInView.r * Vector3.forward;
        Vector3 gripV = GameplayFlashInView.p - axisV * 0.03f;
        P holdR = Grip(gripV, axisV, ShoulderViewR);
        P lowR = Grip(gripV + new Vector3(-0.02f, -0.06f, -0.05f), axisV, ShoulderViewR);

        Vector3 docsAxisV = new Vector3(1f, 0f, 0.3f);
        P meetR = GripArm(new Vector3(0.02f, -0.18f, 0.45f), docsAxisV, ShoulderViewR);
        P liftR = GripArm(new Vector3(0.10f, -0.17f, 0.50f), docsAxisV, ShoulderViewR);
        P keyR = GripArm(KeyHead + new Vector3(0.02f, -0.02f, -0.04f), Vector3.up, ShoulderR);

        // Sol el
        Vector3 upV = new Vector3(0f, 1f, 0.2f);
        P lowL = GripArm(new Vector3(-0.22f, -0.62f, 0.30f), upV, ShoulderViewL);
        P recvL = GripArm(new Vector3(-0.08f, -0.22f, 0.43f), docsAxisV, ShoulderViewL);
        P holdL = GripArm(new Vector3(-0.20f, -0.34f, 0.40f), upV, ShoulderViewL);

        handR.Add(new Key(0f, false, wheelR));
        handR.Add(new Key(16.0f, false, wheelR));
        handR.Add(new Key(17.6f, false, aboveDocs));
        handR.Add(new Key(18.6f, false, grabDocs));
        handR.Add(new Key(T_GrabDocs, false, grabDocs));
        handR.Add(new Key(20.2f, true, liftR));
        handR.Add(new Key(T_DocsToLeft, true, meetR));
        handR.Add(new Key(21.6f, true, new P(new Vector3(0.15f, -0.25f, 0.45f), meetR.r)));
        handR.Add(new Key(22.1f, false, aboveFlash));
        handR.Add(new Key(T_GrabFlash, false, grabFlash));
        handR.Add(new Key(23.4f, true, lowR));
        handR.Add(new Key(24.3f, false, new P(keyR.p + new Vector3(0, 0, -0.05f), keyR.r)));
        handR.Add(new Key(T_KeyTurnStart, false, keyR));
        handR.Add(new Key(T_KeyTurnEnd, false, KeyTwist(keyR, 35f)));
        handR.Add(new Key(25.9f, true, lowR));
        handR.Add(new Key(34.4f, true, lowR));
        handR.Add(new Key(T_FlashOn - 0.2f, true, holdR));
        handR.Add(new Key(T_Handoff, true, holdR));

        handL.Add(new Key(0f, false, wheelL));
        handL.Add(new Key(16.4f, false, wheelL));
        handL.Add(new Key(17.4f, true, lowL));
        handL.Add(new Key(20.3f, true, lowL));
        handL.Add(new Key(T_DocsToLeft, true, recvL));
        handL.Add(new Key(22.0f, true, holdL));
        handL.Add(new Key(26.2f, true, holdL));
        handL.Add(new Key(T_DoorStart - 0.1f, false, AtHandle(T_DoorStart)));
        handL.Add(new Key(27.3f, false, AtHandle(27.3f)));
        handL.Add(new Key(28.0f, true, holdL));
        handL.Add(new Key(32.2f, true, holdL));
        handL.Add(new Key(T_DoorCloseStart - 0.05f, false, PushDoor(T_DoorCloseStart - 0.05f)));
        handL.Add(new Key(T_DoorCloseStart + 0.3f, false, PushDoor(T_DoorCloseStart + 0.3f)));
        handL.Add(new Key(33.9f, true, holdL));
        handL.Add(new Key(T_Handoff, true, holdL));

        // Evrak sağ elden sol ele geçerken yerinden zıplamasın
        P rAt = SampleKeys(handR, T_DocsToLeft), lAt = SampleKeys(handL, T_DocsToLeft);
        docsInL = lAt.Inv() * (rAt * docsInR);
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

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/IC_Pickup.fbx"));
        model.name = "Model";
        model.transform.SetParent(car, false);
        model.transform.localRotation = Quaternion.Euler(0, 180, 0);
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            if (r.name.Contains("Glow")) r.shadowCastingMode = ShadowCastingMode.Off;

        var animator = car.gameObject.AddComponent<Animator>();
        animator.applyRootMotion = false;
        var col = car.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1.0f, 0);
        col.size = new Vector3(1.95f, 1.6f, 5.4f);

        // Işıklar (menzil ölçekten etkilenmez, dünya birimi)
        var headGlow = model.transform.Find("HeadlightGlow");
        foreach (float x in new[] { -0.64f, 0.64f })
            MakeLight("Headlight", headGlow, car, new Vector3(x, 0.93f, 2.7f), Quaternion.Euler(7, 0, 0),
                LightType.Spot, new Color(1f, 0.92f, 0.75f), 90f, 80f, 55f, LightShadows.None);
        MakeLight("TailLight", model.transform.Find("TaillightGlow"), car, new Vector3(0, 1.05f, -2.8f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.1f, 0.05f), 1.5f, 5f, 0, LightShadows.None);
        MakeLight("DashLight", model.transform.Find("GaugeGlow"), car, new Vector3(-0.1f, 1.30f, 0.25f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.62f, 0.3f), 1.2f, 2.4f, 0, LightShadows.None);
        var dome = new GameObject("DomeLight").transform;
        dome.SetParent(car, false);
        MakeLight("Light", dome, car, new Vector3(0, 1.78f, -0.2f), Quaternion.identity,
            LightType.Point, new Color(1f, 0.85f, 0.6f), 2.0f, 4.5f, 0, LightShadows.None);

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
        var hL = MeshGO("HandL", ModelDir + "/IC_Hands.fbx", "Hand_L", car, new P(Vector3.zero, Quaternion.identity));
        var hR = MeshGO("HandR", ModelDir + "/IC_Hands.fbx", "Hand_R", car, new P(Vector3.zero, Quaternion.identity));
        var docsR = MeshGO("DocsR", ModelDir + "/IC_Props.fbx", "Documents", hR.transform, docsInR);
        var docsL = MeshGO("DocsL", ModelDir + "/IC_Props.fbx", "Documents", hL.transform, docsInL);
        var flashR = MeshGO("FlashR", ModelDir + "/IC_Props.fbx", "Flashlight", hR.transform, flashInR);
        var beam = new GameObject("Beam").transform;
        beam.SetParent(flashR.transform, false);
        beam.localPosition = new Vector3(0, 0, 0.13f);
        // Flashlight.cs ile aynı değerler
        AddLight(beam.gameObject, LightType.Spot, new Color(1f, 0.95f, 0.85f), 120f, 70f, 70f, LightShadows.Soft);
        var spill = new GameObject("Spill");
        spill.transform.SetParent(beam, false);
        AddLight(spill, LightType.Spot, new Color(1f, 0.95f, 0.85f), 15f, 20f, 130f, LightShadows.None);
        foreach (var g in new[] { hL, hR, docsR, docsL, flashR })
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
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.playOnAwake = true;

        // Editörde ilk kareyi göster
        ApplyPose(car, CarWorld(0f), true);
        ApplyPose(head, HeadAt(0f), false);
        ApplyPose(hL.transform, SampleKeys(handL, 0f), false);
        ApplyPose(hR.transform, SampleKeys(handR, 0f), false);
        docsR.SetActive(false); docsL.SetActive(false); flashR.SetActive(false);
        beam.gameObject.SetActive(false); dome.gameObject.SetActive(false);

        var fade = SetupFade();

        // Oyuna bağlantı
        var ic = root.AddComponent<IntroCinematic>();
        ic.director = director;
        ic.cinematicCamera = cam;
        ic.head = head;
        ic.hideOnHandoff = new[] { hL, hR };
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

        float r = 0.40f * K;
        Func<float, Quaternion> spin = t => Quaternion.AngleAxis(-Distance(t) / r * Mathf.Rad2Deg, Vector3.right);
        AddRotCurves(clip, "Model/Wheel_FL", n, t => Quaternion.AngleAxis(Steer(t), Vector3.up) * spin(t));
        AddRotCurves(clip, "Model/Wheel_FR", n, t => Quaternion.AngleAxis(Steer(t), Vector3.up) * spin(t));
        AddRotCurves(clip, "Model/Wheel_RL", n, spin);
        AddRotCurves(clip, "Model/Wheel_RR", n, spin);
        AddRotCurves(clip, "Model/SteeringWheel", n, t => Quaternion.AngleAxis(Steer(t) * 12f, SteerNormalModel));
        AddRotCurves(clip, "Model/Door_L", n, t => DoorRot(t).r);
        AddRotCurves(clip, "Model/IgnitionKey", n, t => Quaternion.AngleAxis(Smooth((t - T_KeyTurnStart) / (T_KeyTurnEnd - T_KeyTurnStart)) * 35f, Vector3.forward));

        Active(clip, "Model/HeadlightGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "Model/TaillightGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "Model/GaugeGlow", (0f, true), (T_LightsOff, false));
        Active(clip, "DomeLight", (0f, false), (T_DomeOn, true), (T_DoorClosed, false));
        Active(clip, "SeatDocs", (0f, true), (T_GrabDocs, false));
        Active(clip, "HandR/DocsR", (0f, false), (T_GrabDocs, true), (T_DocsToLeft, false));
        Active(clip, "HandL/DocsL", (0f, false), (T_DocsToLeft, true));
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

    static float Steer(float t)
    {
        if (t > StopTime) return 0f;
        float d = (CarDelta(t + 0.25f) - CarDelta(t - 0.25f)) / 0.5f; // derece/sn
        return Mathf.Clamp(d * 1.6f, -30f, 30f);
    }

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
        AudioClipOn(feet, steps, T_WalkStart, 6.0, T_WalkEnd - T_WalkStart + 0.2, 0.8, 0.2, 0.5);

        // Fener tıkı ve uzaktan köpek havlaması (etrafa bakarken)
        AudioClipOn(fx, Sfx("Flashlight_Click"), T_FlashOn - 0.03, 0, 0.8, 0.8);
        AudioClipOn(fx, Sfx("Dog_DistantBark"), 39.6, 0, 6.3, 0.35, 0, 0.5);

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
            ["IC_Sleeve"] = Mat("IC_Sleeve", "IC_Sleeve", Color.white, 2f, 0f, 0.1f),
            ["IC_Glass"] = Mat("IC_Glass", null, new Color(0.55f, 0.6f, 0.62f, 0.14f), 1f, 0f, 0.92f, alpha: true),
            ["IC_LampLens"] = Mat("IC_LampLens", null, new Color(0.75f, 0.75f, 0.72f), 1f, 0.2f, 0.85f),
            ["IC_TailLens"] = Mat("IC_TailLens", null, new Color(0.35f, 0.04f, 0.03f), 1f, 0f, 0.8f),
            ["IC_HeadlightGlow"] = Mat("IC_HeadlightGlow", null, new Color(1f, 0.95f, 0.85f), 1f, 0f, 0.8f, emission: new Color(1f, 0.92f, 0.78f) * 4f),
            ["IC_TaillightGlow"] = Mat("IC_TaillightGlow", null, new Color(0.6f, 0.05f, 0.03f), 1f, 0f, 0.8f, emission: new Color(1f, 0.08f, 0.04f) * 2f),
            ["IC_GaugeFace"] = Mat("IC_GaugeFace", null, new Color(0.05f, 0.05f, 0.05f), 1f, 0f, 0.6f),
            ["IC_GaugeGlow"] = Mat("IC_GaugeGlow", null, new Color(0.2f, 0.12f, 0.05f), 1f, 0f, 0.6f, emission: new Color(1f, 0.55f, 0.2f) * 1.2f),
            // Flashlight.cs'teki fener renkleri
            ["IC_FlashBody"] = Mat("IC_FlashBody", null, new Color(0.08f, 0.08f, 0.09f), 1f, 0.7f, 0.6f),
            ["IC_FlashLens"] = Mat("IC_FlashLens", null, new Color(0.9f, 0.9f, 0.8f), 1f, 0f, 0.5f),
        };

        foreach (var fbx in new[] { "IC_Pickup", "IC_Props", "IC_Hands" })
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
