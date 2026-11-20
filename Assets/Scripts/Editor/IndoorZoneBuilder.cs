using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sahnedeki kapalı alanlara IndoorZone (+ AudioReverbZone) kurar. Tekrar çalıştırmak güvenli: aynı adlı bölgeyi günceller.
// Ölçüler duvar/pencere/zemin ölçümünden (dünya koordinatı). Bölge yapının altına konur, yapı taşınırsa onunla gider.
public static class IndoorZoneBuilder
{
    struct Spec
    {
        public string name, parent;
        public Vector3 center, size;
        public float yaw, muffle;
        public AudioReverbPreset reverb;
        public Spec(string name, string parent, Vector3 center, Vector3 size, float yaw, float muffle, AudioReverbPreset reverb)
        { this.name = name; this.parent = parent; this.center = center; this.size = size; this.yaw = yaw; this.muffle = muffle; this.reverb = reverb; }
    }

    static readonly Spec[] Zones =
    {
        // Ev: pencereler x -132.3 / -85.8, z 181.3 / 257.6; zemin 10.56, tavan 17.93
        new Spec("IndoorZone_Ev", "MainHouse_V2", new Vector3(-109.05f, 14.25f, 219.45f), new Vector3(46.5f, 7.4f, 76.3f), 0f, 0.7f, AudioReverbPreset.Room),
        // Bodrum (Bodrum_Oda), evin altında; küçük olduğu için içindeyken bodrum geçerli
        new Spec("IndoorZone_Bodrum", "MainHouse_V2", new Vector3(-123.3f, 6.0f, 232.0f), new Vector3(16.6f, 6.1f, 13.5f), 0f, 1f, AudioReverbPreset.Stoneroom),
        new Spec("IndoorZone_Kulube", "Kulube_Model", new Vector3(231.0f, 13.5f, 665.3f), new Vector3(5.1f, 6.2f, 7.6f), 197f, 0.6f, AudioReverbPreset.Room),
        // Ahır tahta aralıklı, kapısı aralık: dışarısı az boğulur
        new Spec("IndoorZone_Ahir", "Barn_Model", new Vector3(-128.0f, 17.0f, 512.0f), new Vector3(54.4f, 14f, 37.2f), 16f, 0.4f, AudioReverbPreset.Hangar),
        new Spec("IndoorZone_Barinak", "Shelter_Model", new Vector3(-20.0f, 12.8f, 602.0f), new Vector3(29.4f, 5.4f, 13.4f), 256f, 0.6f, AudioReverbPreset.Room),
    };

    [MenuItem("Tools/Audio/Build Indoor Zones")]
    public static void Build()
    {
        int n = 0;
        foreach (var s in Zones)
        {
            var parent = GameObject.Find(s.parent);
            if (!parent) { Debug.LogWarning("IndoorZone: " + s.parent + " bulunamadı, " + s.name + " atlandı"); continue; }
            var t = parent.transform.Find(s.name);
            var go = t ? t.gameObject : new GameObject(s.name);
            if (!t) Undo.RegisterCreatedObjectUndo(go, "Build Indoor Zones");
            go.transform.SetParent(parent.transform, true);
            go.transform.SetPositionAndRotation(s.center, Quaternion.Euler(0f, s.yaw, 0f));

            var z = go.GetComponent<IndoorZone>();
            if (!z) z = go.AddComponent<IndoorZone>();
            z.size = s.size;
            z.outsideMuffle = s.muffle;
            z.reverb = s.reverb;

            var rt = go.transform.Find("Reverb");
            var rgo = rt ? rt.gameObject : new GameObject("Reverb");
            rgo.transform.SetParent(go.transform, false);
            var rz = rgo.GetComponent<AudioReverbZone>();
            if (!rz) rz = rgo.AddComponent<AudioReverbZone>();
            rz.reverbPreset = s.reverb;
            rz.minDistance = 0.25f;
            rz.maxDistance = Mathf.Max(0.3f, z.edgeFade);
            EditorUtility.SetDirty(z);
            EditorUtility.SetDirty(rz);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("IndoorZone: " + n + " kapalı alan kuruldu");
    }

    // Tarla (çitli Field: x 135..412, z 62..469): içeride kargalar, cırcır böceği kısılır
    const string RavensClip = "Assets/Sfx/Ambience/Ambience_Ravens.mp3";

    [MenuItem("Tools/Audio/Build Field Ambience")]
    public static void BuildField()
    {
        var field = GameObject.Find("Field");
        if (!field) { Debug.LogWarning("AmbienceZone: Field bulunamadı"); return; }
        var imp = (AudioImporter)AssetImporter.GetAtPath(RavensClip);
        if (imp)
        {
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            imp.defaultSampleSettings = s;
            imp.loadInBackground = true;
            imp.SaveAndReimport();
        }

        var t = field.transform.Find("AmbienceZone_Tarla");
        var go = t ? t.gameObject : new GameObject("AmbienceZone_Tarla");
        if (!t) Undo.RegisterCreatedObjectUndo(go, "Build Field Ambience");
        go.transform.SetParent(field.transform, true);
        go.transform.SetPositionAndRotation(new Vector3(273.5f, 12f, 265.5f), Quaternion.identity);

        var loop = go.GetComponent<AmbienceLoop>();
        if (!loop) loop = go.AddComponent<AmbienceLoop>();   // AudioSource + AudioLowPassFilter'ı RequireComponent ekler
        loop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RavensClip);
        loop.volume = 0.5f;
        loop.pitchRange = new Vector2(0.96f, 1.04f);
        var src = go.GetComponent<AudioSource>();
        src.playOnAwake = false;

        var z = go.GetComponent<AmbienceZone>();
        if (!z) z = go.AddComponent<AmbienceZone>();
        z.size = new Vector3(277f, 12f, 407f);
        z.edgeFade = 10f;
        z.own = loop;
        z.duckLevel = 0.3f;
        var crickets = GameObject.Find("Crickets");
        z.duck = crickets && crickets.GetComponent<AmbienceLoop>() ? new[] { crickets.GetComponent<AmbienceLoop>() } : new AmbienceLoop[0];
        if (z.duck.Length == 0) Debug.LogWarning("AmbienceZone: Crickets bulunamadı, kısılacak ses yok");
        EditorUtility.SetDirty(loop);
        EditorUtility.SetDirty(z);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("AmbienceZone: tarla kuruldu (kargalar, cırcır %" + (z.duckLevel * 100) + ")");
    }
}
