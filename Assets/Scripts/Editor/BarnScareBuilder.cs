using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Ahır gerilim olayını sahneye kurar (BarnScare). Tekrar çalıştırılabilir: kendi objesini yeniden kurar.
// - Sağ ön kapı (Ahir_Kapi_Sag_Aralik) aralığı 1.75 m'den ~0.6 m'ye daraltılır (oyuncu sığmaz) ve SlidingDoor eklenir.
// - Kuzey samanlık kenarına 3 balya, arkalarına siluet, toz efekti; tetik girişin ~6 m içinde, koridor boyunca.
public static class BarnScareBuilder
{
    const string SfxDir = "Assets/Sfx/Ending";
    const string MatDir = "Assets/Materials";

    // Kapının sahnedeki ilk yeri (BarnDoors altında, ebeveyn dönüşsüz) ve daraltma miktarı
    static readonly Vector3 DoorOriginalLocal = new Vector3(26.51f, 0.02f, -5.73f);
    const float DoorNarrow = 1.15f;

    // Ön duvar boyunca (kapının kayma yönü, kuzeye doğru)
    static readonly Vector3 WallAxis = new Vector3(0.28f, 0f, 0.96f).normalized;

    static readonly Vector3 GapCenter = new Vector3(-101.48f, 10f, 505.44f); // kapı açılınca giriş ortası (zemin)
    const float TriggerDepth = 6.2f;           // girişten içeri
    static readonly Vector3 TriggerSize = new Vector3(10f, 3f, 1.5f);
    static readonly float[] BaleX = { -110f, -112.4f, -114.8f }; // balyaların kenar boyunca yerleri (dünya x)
    static readonly float[] BaleSpins = { 180f, 165f, 195f };  // yere yatık, biraz dağınık insinler
    const float BaleInset = 0.9f;             // balya merkezi kenardan samanlığa doğru
    const float LandOut = 2.5f;                // kenardan koridora iniş mesafesi
    const float ShadowX = -116.9f;
    const float ShadowInset = 0.6f;

    [MenuItem("Tools/Level/Build Barn Scare")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Play modundayken kurulmaz."); return; }

        Vector3 inward = -Vector3.Cross(Vector3.up, WallAxis);    // (−0.96, 0, 0.28) ahırın içine
        Vector3 outward = -WallAxis;                               // kuzey kenardan koridora

        // 1) Kapı
        var doorGo = GameObject.Find("Ahir_Kapi_Sag_Aralik");
        if (doorGo == null) { Debug.LogError("Ahir_Kapi_Sag_Aralik bulunamadı."); return; }
        Undo.RecordObject(doorGo.transform, "Barn door narrow");
        doorGo.transform.localPosition = DoorOriginalLocal - WallAxis * DoorNarrow;
        var door = doorGo.GetComponent<SlidingDoor>();
        if (door == null) door = Undo.AddComponent<SlidingDoor>(doorGo);
        Undo.RecordObject(door, "Barn door setup");
        door.localAxis = WallAxis;
        door.distance = BarnScareMath.DoorSlideDistance;
        door.duration = BarnScareMath.DoorSlideDuration;
        door.openText = "Kapıyı Aç";
        EditorUtility.SetDirty(door);
        Physics.SyncTransforms();

        // 2) Kök obje (yerel z = içeri)
        var old = GameObject.Find("BarnScare");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("BarnScare");
        Undo.RegisterCreatedObjectUndo(root, "Build Barn Scare");
        root.transform.SetPositionAndRotation(GapCenter + inward * TriggerDepth + Vector3.up * 1.5f,
                                              Quaternion.LookRotation(inward, Vector3.up));
        var scare = root.AddComponent<BarnScare>();
        scare.door = door;
        scare.areaSize = TriggerSize;
        scare.areaCenter = Vector3.zero;

        // 3) Balyalar
        var src = FindBaleSource();
        if (src == null) { Debug.LogError("Saman_Balyalari/HaiSquare bulunamadı."); Object.DestroyImmediate(root); return; }
        // Kenarda korkuluk var (üst barı zeminden ~0.9 m): iki kat yığın; alttakiler kalır, üsttekiler
        // korkuluğun üstünden devrilir.
        var bales = new Transform[BaleX.Length];
        var heights = new float[BaleX.Length];
        Quaternion baleRot = Quaternion.LookRotation(inward, Vector3.up);   // uzun kenar duvara paralel
        for (int i = 0; i < BaleX.Length; i++)
        {
            Vector3 edge = LoftEdge(BaleX[i]);
            Vector3 pos = edge + WallAxis * BaleInset;                       // samanlığın içine
            pos.y = edge.y;

            var under = Object.Instantiate(src, root.transform);
            under.name = "Alt_Balya_" + (i + 1);
            under.transform.SetPositionAndRotation(pos, baleRot);
            if (under.GetComponentInChildren<Collider>() == null) under.AddComponent<BoxCollider>();
            pos.y = RenderBounds(under).max.y;                               // üstüne

            var b = Object.Instantiate(src, root.transform);
            b.name = "Dusen_Balya_" + (i + 1);
            b.transform.SetPositionAndRotation(pos, baleRot);
            if (b.GetComponentInChildren<Collider>() == null) b.AddComponent<BoxCollider>();
            bales[i] = b.transform;

            // İniş yüksekliği: son dönüşüyle alt köşesi koridor zeminine değsin
            float floorY = FloorAt(pos + outward * (BaleInset + LandOut));
            b.transform.rotation = Quaternion.AngleAxis(BaleSpins[i], inward) * baleRot;
            float minY = RenderBounds(b).min.y;
            b.transform.SetPositionAndRotation(pos, baleRot);
            heights[i] = Mathf.Max(0f, minY - floorY);
        }
        scare.bales = bales;
        scare.baleHeights = heights;
        scare.baleSpins = (float[])BaleSpins.Clone();
        scare.tipAxis = inward;
        scare.outward = outward;
        scare.outwardReach = BaleInset + LandOut;

        // 4) Kadraj: ortadaki balyanın üstü
        var look = new GameObject("LookTarget").transform;
        look.SetParent(root.transform, false);
        look.position = bales[1].position + Vector3.up * 0.5f;
        scare.lookTarget = look;

        // 5) Siluet: ortadaki balyanın arkasında, samanlıkta
        // Alt balyaların arkasında kalırsa görünmez: 3. balyanın ötesinde, kenara yakın durur
        Vector3 shadowEdge = LoftEdge(ShadowX);
        scare.shadow = BuildShadow(root.transform, shadowEdge + WallAxis * ShadowInset);
        scare.shadowExit = WallAxis * 2f + inward * 0.5f;

        // 6) Toz
        scare.dust = BuildDust(root.transform, bales[1].position);

        // 7) Sesler (geçici: oyun sonu sinematiğinin klipleri)
        scare.creak = Clip("Cabinet_Creak");
        scare.baleThud = Clip("Cabinet_Fall");
        scare.heartbeat = Clip("Heartbeat_Loop");
        scare.gasp = Clip("Breath_Gasp");

        EditorUtility.SetDirty(scare);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log("Barn Scare kuruldu. Balya yükseklikleri: " + string.Join(", ", heights));
    }

