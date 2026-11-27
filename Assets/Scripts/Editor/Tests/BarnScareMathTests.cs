using NUnit.Framework;
using UnityEngine;

public class BarnScareMathTests
{
    [Test]
    public void LookWeight_ZeroBeforePull()
    {
        Assert.AreEqual(0f, BarnScareMath.LookWeight(0.2f), 1e-4f);
    }

    [Test]
    public void LookWeight_FullAfterPullUntilRelease()
    {
        Assert.AreEqual(1f, BarnScareMath.LookWeight(0.9f), 1e-4f);
        Assert.AreEqual(1f, BarnScareMath.LookWeight(2.0f), 1e-4f);
        float mid = BarnScareMath.LookWeight(0.6f);
        Assert.Greater(mid, 0f);
        Assert.Less(mid, 1f);
    }

    [Test]
    public void LookWeight_ZeroAfterRelease()
    {
        Assert.AreEqual(0f, BarnScareMath.LookWeight(2.5f), 1e-4f);
    }

    [Test]
    public void SlideOffset_Clamped()
    {
        Assert.AreEqual(0f, BarnScareMath.SlideOffset(-1f, 1.2f, 2.5f), 1e-4f);
        Assert.AreEqual(0f, BarnScareMath.SlideOffset(0f, 1.2f, 2.5f), 1e-4f);
        Assert.AreEqual(2.5f, BarnScareMath.SlideOffset(1.2f, 1.2f, 2.5f), 1e-4f);
        Assert.AreEqual(2.5f, BarnScareMath.SlideOffset(5f, 1.2f, 2.5f), 1e-4f);
    }

    [Test]
    public void TipAngle_ZeroToNinety()
    {
        Assert.AreEqual(0f, BarnScareMath.TipAngle(0f), 1e-4f);
        Assert.AreEqual(90f, BarnScareMath.TipAngle(0.35f), 1e-4f);
        Assert.AreEqual(90f, BarnScareMath.TipAngle(1f), 1e-4f);
    }

    [Test]
    public void FallDrop_ClampsAtHeight()
    {
        Assert.AreEqual(0f, BarnScareMath.FallDrop(0.1f, 4.8f), 1e-4f);
        Assert.AreEqual(1.226f, BarnScareMath.FallDrop(0.675f, 4.8f), 1e-3f);
        Assert.AreEqual(4.8f, BarnScareMath.FallDrop(10f, 4.8f), 1e-4f);
    }

    [Test]
    public void CanTrigger_OnlyOpenAndNotFired()
    {
        Assert.IsTrue(BarnScareMath.CanTrigger(true, false));
        Assert.IsFalse(BarnScareMath.CanTrigger(false, false));
        Assert.IsFalse(BarnScareMath.CanTrigger(true, true));
    }

    [Test]
    public void MouseLookPitch_Clamps()
    {
        var go = new GameObject("MouseLookTest");
        try
        {
            var look = go.AddComponent<MouseLook>();
            look.Pitch = 120f;
            Assert.AreEqual(90f, look.Pitch, 1e-4f);
            look.Pitch = -30f;
            Assert.AreEqual(-30f, look.Pitch, 1e-4f);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
