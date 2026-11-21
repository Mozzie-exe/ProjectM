using NUnit.Framework;

public class NotebookUITests
{
    [Test]
    public void CanToggle_ClosedAndLockedByOther_ReturnsFalse()
    {
        // Oyun sonu sinematiği / kapı bakışı kilitliyken not defteri açılmasın
        Assert.IsFalse(NotebookUI.CanToggle(false, true));
    }

    [Test]
    public void CanToggle_ClosedAndUnlocked_ReturnsTrue()
    {
        Assert.IsTrue(NotebookUI.CanToggle(false, false));
    }

    [Test]
    public void CanToggle_OpenWhileLocked_ReturnsTrue()
    {
        // Açıkken kilit not defterinin kendisi; kapatılabilmeli
        Assert.IsTrue(NotebookUI.CanToggle(true, true));
    }
}
