using NUnit.Framework;
using UnityEngine;

public class ArmRigTests
{
    static readonly Vector3 S = Vector3.zero;
    static readonly Vector3 Pole = new Vector3(0.5f, -0.7f, -0.6f);

    [Test]
    public void Elbow_KeepsBoneLengths_ForAnySwivel()
    {
        Vector3 w = new Vector3(0.1f, -0.3f, 0.4f);
        foreach (float sw in new[] { -60f, 0f, 45f })
        {
            Vector3 e = ArmRig.Elbow(S, w, Vector3.down, sw, out var s);
            Assert.AreEqual(ArmRig.UpperLen, (e - s).magnitude, 1e-3f);
            Assert.AreEqual(ArmRig.ForeLen, (w - e).magnitude, 1e-3f);
        }
    }

    [Test]
    public void Elbow_SwivelRotatesAroundShoulderWristAxis()
    {
        Vector3 w = new Vector3(0f, -0.2f, 0.45f);
        Vector3 e0 = ArmRig.Elbow(S, w, Vector3.down, 0f, out _);
        Vector3 e1 = ArmRig.Elbow(S, w, Vector3.down, 90f, out _);
        Vector3 axis = w.normalized;
        float a = Vector3.SignedAngle(Vector3.ProjectOnPlane(e0, axis), Vector3.ProjectOnPlane(e1, axis), axis);
        Assert.AreEqual(90f, a, 0.5f);
    }

    [Test]
    public void Fore_PointsAlongForearm_NoFlipWhenHandUpCrossesForearmAxis()
    {
        Vector3 d = Vector3.forward;
        Quaternion prev = ArmRig.Fore(d, Quaternion.Euler(50f, 0f, 0f));
        float maxStep = 0f;
        // pitch 90'da elin yukarı ekseni ön kol eksenine paralel (eski LookRotation burada ters dönüyordu)
        for (float pitch = 50f; pitch <= 130f; pitch += 2f)
        {
            var q = ArmRig.Fore(d, Quaternion.Euler(pitch, 0f, 0f));
            Assert.Less(Vector3.Angle(q * Vector3.forward, d), 0.1f);
            maxStep = Mathf.Max(maxStep, Quaternion.Angle(prev, q));
            prev = q;
        }
        Assert.Less(maxStep, 10f, "ön kol tek adımda takla attı");
    }

    [Test]
    public void WristAngles_StraightHandIsZero()
    {
        var f = Quaternion.identity;
        Vector2 a = ArmRig.WristAngles(f, f, false);
        Assert.AreEqual(0f, a.x, 1e-3f);
        Assert.AreEqual(0f, a.y, 1e-3f);
    }

    [Test]
    public void WristAngles_FlexionTowardPalm_IsPositive_BothSides()
    {
        var f = Quaternion.identity;
        // sağ el avucu -x'e, sol el avucu +x'e bakar
        Assert.AreEqual(30f, ArmRig.WristAngles(f, Quaternion.Euler(0f, -30f, 0f), false).x, 0.5f);
        Assert.AreEqual(30f, ArmRig.WristAngles(f, Quaternion.Euler(0f, 30f, 0f), true).x, 0.5f);
        // başparmak (+y) tarafına sapma pozitif
        Assert.AreEqual(20f, ArmRig.WristAngles(f, Quaternion.Euler(-20f, 0f, 0f), false).y, 0.5f);
    }

    [Test]
    public void ClampHand_BringsFoldedWristInsideLimits_AndKeepsGoodPoseUntouched()
    {
        var f = Quaternion.identity;
        var c = ArmRig.ClampHand(f, Quaternion.Euler(0f, -140f, 0f), false);
        Assert.IsTrue(ArmRig.WithinLimits(ArmRig.WristAngles(f, c, false)));

        var ok = Quaternion.Euler(10f, -20f, 0f);
        Assert.Less(Quaternion.Angle(ok, ArmRig.ClampHand(f, ok, false)), 0.01f);
    }

    static ArmRig.Input In(Vector3 hp, Quaternion hr, bool free = false) =>
        new ArmRig.Input { shoulder = S, pole = Pole, upHint = Vector3.back, handPos = hp, handRot = hr, free = free ? 1f : 0f };

    [Test]
    public void Bake_PicksElbowThatStraightensWrist()
    {
        // Varsayılan dirsekle bilek ~55° yana bükülüyor; dirsek dönerek bunu azaltmalı
        Vector3 w = new Vector3(0.15f, -0.35f, 0.35f);
        Vector3 e0 = ArmRig.Elbow(S, w, Pole, 0f, out _);
        Vector3 fd = (w - e0).normalized;
        Vector3 side = Vector3.Cross(fd, Vector3.up).normalized;
        Quaternion hand = Quaternion.LookRotation((fd + side * 1.4f).normalized, Vector3.up);
        Vector3 hp = w - hand * ArmRig.WristLocal(false);
        float before = Vector3.Angle(hand * Vector3.forward, fd);

        var inp = new ArmRig.Input[10];
        for (int i = 0; i < inp.Length; i++) inp[i] = In(hp, hand);
        var o = ArmRig.Bake(inp, false);
        float after = Vector3.Angle(o[5].hand * Vector3.forward, o[5].fore * Vector3.forward);
        // ±70° dirsek dönüşünde ulaşılabilecek en düşük bükülme (kaba tarama)
        float best = float.MaxValue;
        for (float a = -70f; a <= 70f; a += 1f)
        {
            Vector3 e = ArmRig.Elbow(S, w, Pole, a, out _);
            best = Mathf.Min(best, Vector3.Angle(ArmRig.Fore((w - e).normalized, hand) * Vector3.forward, hand * Vector3.forward));
        }
        Assert.Less(after, before - 10f);
        Assert.Less(after, best + 3f);
        Assert.AreEqual(ArmRig.UpperLen, (o[5].elbow - o[5].shoulder).magnitude, 1e-3f);
    }

