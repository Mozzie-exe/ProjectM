using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Toprak yolun ana yoldan ayrıldığı yere eskimiş sokak lambaları: biri kavşakta, biri toprak yolun biraz içinde.
// Loş, sıcak ışık; gece yoldan bakınca "buradan sapılır" diye okunur. Model: BlenderSource/Level/build_street_lamp.py
// Tekrar çalıştırmak güvenli: eski "StreetLamps" silinip yeniden kurulur.
public static class StreetLampBuilder
{
    const float K = 1.78f;              // dünya ölçeği (sinematikteki araba ile aynı)
    const string Dir = "Assets/Models/StreetLamp";
    const string Fbx = Dir + "/StreetLamp_Old.fbx";
    const float PoleHeight = 7.6f;      // build_street_lamp.py H
    static readonly Color Warm = new Color(1f, 0.72f, 0.42f);

    struct Spec
    {
        public string name; public Vector3 pos, aim; public float flicker; public bool shadows;
        public Spec(string name, Vector3 pos, Vector3 aim, float flicker, bool shadows)
        { this.name = name; this.pos = pos; this.aim = aim; this.flicker = flicker; this.shadows = shadows; }
    }

    // Toprak yol x -4..14 boyunca kuzeye gider (z 8'den itibaren); ana yolun kuzey kenarı z 4.3. Kol yola doğru uzanır.
    static readonly Spec[] Lamps =
    {
        // Yolun karşı kenarında, toprak yol ağzının tam karşısında: kuzey kenardaki ağaçlar park yerinden bakışı kesmez,
        // kol yolun üstüne (kavşağa) uzanır
        new Spec("StreetLamp_Kavsak", new Vector3(16f, 0f, -23f), new Vector3(10f, 0f, -8f), 0.05f, true),
        new Spec("StreetLamp_ToprakYol", new Vector3(18f, 0f, 56f), new Vector3(5f, 0f, 56f), 0.16f, false),
    };

    [MenuItem("Tools/Level/Build Street Lamps")]
    public static void Build()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (!prefab) { Debug.LogWarning("StreetLamp: " + Fbx + " yok, önce Blender scriptini çalıştır"); return; }
        SetupMaterials();

        var old = GameObject.Find("StreetLamps");
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject("StreetLamps");
        var glareMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/IntroCinematic/Materials/IC_Glare.mat");

        foreach (var s in Lamps)
        {
            var lamp = new GameObject(s.name);
            lamp.transform.SetParent(root.transform, false);
            Vector3 p = s.pos;
            // Zemin: yukarıdan ışın (yol levhası araziden yüksek); direk biraz gömülü
            var terrain = Terrain.activeTerrain;
            p.y = terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : 10f;
            if (Physics.Raycast(new Vector3(p.x, p.y + 50f, p.z), Vector3.down, out var hit, 100f)) p.y = hit.point.y;
            p.y -= 0.15f;
            lamp.transform.position = p;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.name = "Model";
            model.transform.SetParent(lamp.transform, false);
            model.transform.localScale = Vector3.one * K;

            // Kolu hedefe çevir: ampulün direğe göre yönü modelden okunur (eksen dönüşümüne güvenmeden)
            var bulb = model.transform.Find("StreetLamp_Bulb").GetComponent<Renderer>();
            Vector3 bl = lamp.transform.InverseTransformPoint(bulb.bounds.center);
            float armYaw = Mathf.Atan2(bl.x, bl.z) * Mathf.Rad2Deg;
            Vector3 to = s.aim - p;
            lamp.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - armYaw, 0f);
            Vector3 bulbW = lamp.transform.TransformPoint(bl);

            // Loş sokak ışığı: aşağı bakan geniş spot + şapkanın içini aydınlatan küçük nokta ışık
            var spot = MakeLight(lamp.transform, "Light", bulbW + Vector3.down * 0.1f, Quaternion.Euler(90f, 0f, 0f),
                LightType.Spot, 90f, 30f, s.shadows ? LightShadows.Soft : LightShadows.None);
            spot.spotAngle = 120f;
            spot.innerSpotAngle = 45f;
            var fill = MakeLight(lamp.transform, "ShadeFill", bulbW + Vector3.down * 0.35f, Quaternion.identity,
                LightType.Point, 1.2f, 3f, LightShadows.None);

            // Uzaktan okunan hale (kameraya dönük, her yönden)
            if (glareMat)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Glare";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(lamp.transform, false);
                q.transform.position = bulbW + Vector3.down * 0.05f;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = glareMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                var g = q.AddComponent<HeadlightGlare>();
                g.omni = true;
                g.size = 1.1f;
                g.distanceGrow = 0.018f;
                g.maxSize = 2.4f;     // uzaktan (sürüşte) güneş gibi top değil, küçük bir ışık noktası
                g.farDim = 300f;
                g.color = new Color(1f, 0.72f, 0.42f, 1f) * 0.8f;
            }

            // Direk oyuncuyu durdursun
            var col = lamp.AddComponent<CapsuleCollider>();
            col.radius = 0.25f;
            col.height = PoleHeight * K;
            col.center = new Vector3(0f, PoleHeight * K * 0.5f, 0f);

            var f = lamp.AddComponent<LampFlicker>();
            f.lights = new[] { spot, fill };
            f.glows = new Renderer[0];
            f.amount = s.flicker;
            f.speed = 2.2f;
        }

        Undo.RegisterCreatedObjectUndo(root, "Build Street Lamps");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("StreetLamp: " + Lamps.Length + " lamba kuruldu");
    }

    static Light MakeLight(Transform parent, string name, Vector3 pos, Quaternion rot, LightType type, float intensity, float range, LightShadows sh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = Warm;
        l.intensity = intensity;
        l.range = range;
        l.shadows = sh;
        l.shadowBias = 0.02f;
        l.shadowNormalBias = 0.3f;
        return l;
    }

    // ------------------------------------------------------------------ malzemeler
    static void SetupMaterials()
    {
        if (!AssetDatabase.IsValidFolder(Dir + "/Materials")) AssetDatabase.CreateFolder(Dir, "Materials");
        var mats = new System.Collections.Generic.Dictionary<string, Material>
        {
            ["SL_Wood"] = Mat("SL_Wood", "SL_Wood", Color.white, 0f, 0.1f, null),
            ["SL_Enamel"] = Mat("SL_Enamel", "SL_Enamel", Color.white, 0.2f, 0.35f, null),
            ["SL_Iron"] = Mat("SL_Iron", null, new Color(0.17f, 0.15f, 0.14f), 0.6f, 0.3f, null),
            ["SL_BulbGlow"] = Mat("SL_BulbGlow", null, new Color(1f, 0.9f, 0.7f), 0f, 0.8f, new Color(2.6f, 1.6f, 0.75f)),
        };
        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var kv in mats)
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        imp.SaveAndReimport();
    }

    static Material Mat(string name, string tex, Color color, float metallic, float smooth, Color? emission)
    {
        string path = Dir + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        if (tex != null)
        {
            string tp = Dir + "/Textures/" + tex + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
            if (ti && ti.maxTextureSize != 512) { ti.maxTextureSize = 512; ti.SaveAndReimport(); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        }
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