    static GameObject FindBaleSource()
    {
        var parent = GameObject.Find("Saman_Balyalari");
        if (parent == null) return null;
        foreach (Transform c in parent.transform) if (c.name == "HaiSquare") return c.gameObject;
        return null;
    }

    // Kuzey samanlık katının koridora bakan kenarı (verilen x'te), üst yüzey yüksekliğiyle
    static Vector3 LoftEdge(float x)
    {
        // Çatı ve süslemelere çarpmamak için katın hemen üstünden aşağı bakılır (kat y≈14.8)
        // Koridordaki kirişlere takılmamak için arkasında 1.5 m kesintisiz zemin olan ilk nokta kenar sayılır
        for (float z = 508f; z <= 525f; z += 0.05f)
        {
            if (!LoftAt(x, z, out float y)) continue;
            if (LoftAt(x, z + 0.5f, out _) && LoftAt(x, z + 1f, out _) && LoftAt(x, z + 1.5f, out _))
                return new Vector3(x, y, z);
        }
        Debug.LogWarning("Samanlık kenarı bulunamadı, x=" + x);
        return new Vector3(x, 14.8f, 513.5f);
    }

    static bool LoftAt(float x, float z, out float y)
    {
        y = 0f;
        if (!Physics.Raycast(new Vector3(x, 16f, z), Vector3.down, out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore)) return false;
        y = h.point.y;
        return y > 14f && y < 15.5f;
    }

    static float FloorAt(Vector3 p) =>
        Physics.Raycast(new Vector3(p.x, 13.5f, p.z), Vector3.down, out RaycastHit h, 6f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 10f;

    static Bounds RenderBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform BuildShadow(Transform parent, Vector3 feet)
    {
        // Işıksız siyah karanlık çatının önünde kayboluyor; koyu ama fenerin aydınlattığı bir malzeme
        var mat = LoadOrCreateMat(MatDir + "/Siluet_Koyu.mat", "Universal Render Pipeline/Lit", new Color(0.05f, 0.045f, 0.04f));
        var root = new GameObject("Siluet").transform;
        root.SetParent(parent, false);
        root.position = feet;
        var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyGo.name = "Govde";
        bodyGo.transform.SetParent(root, false);
        bodyGo.transform.localScale = new Vector3(0.5f, 0.75f, 0.35f);  // 1.5 m gövde
        bodyGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Bas";
        head.transform.SetParent(root, false);
        head.transform.localScale = Vector3.one * 0.26f;
        head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        foreach (var g in new[] { bodyGo, head })
        {
            Object.DestroyImmediate(g.GetComponent<Collider>());
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return root;
    }

    static ParticleSystem BuildDust(Transform parent, Vector3 pos)
    {
        var tint = new Color(0.55f, 0.48f, 0.38f, 0.35f);
        var mat = LoadOrCreateMat(MatDir + "/Ahir_Toz.mat", "Universal Render Pipeline/Particles/Unlit", tint);
        if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") < 0.5f)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(mat);
        }
        var go = new GameObject("Toz");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startColor = tint;
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.8f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static Material LoadOrCreateMat(string path, string shader, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find(shader));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static AudioClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + name + ".mp3");
        if (c == null) Debug.LogWarning("Ses bulunamadı: " + name);
        return c;
    }
}
