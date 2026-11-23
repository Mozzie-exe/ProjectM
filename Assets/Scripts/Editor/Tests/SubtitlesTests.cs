using NUnit.Framework;

public class SubtitlesTests
{
    static readonly SubtitleLine[] Lines =
    {
        new SubtitleLine(3f, 5f, "Wayne. Vardın mı?"),
        new SubtitleLine(6f, 8f, "Tabelayı yeni geçtim."),
    };

    [Test]
    public void IndexAt_NoneBeforeFirstLine() => Assert.AreEqual(-1, Subtitles.IndexAt(Lines, 2.9f));

    [Test]
    public void IndexAt_InsideLine() => Assert.AreEqual(0, Subtitles.IndexAt(Lines, 4f));

    [Test]
    public void IndexAt_NoneBetweenLines() => Assert.AreEqual(-1, Subtitles.IndexAt(Lines, 5.5f));

    [Test]
    public void IndexAt_SecondLine() => Assert.AreEqual(1, Subtitles.IndexAt(Lines, 6f));

    [Test]
    public void IndexAt_NoneAfterEnd() => Assert.AreEqual(-1, Subtitles.IndexAt(Lines, 8f));

    [Test]
    public void IndexAt_NullOrEmpty()
    {
        Assert.AreEqual(-1, Subtitles.IndexAt(null, 4f));
        Assert.AreEqual(-1, Subtitles.IndexAt(new SubtitleLine[0], 4f));
    }

    [Test]
    public void Duck_ZeroFarFromLines() => Assert.AreEqual(0f, Subtitles.Duck(Lines, 1f, 0.3f, 0.6f), 1e-4f);

    [Test]
    public void Duck_FullInsideLine() => Assert.AreEqual(1f, Subtitles.Duck(Lines, 4f, 0.3f, 0.6f), 1e-4f);

    [Test]
    public void Duck_RampsBeforeStart()
    {
        float d = Subtitles.Duck(Lines, 3f - 0.15f, 0.3f, 0.6f);
        Assert.Greater(d, 0f);
        Assert.Less(d, 1f);
    }

    [Test]
    public void Duck_ReleasesAfterEnd()
    {
        float d = Subtitles.Duck(Lines, 5.3f, 0.3f, 0.6f);
        Assert.Greater(d, 0f);
        Assert.Less(d, 1f);
        Assert.AreEqual(0f, Subtitles.Duck(Lines, 5.65f, 0.3f, 0.6f), 1e-4f);
    }

    [Test]
    public void Duck_NullIsZero() => Assert.AreEqual(0f, Subtitles.Duck(null, 4f, 0.3f, 0.6f), 1e-4f);

    [Test]
    public void Alpha_FadesInAndOut()
    {
        var l = Lines[0];
        Assert.AreEqual(0f, Subtitles.Alpha(l, 3f), 1e-4f);
        Assert.AreEqual(1f, Subtitles.Alpha(l, 4f), 1e-4f);
        Assert.AreEqual(1f, Subtitles.Alpha(l, 3f + Subtitles.Fade), 1e-4f);
        Assert.Less(Subtitles.Alpha(l, 5f - Subtitles.Fade * 0.5f), 1f);
        Assert.AreEqual(0f, Subtitles.Alpha(l, 5f), 1e-4f);
    }
}
