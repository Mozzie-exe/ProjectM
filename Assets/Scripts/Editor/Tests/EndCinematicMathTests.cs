using NUnit.Framework;

public class EndCinematicMathTests
{
    [Test]
    public void DoorLook_Profile()
    {
        Assert.AreEqual(0f, EndCinematicMath.DoorLookWeight(0f), 1e-4f);
        Assert.AreEqual(1f, EndCinematicMath.DoorLookWeight(1.1f), 1e-4f);
        Assert.AreEqual(1f, EndCinematicMath.DoorLookWeight(1.7f), 1e-4f);
        Assert.AreEqual(0f, EndCinematicMath.DoorLookWeight(3f), 1e-4f);
    }

    [Test]
    public void StartBlend_ReachesOneAt1_2s()
    {
        Assert.AreEqual(0f, EndCinematicMath.StartBlend(0f), 1e-4f);
        Assert.AreEqual(1f, EndCinematicMath.StartBlend(1.2f), 1e-4f);
        Assert.AreEqual(1f, EndCinematicMath.StartBlend(5f), 1e-4f);
    }

    [Test]
    public void Cabinet_StillBeforeCreak()
    {
        Assert.AreEqual(0f, EndCinematicMath.CabinetAngle(15.4f, 15.5f, 16.6f), 1e-4f);
    }

    [Test]
    public void Cabinet_LeansSlowlyDuringCreak()
    {
        // Gıcırtıdan devrilmeye kadar 5°'ye eğilir (arkadan gelen ışıkta gölgesi öne kayar)
        Assert.AreEqual(EndCinematicMath.CabinetPreLean, EndCinematicMath.CabinetAngle(16.6f, 15.5f, 16.6f), 1e-4f);
        float mid = EndCinematicMath.CabinetAngle(16.05f, 15.5f, 16.6f);
        Assert.Greater(mid, 0f);
        Assert.Less(mid, EndCinematicMath.CabinetPreLean);
    }

    [Test]
    public void Cabinet_FallAcceleratesAndEndsAt35()
    {
        float lean = EndCinematicMath.CabinetPreLean;
        Assert.AreEqual(35f, EndCinematicMath.CabinetAngle(17.4f, 15.5f, 16.6f), 1e-4f);
        float a2 = EndCinematicMath.CabinetAngle(16.8f, 15.5f, 16.6f) - lean;
        float a4 = EndCinematicMath.CabinetAngle(17.0f, 15.5f, 16.6f) - lean;
        Assert.Less(a2, a4 / 2f);
    }

    [Test]
    public void Hush_LeavesCinematicAndMaster()
    {
        CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Cinematic);
        CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Master);
        CollectionAssert.Contains(EndCinematicMath.HushChannels, AudioChannel.Music);
    }
}
