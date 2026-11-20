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
}
