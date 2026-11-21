using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Evin yola bakan (güney) iki penceresinin arkasında gaz lambası ışığı: gece yoldan, ağaçların arasından
// tahtaların aralığında sıcak bir ışık olarak okunur. İçerideki iki gaz lambası da yanar.
// Tekrar çalıştırmak güvenli: eski "HouseLights" silinip yeniden kurulur.
public static class HouseLightsBuilder
{
    const string GlowMatPath = "Assets/Materials/HouseWindowGlow.mat";
    static readonly string[] Windows = { "Cam_S_26", "Cam_S_14" };
    static readonly string[] Lamps = { "vintage_oil_lamp_flame.002", "vintage_oil_lamp_flame.003" };
    static readonly Color Warm = new Color(1f, 0.62f, 0.3f);

    [MenuItem("Tools/Level/Build House Lights")]
    public static void Build()
    {
        var house = GameObject.Find("MainHouse_V2");
        if (!house) { Debug.LogWarning("HouseLights: MainHouse_V2 bulunamadı"); return; }
        var old = house.transform.Find("HouseLights");
        if (old) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("HouseLights");
        root.transform.SetParent(house.transform, false);

        var mat = GlowMaterial();
        var lights = new System.Collections.Generic.List<Light>();
        var glows = new System.Collections.Generic.List<Renderer>();

        foreach (var n in Windows)
        {
            var glass = GameObject.Find(n);
            if (!glass) { Debug.LogWarning("HouseLights: " + n + " bulunamadı"); continue; }
            Bounds b = glass.GetComponent<Renderer>().bounds;   // cam güney duvarında (z sabit), içerisi +z
            // Camın hemen arkasında parlayan yüzey (dışarıdan bakınca tahtaların aralığından görünür)
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Glow_" + n;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(root.transform, true);
            q.transform.position = b.center + new Vector3(0f, 0f, 0.25f);
            q.transform.rotation = Quaternion.identity;               // quad -z'ye (dışarıya) bakar
            q.transform.localScale = Vector3.Scale(new Vector3(b.size.x * 0.95f, b.size.y * 0.95f, 1f), Inv(root.transform.lossyScale));
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            glows.Add(r);
            // Odayı aydınlatan ışık: gölgeli, dışarıya yalnızca pencereden taşar
            lights.Add(MakeLight(root.transform, "Light_" + n, b.center + new Vector3(0f, -0.4f, 1.9f), 4f, 8f, LightShadows.Soft));
        }
        foreach (var n in Lamps)
        {
            var flame = GameObject.Find(n);
            if (!flame) { Debug.LogWarning("HouseLights: " + n + " bulunamadı"); continue; }
            lights.Add(MakeLight(root.transform, "Lamp_" + n, flame.transform.position + Vector3.up * 0.25f, 2.2f, 5f, LightShadows.None));
        }

        var f = root.AddComponent<LampFlicker>();
        f.lights = lights.ToArray();
        f.glows = glows.ToArray();
        f.glowColor = mat.GetColor("_BaseColor");
        Undo.RegisterCreatedObjectUndo(root, "Build House Lights");
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("HouseLights: " + glows.Count + " pencere, " + lights.Count + " ışık");
    }

    static Vector3 Inv(Vector3 s) => new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

    static Light MakeLight(Transform parent, string name, Vector3 pos, float intensity, float range, LightShadows sh)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = Warm;
        l.intensity = intensity;
        l.range = range;
        l.shadows = sh;
        l.shadowBias = 0.02f;
        l.shadowNormalBias = 0.3f;
        return l;
    }

    static Material GlowMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, GlowMatPath);
        }
        m.SetColor("_BaseColor", new Color(1.5f, 0.62f, 0.2f, 1f));   // turuncu gaz lambası; HDR, bloom ile hafif hale
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
