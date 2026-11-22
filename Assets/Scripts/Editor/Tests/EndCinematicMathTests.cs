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
    public void CabinetFall_StartsStillEndsAt35()
    {
        Assert.AreEqual(0f, EndCinematicMath.CabinetFallAngle(0f), 1e-4f);
        Assert.AreEqual(35f, EndCinematicMath.CabinetFallAngle(0.8f), 1e-4f);
        Assert.Less(EndCinematicMath.CabinetFallAngle(0.2f), EndCinematicMath.CabinetFallAngle(0.4f) / 2f);
    }

    [Test]
    public void Hush_LeavesCinematicAndMaster()
    {
        CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Cinematic);
        CollectionAssert.DoesNotContain(EndCinematicMath.HushChannels, AudioChannel.Master);
        CollectionAssert.Contains(EndCinematicMath.HushChannels, AudioChannel.Music);
    }
}