    [Test]
    public void Bake_FreeHandIsClamped_HeldHandKeepsItsPose()
    {
        Vector3 w = new Vector3(0.05f, -0.45f, 0.30f);
        Vector3 e0 = ArmRig.Elbow(S, w, Pole, 0f, out _);
        Quaternion hand = Quaternion.LookRotation(-(w - e0).normalized, Vector3.up);   // geriye katlanmış
        Vector3 hp = w - hand * ArmRig.WristLocal(false);
        var a = ArmRig.Bake(new[] { In(hp, hand, true) }, false)[0];
        var b = ArmRig.Bake(new[] { In(hp, hand, false) }, false)[0];
        Assert.IsTrue(ArmRig.WithinLimits(ArmRig.WristAngles(a.fore, a.hand, false)));
        Assert.AreEqual(0f, (a.handPos + a.hand * ArmRig.WristLocal(false) - w).magnitude, 1e-3f, "bilek yerinde kalmalı");
        Assert.Less(Quaternion.Angle(b.hand, hand), 0.01f);
        Assert.Less((b.handPos - hp).magnitude, 1e-4f);
    }

    [Test]
    public void Bake_ElbowSwivelIsRateLimited_WhenBestSideJumps()
    {
        // El bir karede bileği öbür yana büken poza atlar: en iyi dirsek açısı -70'ten +70'e sıçrar.
        // Dirsek bir karede karşı tarafa takla atmamalı, kare başına en fazla 5° dönmeli.
        Vector3 w = new Vector3(0.15f, -0.35f, 0.35f);
        Vector3 fd = (w - ArmRig.Elbow(S, w, Pole, 0f, out _)).normalized;
        Vector3 side = Vector3.Cross(fd, Vector3.up).normalized;
        int n = 40;
        var inp = new ArmRig.Input[n];
        for (int i = 0; i < n; i++)
        {
            var hr = Quaternion.LookRotation((fd + side * (i < 20 ? 1.4f : -1.4f)).normalized, Vector3.up);
            inp[i] = In(w - hr * ArmRig.WristLocal(false), hr);
        }
        var o = ArmRig.Bake(inp, false);
        for (int i = 1; i < n; i++)
            Assert.LessOrEqual(Mathf.Abs(o[i].swivel - o[i - 1].swivel), 5.01f, "dirsek tek karede savruldu: " + i);
    }

    [Test]
    public void Bake_ArmPassingThroughPoleDirection_ElbowDoesNotJump()
    {
        // Sarkan kol: omuz-bilek ekseni pole yönünün tam içinden geçer (dirsek yönü tanımsızlaşır)
        Vector3 pn = Pole.normalized;
        Vector3 axis = Vector3.Cross(pn, Vector3.up).normalized;
        int n = 60;
        var inp = new ArmRig.Input[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 w = Quaternion.AngleAxis(-30f + 60f * i / (n - 1), axis) * pn * 0.45f;
            var hr = Quaternion.LookRotation(w.normalized, Vector3.back);
            inp[i] = In(w - hr * ArmRig.WristLocal(false), hr);
        }
        var o = ArmRig.Bake(inp, false);
        for (int i = 1; i < n; i++)
        {
            Assert.Less((o[i].elbow - o[i - 1].elbow).magnitude, 0.03f, "dirsek sıçradı: " + i);
            Assert.Less(Quaternion.Angle(o[i - 1].upper, o[i].upper), 15f, "üst kol takla: " + i);
        }
    }

    [Test]
    public void Bake_NoFlips_AlongSweep()
    {
        int n = 120;
        var inp = new ArmRig.Input[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)(n - 1) * 300f;
            Vector3 w = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -0.25f, 0.45f);
            var hr = Quaternion.LookRotation(w.normalized, Vector3.up);
            inp[i] = In(w - hr * ArmRig.WristLocal(false), hr);
        }
        var o = ArmRig.Bake(inp, false);
        for (int i = 1; i < n; i++)
        {
            Assert.Less(Quaternion.Angle(o[i - 1].upper, o[i].upper), 20f, "üst kol takla: " + i);
            Assert.Less(Quaternion.Angle(o[i - 1].fore, o[i].fore), 20f, "ön kol takla: " + i);
        }
    }
}
