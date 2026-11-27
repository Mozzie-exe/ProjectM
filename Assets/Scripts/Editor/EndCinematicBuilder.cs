using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

// Oyun sonu sinematiğini kurar: Tools > End Cinematic > Build.
// Kök "EndCinematic" (dünya orijininde) > "Rig" (ölçek K): kafa + sinematik kamera, kollar, eller, fener.
// Pozlar rig yerelinde (insan metresi = dünya / K); el/kol modelleri ve IK giriş sinematiğiyle aynı.
// Dolabın devrilmesi klipte değil, oyunda EndCinematic.ApplyCabinet ile (level objesine Animator eklenmesin).
public static class EndCinematicBuilder
{
    const string ModelDir = "Assets/Models/IntroCinematic";
    const string CineDir = "Assets/Cinematics";
    const string EndSfxDir = "Assets/Sfx/Ending";
    const float K = 1.78f;
    const float FPS = 30f;

    // Sinematik zamanı (sn), spec tablosu
    public const float T_BlendEnd = 1.2f, T_InspectEnd = 6.5f, T_WalkEnd = 10.0f, T_Reach = 13.0f, T_Creak = 18.0f,
        T_TurnStart = 18.7f, T_TurnEnd = 19.1f, T_FallStart = 19.1f, T_Black = 19.9f, T_End = 21.5f;
    // Sessizlik T_Reach..T_Creak (5 sn): el örtünün üstünde titreyerek durur

    // Göz yüksekliği zeminden (dünya): oyuncu kamerası ayakta 2.85; sinematikte tek dizin üstüne çöker
    const float EyeStand = 2.85f, EyeCrouch = 1.85f;

    const string DoorName = "Kapi_TasOda", CorpseName = "Bodrum_OrtuluSekil", SheetName = "Bodrum_Ortu",
        CabinetPath = "Bodrum_Yeni/Bodrum_DevrilenDolap", FloorName = "BodrumOda_TopraKZemin", DripsName = "Bodrum_Blood_Drips";

