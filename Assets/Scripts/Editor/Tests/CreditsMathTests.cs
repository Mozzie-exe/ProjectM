using NUnit.Framework;

public class CreditsMathTests
{
    [Test]
    public void Parse_KindsByPrefix()
    {
        var c = CreditsMath.Parse("## Oyun\n# Level tasarımı\nAyşe\n\nSon");
        Assert.AreEqual(5, c.Lines.Count);
        Assert.AreEqual(CreditsKind.Title, c.Lines[0].kind);
        Assert.AreEqual("Oyun", c.Lines[0].text);
        Assert.AreEqual(CreditsKind.Heading, c.Lines[1].kind);
        Assert.AreEqual("Level tasarımı", c.Lines[1].text);
        Assert.AreEqual(CreditsKind.Name, c.Lines[2].kind);
        Assert.AreEqual(CreditsKind.Gap, c.Lines[3].kind);
        Assert.AreEqual(CreditsKind.Name, c.Lines[4].kind);
    }

    [Test]
    public void Parse_CaseLinesGoToCard_KeepEmptyCaseLine()
    {
        var c = CreditsMath.Parse("> VAKA DOSYASI\n>\n> Durum: KAYIP\n## Oyun");
        CollectionAssert.AreEqual(new[] { "VAKA DOSYASI", "", "Durum: KAYIP" }, c.Card);
        Assert.AreEqual(1, c.Lines.Count);
    }

    [Test]
    public void Parse_IgnoresCommentsAndCarriageReturns()
    {
        var c = CreditsMath.Parse("// yorum\r\n# Ses\r\nPixabay\r\n");
        Assert.AreEqual(2, c.Lines.Count);
        Assert.AreEqual("Ses", c.Lines[0].text);
        Assert.AreEqual("Pixabay", c.Lines[1].text);
    }

    [Test]
    public void Parse_TrailingAndRepeatedBlankLinesCollapse()
    {
        var c = CreditsMath.Parse("\n\nA\n\n\n\nB\n\n");
        Assert.AreEqual(3, c.Lines.Count);   // A, boşluk, B
        Assert.AreEqual(CreditsKind.Gap, c.Lines[1].kind);
    }

    [Test]
    public void Parse_NullIsEmpty()
    {
        var c = CreditsMath.Parse(null);
        Assert.AreEqual(0, c.Lines.Count);
        Assert.AreEqual(0, c.Card.Count);
    }

    [Test]
    public void ScrollDuration_FillsMusicMinusCardHoldAndFade()
    {
        Assert.AreEqual(40f, CreditsMath.ScrollDuration(57.6f, 10.6f, 4.5f, 2.5f, 20f), 1e-4f);
    }

    [Test]
    public void ScrollDuration_NeverBelowMinimum()
    {
        Assert.AreEqual(20f, CreditsMath.ScrollDuration(15f, 10f, 4f, 2f, 20f), 1e-4f);
    }

    [Test]
    public void TypedChars_ClampsToLength()
    {
        Assert.AreEqual(0, CreditsMath.TypedChars(-1f, 25f, 10));
        Assert.AreEqual(5, CreditsMath.TypedChars(0.2f, 25f, 10));
        Assert.AreEqual(10, CreditsMath.TypedChars(3f, 25f, 10));
    }

    [Test]
    public void ScrollOffset_StartsBelowScreen_EndsWithLastLineCentered()
    {
        // içerik 1000 birim, ekran 1080: başta içeriğin tepesi ekranın altında, sonda son satır ortada
        Assert.AreEqual(0f, CreditsMath.ScrollOffset(0f, 40f, 1000f, 1080f), 1e-3f);
        Assert.AreEqual(1000f + 540f, CreditsMath.ScrollOffset(40f, 40f, 1000f, 1080f), 1e-3f);
        Assert.AreEqual(1000f + 540f, CreditsMath.ScrollOffset(60f, 40f, 1000f, 1080f), 1e-3f);
        Assert.AreEqual(770f, CreditsMath.ScrollOffset(20f, 40f, 1000f, 1080f), 1e-3f);
    }
}
