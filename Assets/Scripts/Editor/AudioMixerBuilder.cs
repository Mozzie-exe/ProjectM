using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// MainMixer'ı ve GameAudioSettings asset'ini kurar. Tekrar çalıştırmak güvenli:
// eksik grup/parametreyi ekler, mevcut seviyeleri korur.
// Unity grup oluşturmak için public API sunmadığından dahili AudioMixerController reflection ile kullanılır.
public static class AudioMixerBuilder
{
    public const string MixerPath = "Assets/Audio/MainMixer.mixer";
    public const string SettingsPath = "Assets/Resources/GameAudioSettings.asset";

    // grup -> ebeveyn (Master kökte)
    static readonly (AudioChannel id, AudioChannel parent)[] Layout =
    {
        (AudioChannel.Music, AudioChannel.Master),
        (AudioChannel.SFX, AudioChannel.Master),
        (AudioChannel.Cinematic, AudioChannel.SFX),
        (AudioChannel.Ambience, AudioChannel.Master),
        (AudioChannel.UI, AudioChannel.Master),
    };

    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Assembly Ed = typeof(Editor).Assembly;
    static readonly Type ControllerT = Ed.GetType("UnityEditor.Audio.AudioMixerController");
    static readonly Type GroupT = Ed.GetType("UnityEditor.Audio.AudioMixerGroupController");
    static readonly Type GroupPathT = Ed.GetType("UnityEditor.Audio.AudioGroupParameterPath");

    [MenuItem("Tools/Audio/Build Mixer")]
    public static void Build()
    {
        MakeFolder("Assets/Audio");
        MakeFolder("Assets/Resources");

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (!mixer)
            mixer = (AudioMixer)ControllerT.GetMethod("CreateMixerControllerAtPath", F).Invoke(null, new object[] { MixerPath });

        var groups = new Dictionary<AudioChannel, AudioMixerGroup>();
        groups[AudioChannel.Master] = (AudioMixerGroup)ControllerT.GetProperty("masterGroup", F).GetValue(mixer);
        foreach (var g in mixer.FindMatchingGroups(""))
            if (Enum.TryParse(g.name, out AudioChannel id) && id != AudioChannel.Master) groups[id] = g;

        foreach (var (id, parent) in Layout)
        {
            if (groups.ContainsKey(id)) continue;
            var g = (AudioMixerGroup)ControllerT.GetMethod("CreateNewGroup", F).Invoke(mixer, new object[] { id.ToString(), false });
            ControllerT.GetMethod("AddChildToParent", F).Invoke(mixer, new object[] { g, groups[parent] });
            groups[id] = g;
        }

        foreach (var kv in groups)
            Expose(mixer, kv.Value, ParamName(kv.Key));

        EditorUtility.SetDirty(mixer);

        var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(SettingsPath);
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<GameAudioSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.mixer = mixer;
        var list = new List<GameAudioSettings.Channel>(settings.channels ?? new GameAudioSettings.Channel[0]);
        foreach (AudioChannel id in Enum.GetValues(typeof(AudioChannel)))
        {
            var c = list.Find(x => x != null && x.id == id);
            if (c == null) list.Add(c = new GameAudioSettings.Channel { id = id, level = 1f });
            c.group = groups[id];
            c.param = ParamName(id);
        }
        settings.channels = list.ToArray();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("MainMixer kuruldu: " + groups.Count + " grup. Seviyeler: " + SettingsPath);
    }

    public static string ParamName(AudioChannel id) => id + "Vol";

    // Grubun Volume'unu verilen adla expose et (zaten varsa sadece adı düzelt)
    static void Expose(AudioMixer mixer, AudioMixerGroup group, string name)
    {
        var guid = GroupT.GetMethod("GetGUIDForVolume", F).Invoke(group, null);
        bool has = (bool)ControllerT.GetMethod("ContainsExposedParameter", F).Invoke(mixer, new[] { guid });
        if (!has)
        {
            var path = Activator.CreateInstance(GroupPathT, F, null, new[] { group, guid }, null);
            ControllerT.GetMethod("AddExposedParameter", F).Invoke(mixer, new[] { path });
        }

        var prop = ControllerT.GetProperty("exposedParameters", F);
        var arr = (Array)prop.GetValue(mixer);
        var elemT = arr.GetType().GetElementType();
        var guidF = elemT.GetField("guid", F);
        var nameF = elemT.GetField("name", F);
        for (int i = 0; i < arr.Length; i++)
        {
            var e = arr.GetValue(i);
            if (!guidF.GetValue(e).Equals(guid)) continue;
            nameF.SetValue(e, name);
            arr.SetValue(e, i); // struct: kopyayı geri yaz
        }
        prop.SetValue(mixer, arr);
        ControllerT.GetMethod("OnChangedExposedParameter", F)?.Invoke(mixer, null);
    }

    static void MakeFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