    // ------------------------------------------------------------------ poz yardımcıları (IntroCinematicBuilder ile aynı)
    struct P
    {
        public Vector3 p; public Quaternion r;
        public P(Vector3 p, Quaternion r) { this.p = p; this.r = r; }
        public static P operator *(P a, P b) => new P(a.p + a.r * b.p, a.r * b.r);
        public P Inv() { var ir = Quaternion.Inverse(r); return new P(-(ir * p), ir); }
        public static P Lerp(P a, P b, float s) => new P(Vector3.Lerp(a.p, b.p, s), Quaternion.Slerp(a.r, b.r, s));
    }
    static float Smooth(float s) { s = Mathf.Clamp01(s); return s * s * (3 - 2 * s); }
    static float Perlin(float t, float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

    // ------------------------------------------------------------------ sahne noktaları (dünya)
    static float floorY;
    static Vector3 posA, posA2, posS, posC;
    static Vector3[] walkPath;
    static float[] walkTable;
    const int WalkSamples = 200;
    static Vector3 lookBoots, lookMid, lookHeadEnd, lookBody, lookDrips, reachPoint;
    static Vector3 cabCenter, cabPivot, cabAxis, cabAim, fallAway, cabTopFront;
    static readonly float[] StepTimes = { 3.75f, 6.95f, 7.6f, 8.25f, 8.9f, 9.5f };

    static Bounds RendererBounds(GameObject go)
    {
        bool any = false; var b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // Örtünün (x, z) noktasındaki üst yüzeyi: yakındaki köşe noktalarının en yükseği
    static Vector3[] sheetVerts;
    static float SheetTop(float x, float z, float fallback)
    {
        float best = float.MinValue;
        foreach (var v in sheetVerts)
        {
            float dx = v.x - x, dz = v.z - z;
            if (dx * dx + dz * dz < 0.15f * 0.15f && v.y > best) best = v.y;
        }
        return best == float.MinValue ? fallback : best;
    }

    static void Prepare(GameObject corpse, GameObject sheet, GameObject cabinet, GameObject floor, GameObject drips)
    {
        floorY = RendererBounds(floor).max.y;
        var cb = RendererBounds(corpse);
        var sb = RendererBounds(sheet);
        var mf = sheet.GetComponent<MeshFilter>();
        var local = mf ? mf.sharedMesh.vertices : new Vector3[0];
        sheetVerts = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++) sheetVerts[i] = sheet.transform.TransformPoint(local[i]);

        float bootsX = cb.min.x, headEndX = sb.max.x, southZ = sb.min.z, zMid = sb.center.z;
        float eye = floorY + EyeStand;
        Func<float, float, Vector3> onSheet = (x, z) => new Vector3(x, SheetTop(x, z, sb.max.y), z);

        // Başlangıç: çizmelerin ~1.1 m (insan) batısı; adım atıp örtünün güney yanından baş ucuna yürür
        posA = new Vector3(bootsX - 1.95f, eye, zMid - 0.10f);
        posA2 = new Vector3(bootsX - 1.25f, eye, zMid - 0.45f);
        posS = new Vector3(headEndX - 1.30f, eye, southZ - 0.40f);
        // Örtünün kenarına çömelir, üstüne eğilir. Baş, uzanan kolun biraz sağında: el kolun arkasında kalmaz.
        posC = new Vector3(headEndX - 1.00f, floorY + EyeCrouch, southZ - 0.02f);
        walkPath = new[]
        {
            posA2,
            new Vector3(bootsX - 0.85f, eye, southZ - 0.35f),
            new Vector3(bootsX + 0.45f, eye, southZ - 0.80f),
            new Vector3(bootsX + 2.05f, eye, southZ - 0.75f),
            posS,
        };
        walkTable = new float[WalkSamples + 1];
        for (int i = 1; i <= WalkSamples; i++)
            walkTable[i] = walkTable[i - 1] + Vector3.Distance(Catmull(walkPath, (i - 1f) / WalkSamples), Catmull(walkPath, (float)i / WalkSamples));

        lookBoots = onSheet(bootsX + 0.5f, zMid);
        lookMid = onSheet((bootsX + headEndX) * 0.5f, zMid);
        lookHeadEnd = onSheet(headEndX - 0.4f, zMid);
        lookBody = onSheet(headEndX - 1.6f, zMid - 0.1f);
        lookDrips = RendererBounds(drips).center;
        reachPoint = onSheet(headEndX - 0.75f, zMid - 0.15f);   // örtünün altındaki başın üstü

        // Dolap oyuncuya doğru, alt kenarının üstünden devrilir
        var kb = RendererBounds(cabinet);
        cabCenter = kb.center;
        Vector3 d = posC - cabCenter; d.y = 0f; d.Normalize();
        fallAway = d;
        float support = Mathf.Abs(d.x) * kb.extents.x + Mathf.Abs(d.z) * kb.extents.z;
        cabPivot = new Vector3(cabCenter.x + d.x * support, kb.min.y, cabCenter.z + d.z * support);
        cabAxis = Vector3.Cross(Vector3.up, d);
        cabAim = new Vector3(cabCenter.x, kb.min.y + kb.size.y * 0.5f, cabCenter.z);
        cabTopFront = new Vector3(cabPivot.x, kb.max.y, cabPivot.z);   // oyuncuya bakan üst kenar
    }

    static Vector3 Catmull(Vector3[] p, float u)
    {
        int n = p.Length - 1;
        float f = Mathf.Clamp01(u) * n;
        int i = Mathf.Min((int)f, n - 1);
        float s = f - i;
        Vector3 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, n)];
        return 0.5f * (2f * p1 + (-p0 + p2) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (s * s) + (-p0 + 3f * p1 - 3f * p2 + p3) * (s * s * s));
    }

    // a: yolun uzunluğuna göre oran (0..1)
    static Vector3 WalkAt(float a)
    {
        float target = Mathf.Clamp01(a) * walkTable[WalkSamples];
        int i = 1;
        while (i < WalkSamples && walkTable[i] < target) i++;
        float seg = walkTable[i] - walkTable[i - 1];
        float s = seg > 1e-6f ? (target - walkTable[i - 1]) / seg : 0f;
        return Catmull(walkPath, (i - 1 + s) / WalkSamples);
    }

    static Quaternion CabRot(float t) => Quaternion.AngleAxis(EndCinematicMath.CabinetAngle(t, T_Creak, T_FallStart), cabAxis);
    static Vector3 CabPoint(float t) => cabPivot + CabRot(t) * (cabAim - cabPivot);

    // ------------------------------------------------------------------ kafa
    static float FallU(float t) { float u = Mathf.Clamp01((t - T_FallStart) / (T_Black - T_FallStart)); return u * u; }

    // Her adımda kafa biraz iner (topuk basışı)
    static float Bob(float t)
    {
        float b = 0f;
        foreach (var s in StepTimes) { float x = (t - s) / 0.32f; b -= 0.035f * Mathf.Exp(-x * x * 2f); }
        return b;
    }

    static Vector3 HeadPosW(float t)
    {
        Vector3 p;
        if (t < 3.0f) p = posA;
        else if (t < 4.6f) p = Vector3.Lerp(posA, posA2, Smooth((t - 3.0f) / 1.6f));
        else if (t < 6.5f) p = posA2;
        else if (t < 9.7f) { float s = (t - 6.5f) / 3.2f; p = WalkAt(0.5f * s + 0.5f * Smooth(s)); }
        else if (t < T_WalkEnd) p = posS;
        else if (t < 11.2f) p = Vector3.Lerp(posS, posC, Smooth((t - T_WalkEnd) / 1.2f));
        else p = posC;
        // Dolap gelirken geriye, cesedin üstüne düşer
        float u = FallU(t);
        p += fallAway * (0.7f * u) + Vector3.down * (0.65f * u);
        p.y += Bob(t);
        return p;
    }

    static Vector2 YawPitch(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        return new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
    }

    static List<(float t, Func<float, Vector3> at)> gaze;
    static void BuildGaze()
    {
        Func<Vector3, Func<float, Vector3>> fix = v => _ => v;
        gaze = new List<(float, Func<float, Vector3>)>
        {
            (0f, fix(lookBoots)), (1.9f, fix(lookBoots)),        // çizmeler
            (3.7f, fix(lookMid)), (5.4f, fix(lookHeadEnd)),       // boydan baş ucuna
            (6.6f, fix(lookHeadEnd)),
            (7.9f, fix(lookBody)),                                // yürürken cesetten gözünü ayırmaz
            (8.6f, fix(lookBody)),
            (9.05f, fix(lookDrips)), (9.45f, fix(lookDrips)),     // duvardaki kan
            (10.3f, fix(reachPoint)),                             // eğilir, uzanacağı yere bakar
            (T_TurnStart, fix(reachPoint)),
            (T_TurnEnd, CabPoint),                                // ani dönüş: dolap
        };
    }

    static Vector2 Aim(float t, Vector3 pos)
    {
        if (t >= T_FallStart) return YawPitch(pos, CabPoint(t));
        int k = 0;
        while (k < gaze.Count - 2 && t >= gaze[k + 1].t) k++;
        var a = YawPitch(pos, gaze[k].at(t));
        var b = YawPitch(pos, gaze[k + 1].at(t));
        float s = Smooth((t - gaze[k].t) / (gaze[k + 1].t - gaze[k].t));
        return new Vector2(a.x + Mathf.DeltaAngle(a.x, b.x) * s, Mathf.Lerp(a.y, b.y, s));
    }

    static Quaternion HeadRotW(float t, Vector3 pos)
    {
        Vector2 aim = Aim(t, pos);
        // Nefes tutma (sessizlik): sallantı azalır
        float freeze = Smooth((t - T_Reach) / 0.3f) * (1f - Smooth((t - T_Creak) / 0.2f));
        float n = 1f - 0.75f * freeze;
        float yaw = aim.x + Perlin(t * 0.35f, 1.3f) * 0.9f * n;
        float pitch = aim.y + Perlin(t * 0.3f, 7.1f) * 0.6f * n + Mathf.Sin(t * Mathf.PI * 2f / 3.6f) * 0.25f * n;
        float roll = Perlin(t * 0.25f, 13.7f) * 0.5f * n;
        // Gıcırtıda irkilme: baş hafif kalkar, dönüşe kadar gergin kalır
        pitch -= 3f * Smooth((t - (T_Creak + 0.05f)) / 0.18f) * (1f - 0.5f * Smooth((t - (T_Creak + 0.4f)) / 0.3f)) * (1f - Smooth((t - T_TurnStart) / 0.2f));
        // Düşerken yana yatar, sarsılır
        float u = FallU(t);
        roll += 7f * u + Perlin(t * 9f, 3.3f) * 1.5f * u;
        pitch += Perlin(t * 8f, 5.9f) * 1.2f * u;
        return Quaternion.Euler(pitch, yaw, roll);
    }

    static P HeadRig(float t)
    {
        Vector3 w = HeadPosW(t);
        return new P(w / K, HeadRotW(t, w));
    }

    // ------------------------------------------------------------------ kollar ve eller (rig yereli, IntroCinematicBuilder ile aynı ölçüler)
    const float UpperLen = 0.34f, ForeLen = 0.31f;
    static readonly Vector3 WristR = new Vector3(0.020f, 0f, -0.118f);
    static readonly string[] HandShapes = { "Grip", "Relax", "Pinch", "Open", "Hold" };
    static Vector3 WristLocal(bool left) => left ? new Vector3(-WristR.x, WristR.y, WristR.z) : WristR;
    static readonly P GameplayFlashInView = new P(new Vector3(0.22f, -0.30f, 0.55f) / K, Quaternion.Euler(-2, -4, 0));
    static readonly Vector3 LeftHangView = new Vector3(-0.24f, -0.62f, 0.22f);
    static readonly Vector3 UpV = new Vector3(0f, 1f, 0.2f);
    static readonly Vector3 DefendView = new Vector3(-0.14f, -0.10f, 0.48f);   // dolap gelirken sol el öne kalkar (fener ışığının içinde)
    // Fener avuçta: gövde ortasından (ucundan 3 cm geride) gevşek yumruk
    static readonly P FlashInR = HoldPose(new Vector3(0f, 0f, -0.03f), Vector3.forward, Vector3.up, false).Inv();

    static float YawOf(Quaternion q) { var f = q * Vector3.forward; return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }
    static Quaternion Torso(float t) => Quaternion.Euler(0, YawOf(HeadRig(t).r), 0);
    static Vector3 ShoulderAt(float t, bool left) => HeadRig(t).p + Torso(t) * new Vector3(left ? -0.20f : 0.20f, -0.32f, 0f);
    static Vector3 Pole(float t, bool left) => Torso(t) * new Vector3(left ? -0.5f : 0.5f, -0.7f, -0.6f);

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

    static P HoldPose(Vector3 c, Vector3 axis, Vector3 palmDir, bool left)
    {
        Vector3 y = axis.normalized;
        Vector3 x = Vector3.ProjectOnPlane(left ? -palmDir : palmDir, y).normalized;
        var r = Quaternion.LookRotation(Vector3.Cross(x, y), y);
        return new P(c - r * new Vector3(left ? 0.018f : -0.018f, 0f, -0.032f), r);
    }
    static P PalmFacing(Vector3 pos, Vector3 faceDir, Vector3 from, bool left)
    {
        Vector3 x = left ? faceDir.normalized : -faceDir.normalized;
        Vector3 z = Vector3.ProjectOnPlane(pos - from, x).normalized;
        return new P(pos, Quaternion.LookRotation(z, Vector3.Cross(z, x)));
    }
    static P GripArm(Vector3 pos, Vector3 axisHint, Vector3 from)
    {
        Vector3 f = pos - from;
        return new P(pos, Quaternion.LookRotation(f, Vector3.ProjectOnPlane(axisHint, f)));
    }
    static Vector3 HV(float t, Vector3 v) { var h = HeadRig(t); return h.p + h.r * v; }
    static Vector3 HD(float t, Vector3 d) => HeadRig(t).r * d;

    // Sağ el: fener oyundaki yerinde (kafaya bağlı); fenerin kendi ekseni etrafında bileği en düz tutan açı
    // Uzanırken fener aşağı-sağa iner: ışığın parlak merkezi elin altına düşer, beyaz örtüde el kaybolmaz
    static readonly P FlashReachView = new P(GameplayFlashInView.p + new Vector3(0.03f, -0.05f, -0.03f), Quaternion.Euler(24, 4, 0));
    static P FlashView(float t)
    {
        float w = Smooth((t - 10.4f) / 1.0f) * (1f - Smooth((t - T_TurnStart) / 0.3f));
        return P.Lerp(GameplayFlashInView, FlashReachView, w);
    }

    static P HandR(float t)
    {
        P flashRig = HeadRig(t) * FlashView(t);
        Vector3 s = ShoulderAt(t, false), pole = Pole(t, false);
        P best = default;
        float bestBend = float.MaxValue;
        for (int roll = 0; roll < 360; roll += 10)
        {
            P flash = flashRig * new P(Vector3.zero, Quaternion.AngleAxis(roll, Vector3.forward));
            P hand = flash * FlashInR.Inv();
            Vector3 w = hand.p + hand.r * WristLocal(false);
            SolveArm(s, w, pole, out _, out var e);
            float bend = Vector3.Angle(w - e, hand.r * Vector3.forward);
            if (bend < bestBend) { bestBend = bend; best = hand; }
        }
        return best;
    }

    // Sol el: yanda sarkar; 10.4'ten itibaren örtünün baş ucuna uzanır, 13.0'da örtünün ~5 cm üstünde durur
    const float PalmThick = 0.03f, StopGap = 0.027f;   // ölçüldü: elin en alt noktası örtüden ~5 cm yukarıda
    static P HandL(float t)
    {
        Vector3 reach = reachPoint / K;
        Func<P> hang = () => Solve(t, true, f => GripArm(HV(t, LeftHangView), HD(t, UpV), f));
        Func<float, P> over = h => Solve(t, true, f => PalmFacing(reach + Vector3.up * (PalmThick + h), Vector3.down, f, true));
        Func<P> defend = () => Solve(t, true, f => PalmFacing(HV(t, DefendView), HD(t, new Vector3(0.1f, 0.35f, 1f)), f, true));

        if (t < 10.4f) return hang();
        if (t < 12.1f) return P.Lerp(hang(), over(0.12f), Smooth((t - 10.4f) / 1.7f));
        if (t < T_Reach) return P.Lerp(over(0.12f), over(StopGap), Smooth((t - 12.1f) / (T_Reach - 12.1f)));
        if (t < T_TurnStart + 0.05f)
        {
            // Durur; çok hafif titrer
            var p = over(StopGap);
            p.p += new Vector3(Perlin(t * 6f, 2.1f), Perlin(t * 6f, 4.4f) * 0.5f, Perlin(t * 6f, 8.8f)) * 0.0015f;
            return p;
        }
        if (t < T_TurnStart + 0.35f) return P.Lerp(over(StopGap), hang(), Smooth((t - (T_TurnStart + 0.05f)) / 0.3f));
        if (t < T_TurnStart + 0.55f) return hang();
        return P.Lerp(hang(), defend(), Smooth((t - (T_TurnStart + 0.55f)) / 0.4f));
    }

    static P HandAt(float t, bool left) => left ? HandL(t) : HandR(t);
    static P UpperAt(float t, bool left)
    {
        P h = HandAt(t, left);
        SolveArm(ShoulderAt(t, left), h.p + h.r * WristLocal(left), Pole(t, left), out var s, out var e);
        return new P(s, Quaternion.LookRotation(e - s, Torso(t) * new Vector3(0f, 0.5f, -1f)));
    }
    static P ForeAt(float t, bool left)
    {
        P h = HandAt(t, left);
        Vector3 w = h.p + h.r * WristLocal(left);
        SolveArm(ShoulderAt(t, left), w, Pole(t, left), out _, out var e);
        return new P(e, Quaternion.LookRotation(w - e, h.r * Vector3.up));
    }

    // ------------------------------------------------------------------ kurulum
    static string ArmsFbx => ModelDir + "/IC_Arms.fbx";
    static string PropsFbx => ModelDir + "/IC_Props.fbx";

    static T Need<T>(T obj, string what) where T : UnityEngine.Object
    {
        if (!obj) Debug.LogError("End cinematic: bulunamadı: " + what);
        return obj;
    }

    [MenuItem("Tools/End Cinematic/Build")]
    public static void Build()
    {
        var doorGO = Need(GameObject.Find(DoorName), DoorName);
        var corpse = Need(GameObject.Find(CorpseName), CorpseName);
        var sheet = Need(GameObject.Find(SheetName), SheetName);
        var cabinet = Need(GameObject.Find(CabinetPath), CabinetPath);
        var floor = Need(GameObject.Find(FloorName), FloorName);
        var drips = Need(GameObject.Find(DripsName), DripsName);
        var player = Need(GameObject.Find("Player"), "Player");
        if (!doorGO || !corpse || !sheet || !cabinet || !floor || !drips || !player) return;
        var door = Need(doorGO.GetComponent<Door>(), DoorName + " (Door bileşeni)");
        var camT = player.transform.Find("CamHolder/Camera");
        var playerCam = camT ? camT.GetComponent<Camera>() : null;
        Need(playerCam, "Player/CamHolder/Camera");
        var music = Need(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/ceset_room_extended.wav"), "Assets/Sfx/ceset_room_extended.wav");
        var creak = Need(AssetDatabase.LoadAssetAtPath<AudioClip>(EndSfxDir + "/Cabinet_Creak.mp3"), "Cabinet_Creak.mp3");
        var fall = Need(AssetDatabase.LoadAssetAtPath<AudioClip>(EndSfxDir + "/Cabinet_Fall.mp3"), "Cabinet_Fall.mp3");
        var steps = Need(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/Intro/Footsteps_Gravel.mp3"), "Footsteps_Gravel.mp3");
        var arms = Need(AssetDatabase.LoadAssetAtPath<GameObject>(ArmsFbx), ArmsFbx);
        var props = Need(AssetDatabase.LoadAssetAtPath<GameObject>(PropsFbx), PropsFbx);
        if (!door || !playerCam || !music || !creak || !fall || !steps || !arms || !props) return;

        var old = GameObject.Find("EndCinematic");
        if (old) Undo.DestroyObjectImmediate(old);

        Prepare(corpse, sheet, cabinet, floor, drips);
        BuildGaze();

        // Etkileşim ışını örtüye çarpsın
        if (!sheet.GetComponent<Collider>()) Undo.AddComponent<BoxCollider>(sheet);

        var root = new GameObject("EndCinematic");
        var animator = root.AddComponent<Animator>();
        animator.applyRootMotion = false;
        var rig = new GameObject("Rig").transform;
        rig.SetParent(root.transform, false);
        rig.localScale = Vector3.one * K;

        // Kafa ve kamera
        var head = new GameObject("Head").transform;
        head.SetParent(rig, false);
        var camGO = new GameObject("CinematicCamera");
        camGO.transform.SetParent(head, false);
        var cam = camGO.AddComponent<Camera>();
        cam.CopyFrom(playerCam);
        camGO.transform.localPosition = Vector3.zero;   // CopyFrom konumu da kopyalar
        camGO.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = playerCam.GetUniversalAdditionalCameraData().renderPostProcessing;
        cam.enabled = false;   // oyunda EndCinematic açar

        // Eller, kollar, fener
        var hL = HandGO("HandL", "L", rig);
        var hR = HandGO("HandR", "R", rig);
        var armGOs = new[]
        {
            MeshGO("UpperL", ArmsFbx, "UpperArm", rig, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("ForeL", ArmsFbx, "Forearm", rig, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("UpperR", ArmsFbx, "UpperArm", rig, new P(Vector3.zero, Quaternion.identity)),
            MeshGO("ForeR", ArmsFbx, "Forearm", rig, new P(Vector3.zero, Quaternion.identity)),
        };
        var flashR = MeshGO("FlashR", PropsFbx, "Flashlight", hR.transform, FlashInR);
        var beam = new GameObject("Beam").transform;
        beam.SetParent(flashR.transform, false);
        beam.localPosition = new Vector3(0, 0, 0.13f);
        // Flashlight.cs ile aynı değerler
        AddLight(beam.gameObject, LightType.Spot, new Color(1f, 0.95f, 0.85f), 120f, 70f, 70f, LightShadows.Soft);
        var spill = new GameObject("Spill");
        spill.transform.SetParent(beam, false);
        AddLight(spill, LightType.Spot, new Color(1f, 0.95f, 0.85f), 15f, 20f, 130f, LightShadows.None);
        foreach (var g in new[] { hL, hR, flashR, armGOs[0], armGOs[1], armGOs[2], armGOs[3] })
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                mr.shadowCastingMode = ShadowCastingMode.Off;   // fener kendi ışığını kesmesin

        // Animasyon + Timeline + ses
        var clip = BuildClip();
        SetupAudioImport();
        var timeline = BuildTimeline(clip, out var track, creak, fall, steps);
        var director = root.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.SetGenericBinding(track, animator);
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
        director.playOnAwake = false;

        // Editörde ilk kare
        ApplyPose(head, HeadRig(0f));
        ApplyPose(hL.transform, HandL(0f));
        ApplyPose(hR.transform, HandR(0f));
        ApplyPose(armGOs[0].transform, UpperAt(0f, true));
        ApplyPose(armGOs[1].transform, ForeAt(0f, true));
        ApplyPose(armGOs[2].transform, UpperAt(0f, false));
        ApplyPose(armGOs[3].transform, ForeAt(0f, false));
        foreach (var s in HandShapes) hL.transform.Find(s).gameObject.SetActive(s == "Relax");
        foreach (var s in HandShapes) hR.transform.Find(s).gameObject.SetActive(s == "Hold");

        // Oyuna bağlantı
        var ec = root.AddComponent<EndCinematic>();
        ec.door = door;
        ec.corpse = corpse.transform;
        ec.player = player.transform;
        ec.playerCamera = playerCam;
        ec.music = music;
        ec.director = director;
        ec.head = head;
        ec.cinematicCamera = cam;
        ec.cabinet = cabinet.transform;
        ec.rig = rig.gameObject;
        ec.hushAt = T_Reach;
        ec.blackAt = T_Black;
        ec.endAt = T_End;
        ec.fallAt = T_FallStart;
        ec.creakAt = T_Creak;
        ec.cabinetPivot = cabPivot;
        ec.cabinetAxis = cabAxis;
        ec.black = SetupBlack();
        ec.backLight = SetupBackLight(root.transform);
        ec.backLightIntensity = BackLightCd;
        camGO.AddComponent<AudioListener>().enabled = false;   // BeginMain açar
        SetupCredits(ec);

        // E ile etkileşim örtüde
        var inspect = sheet.GetComponent<CorpseInspect>();
        if (!inspect) inspect = Undo.AddComponent<CorpseInspect>(sheet);
        Undo.RecordObject(inspect, "Build End Cinematic");
        inspect.ending = ec;
        EditorUtility.SetDirty(inspect);

        rig.gameObject.SetActive(false);   // eller ve fener sinematik başlayana kadar görünmez
        Undo.RegisterCreatedObjectUndo(root, "Build End Cinematic");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("End cinematic kuruldu. Süre: " + T_End + " sn. Timeline: " + CineDir + "/EndCinematic.playable");
    }

    static void ApplyPose(Transform t, P p) { t.localPosition = p.p; t.localRotation = p.r; }

    // Sinematik bitince (EndCinematic.Ended) credits başlar; metin Resources/Credits/Credits.txt
    public static void SetupCredits(EndCinematic ec)
    {
        var cs = ec.GetComponent<CreditsScreen>();
        if (!cs) cs = Undo.AddComponent<CreditsScreen>(ec.gameObject);
        Undo.RecordObject(cs, "Setup Credits");
        cs.ending = ec;
        cs.music = Need(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sfx/credits_extended.wav"), "Assets/Sfx/credits_extended.wav");
        cs.content = Need(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Credits/Credits.txt"), "Assets/Resources/Credits/Credits.txt");
        EditorUtility.SetDirty(cs);
    }

    // Oyunun sonundaki ani siyah: Canvas'ın en üstünde, başta kapalı
    static UnityEngine.UI.Image SetupBlack()
    {
        var canvas = GameObject.Find("Canvas");
        if (!canvas) { Debug.LogError("End cinematic: Canvas yok; siyah ekran kurulamadı"); return null; }
        var existing = canvas.transform.Find("EndBlack");
        if (existing) Undo.DestroyObjectImmediate(existing.gameObject);
        var go = new GameObject("EndBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = false;
        img.enabled = false;
        Undo.RegisterCreatedObjectUndo(go, "Build End Cinematic");
        return img;
    }

    static GameObject HandGO(string name, string side, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        foreach (var s in HandShapes)
            MeshGO(s, ArmsFbx, "Hand_" + side + "_" + s, go.transform, new P(Vector3.zero, Quaternion.identity));
        return go;
    }

    static GameObject MeshGO(string name, string fbx, string child, Transform parent, P local)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx).transform.Find(child);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ApplyPose(go.transform, local);
        var m = new GameObject("Mesh");
        m.transform.SetParent(go.transform, false);
        m.transform.localRotation = Quaternion.Euler(0, 180, 0);   // Blender -> Unity yönü
        m.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        m.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
        return go;
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

    // ------------------------------------------------------------------ animasyon klibi
    static AnimationClip BuildClip()
    {
        var clip = new AnimationClip { frameRate = FPS };
        int n = Mathf.CeilToInt(T_End * FPS) + 1;
        AddPoseCurves(clip, "Rig/Head", n, HeadRig);
        AddPoseCurves(clip, "Rig/HandL", n, HandL);
        AddPoseCurves(clip, "Rig/HandR", n, HandR);
        AddPoseCurves(clip, "Rig/UpperL", n, t => UpperAt(t, true));
        AddPoseCurves(clip, "Rig/ForeL", n, t => ForeAt(t, true));
        AddPoseCurves(clip, "Rig/UpperR", n, t => UpperAt(t, false));
        AddPoseCurves(clip, "Rig/ForeR", n, t => ForeAt(t, false));
        HandShapeCurves(clip, "Rig/HandL", (0f, "Relax"), (11.4f, "Open"), (T_TurnStart + 0.1f, "Relax"), (T_TurnStart + 0.6f, "Open"));
        HandShapeCurves(clip, "Rig/HandR", (0f, "Hold"));
        clip.EnsureQuaternionContinuity();
        if (!AssetDatabase.IsValidFolder(CineDir)) AssetDatabase.CreateFolder("Assets", "Cinematics");
        string path = CineDir + "/EndCinematic_Anim.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
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
        for (int i = 1; i < n; i++)
            if (Quaternion.Dot(rot[i - 1], rot[i]) < 0) rot[i] = new Quaternion(-rot[i].x, -rot[i].y, -rot[i].z, -rot[i].w);
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

    static void HandShapeCurves(AnimationClip clip, string hand, params (float t, string shape)[] steps)
    {
        foreach (var s in HandShapes)
        {
            var curve = new AnimationCurve();
            foreach (var st in steps)
                curve.AddKey(new Keyframe(st.t, st.shape == s ? 1 : 0, float.PositiveInfinity, float.PositiveInfinity));
            curve.AddKey(new Keyframe(T_End, steps[steps.Length - 1].shape == s ? 1 : 0, float.PositiveInfinity, float.PositiveInfinity));
            clip.SetCurve(hand + "/" + s, typeof(GameObject), "m_IsActive", curve);
        }
    }

    // ------------------------------------------------------------------ Timeline ve ses
    // Adım sesi: Footsteps_Gravel.mp3 içindeki tek adımların başlangıçları (sn)
    static readonly float[] StepOnsets = { 3.62f, 4.17f, 5.19f, 6.92f, 8.91f };
    // Ölçüldü (95. yüzdelik, 50 ms RMS): adımlar ~-25 dB, müzik -6, çarpma -1 (en yüksek)
    // Dosya yüksekliği (LUFS): müzik -14, gıcırtı -22, çınlama -28
    const double StepVol = 0.9, CreakVol = 1.0, FallVol = 1.0, RingVol = 1.0;
    const double FallImpactIn = 0.52;   // Cabinet_Fall.mp3: ana çarpma 0.52 sn'de
    const double RingDelay = 0.05;
    static readonly string[] EndSfx = { "Cabinet_Creak", "Cabinet_Fall", "Ear_Ring" };

    static AudioClip EndClip(string n) => Need(AssetDatabase.LoadAssetAtPath<AudioClip>(EndSfxDir + "/" + n + ".mp3"), n + ".mp3");

    static TimelineAsset BuildTimeline(AnimationClip clip, out AnimationTrack track, AudioClip creak, AudioClip fall, AudioClip steps)
    {
        string path = CineDir + "/EndCinematic.playable";
        AssetDatabase.DeleteAsset(path);
        var tl = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(tl, path);
        tl.durationMode = TimelineAsset.DurationMode.FixedLength;
        tl.fixedDuration = T_End;
        track = tl.CreateTrack<AnimationTrack>(null, "Kamera ve Eller");
        track.trackOffset = TrackOffset.ApplyTransformOffsets;
        track.position = Vector3.zero;
        track.rotation = Quaternion.identity;
        var tc = track.CreateClip(clip);
        tc.displayName = "Son";
        tc.start = 0;
        tc.duration = T_End;
        ((AnimationPlayableAsset)tc.asset).removeStartOffset = false;

        var stepTrack = tl.CreateTrack<AudioTrack>(null, "Adimlar");
        for (int i = 0; i < StepTimes.Length; i++)
            AudioClipOn(stepTrack, steps, StepTimes[i] - 0.03, StepOnsets[i % StepOnsets.Length] - 0.03, 0.4, StepVol, 0, 0.08);
        var creakTrack = tl.CreateTrack<AudioTrack>(null, "Dolap_Gicirti");
        AudioClipOn(creakTrack, creak, T_Creak, 0, creak.length, CreakVol);
        var fallTrack = tl.CreateTrack<AudioTrack>(null, "Dolap_Devrilme");
        double fallStart = T_Black - FallImpactIn;
        AudioClipOn(fallTrack, fall, fallStart, 0, Math.Min(fall.length, T_End - fallStart), FallVol);

        var ring = EndClip("Ear_Ring");
        var ringTrack = tl.CreateTrack<AudioTrack>(null, "Cinlama");
        if (ring) AudioClipOn(ringTrack, ring, T_Black + RingDelay, 0, Math.Min(ring.length, T_End - T_Black - RingDelay), RingVol);

        EditorUtility.SetDirty(tl);
        AssetDatabase.SaveAssets();
        return tl;
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

    static void SetupAudioImport()
    {
        foreach (var n in EndSfx)
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(EndSfxDir + "/" + n + ".mp3");
            if (!imp) continue;
            var s = imp.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ arka ışık
    // Dolabın arkasından, tavana yakın sönük soğuk ışık. Dolap dikken gölgesi oyuncunun önünde biter;
    // gıcırtıyla 5° eğilince gölge örtünün baş ucuna ve duvara kayar (fener öne baktığı için gölge ondan gelemez).
    // Yoğunluk candela: 150 cd örtüde ~2.5 lux (fener ~100 lux), oda karanlık kalır ama gölge seçilir.
    const float BackLightCd = 150f, BackLightBack = 1.45f, BackLightUp = 2.6f, BackLightReach = 2.6f;

    static Light SetupBackLight(Transform root)
    {
        var go = new GameObject("BackLight");
        go.transform.SetParent(root, false);
        var pos = cabTopFront - fallAway * BackLightBack + Vector3.up * BackLightUp;
        if (Physics.Raycast(cabTopFront + Vector3.up * 0.05f, Vector3.up, out var ceil, 10f))
            pos.y = Mathf.Min(pos.y, ceil.point.y - 0.2f);
        var aim = cabTopFront + fallAway * BackLightReach;
        aim.y = floorY + 0.65f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim - pos));
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = 60f;
        l.innerSpotAngle = 25f;
        l.range = 16f;
        l.intensity = BackLightCd;
        l.color = new Color(0.62f, 0.72f, 1f);
        l.shadows = LightShadows.Soft;
        l.enabled = false;   // EndCinematic sinematikte açar
        return l;
    }

    // ------------------------------------------------------------------ kare kontrolü
    // Sinematik kamerayı verilen anlarda render edip Temp/ICShots/end_<t>.png olarak kaydeder; sahne değişmeden kalır.
    [MenuItem("Tools/End Cinematic/Render Check Frames")]
    public static void RenderCheckFrames() => RenderFrames(new[] { 0.5f, 3f, 8f, 12.9f, 15.5f, T_TurnEnd, T_Black - 0.1f });

    public static void RenderFrames(float[] times)
    {
        var root = GameObject.Find("EndCinematic");
        var ec = root ? root.GetComponent<EndCinematic>() : null;
        if (!ec) { Debug.LogError("End cinematic: önce Tools > End Cinematic > Build"); return; }
        Directory.CreateDirectory("Temp/ICShots");
        bool rigWas = ec.rig.activeSelf;
        var cam = ec.cinematicCamera;
        var rt = new RenderTexture(1280, 720, 24);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            ec.rig.SetActive(true);
            foreach (float t in times)
            {
                ec.director.time = t;
                ec.director.Evaluate();
                ec.ApplyCabinet(t);
                ec.ApplyBackLight(t);
                var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/ICShots/end_" + t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ".png", tex.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = null;
            ec.ResetCabinet();
            if (ec.backLight) ec.backLight.enabled = false;
            ec.director.time = 0;
            ec.director.Evaluate();
            ec.rig.SetActive(rigWas);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
