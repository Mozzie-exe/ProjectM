using NUnit.Framework;
using UnityEngine;

public class GameAudioTests
{
    [Test]
    public void LinearToDb_FullLevel_IsZeroDb()
    {
        Assert.AreEqual(0f, GameAudioSettings.LinearToDb(1f), 0.0001f);
    }

    [Test]
    public void LinearToDb_Zero_IsMinus80NotInfinity()
    {
        Assert.AreEqual(-80f, GameAudioSettings.LinearToDb(0f));
    }

    [Test]
    public void LinearToDb_Half_IsAboutMinus6Db()
    {
        Assert.AreEqual(-6.02f, GameAudioSettings.LinearToDb(0.5f), 0.01f);
    }

    [Test]
    public void SetVolume_AboveOne_IsClampedToOne()
    {
        var s = ScriptableObject.CreateInstance<GameAudioSettings>();
        s.SetVolume(AudioChannel.SFX, 2f);
        Assert.AreEqual(1f, s.GetVolume(AudioChannel.SFX));
        Object.DestroyImmediate(s);
    }

    [Test]
    public void BuiltMixer_EveryChannelHasGroupAndExposedParam()
    {
        var s = GameAudio.Settings;
        Assert.IsNotNull(s, "Resources/GameAudioSettings.asset yok");
        Assert.IsNotNull(s.mixer);
        foreach (AudioChannel ch in System.Enum.GetValues(typeof(AudioChannel)))
        {
            Assert.IsNotNull(s.GetGroup(ch), ch + " grubu yok");
            Assert.IsTrue(s.mixer.GetFloat(ch + "Vol", out _), ch + "Vol expose edilmemiş");
        }
        Assert.AreEqual(6, s.mixer.FindMatchingGroups("").Length);
        Assert.AreEqual(1, s.mixer.FindMatchingGroups("Master/SFX/Cinematic").Length, "Cinematic, SFX'in altında değil");
    }

    [Test]
    public void SetVolume_WithoutMixer_DoesNotThrow()
    {
        var s = ScriptableObject.CreateInstance<GameAudioSettings>();
        Assert.DoesNotThrow(() => s.SetVolume(AudioChannel.Music, 0.3f));
        Assert.AreEqual(0.3f, s.GetVolume(AudioChannel.Music), 0.0001f);
        Object.DestroyImmediate(s);
    }

    [Test]
    public void Rolloff_FullUpToMin_SilentAtMax_Monotonic()
    {
        var c = GameAudio.Rolloff(2f, 30f);
        Assert.AreEqual(1f, c.Evaluate(1f / 30f), 0.01f);
        Assert.AreEqual(0f, c.Evaluate(1f), 0.001f);
        Assert.AreEqual(0.5f, c.Evaluate(4f / 30f), 0.05f, "iki katı mesafede yarı ses (-6 dB)");
        float prev = 2f;
        for (float x = 0f; x <= 1f; x += 0.01f) { float g = c.Evaluate(x); Assert.LessOrEqual(g, prev + 0.01f); prev = g; }
    }

    [Test]
    public void IndoorZone_RotatedBox_ContainsAndFadesAtEdge()
    {
        var go = new GameObject("z");
        go.transform.SetPositionAndRotation(new Vector3(10, 0, 0), Quaternion.Euler(0, 90, 0));
        var z = go.AddComponent<IndoorZone>();
        z.size = new Vector3(10, 4, 2);   // 90 derece: dünyada z boyunca 10, x boyunca 2
        z.edgeFade = 2f;
        Assert.IsTrue(z.Contains(new Vector3(10, 0, 4.5f)));
        Assert.IsFalse(z.Contains(new Vector3(12, 0, 0)));
        Assert.AreEqual(1f, z.Weight(new Vector3(10.9f, 0, 0)), 0.001f);
        Assert.AreEqual(0.5f, z.Weight(new Vector3(12f, 0, 0)), 0.001f);
        Assert.AreEqual(0f, z.Weight(new Vector3(14f, 0, 0)), 0.001f);
        Object.DestroyImmediate(go);
    }
}
