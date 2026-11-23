using NUnit.Framework;

public class UiFactoryFontTests
{
    [Test]
    public void DefaultFont_IsCourierPrime()
    {
        Assert.AreEqual("CourierPrime-Regular", UiFactory.DefaultFont.name);
    }

    [Test]
    public void HandFont_IsCaveat()
    {
        Assert.AreEqual("Caveat-Variable", UiFactory.HandFont.name);
    }

    [Test]
    public void Fonts_HaveTurkishLetters()
    {
        foreach (var f in new[] { UiFactory.DefaultFont, UiFactory.HandFont })
            foreach (char c in "ğĞşŞıİçÇöÖüÜ")
                Assert.IsTrue(f.HasCharacter(c), f.name + " '" + c + "' içermiyor");
    }
}
