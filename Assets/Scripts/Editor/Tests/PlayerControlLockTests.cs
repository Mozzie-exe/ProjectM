using NUnit.Framework;

public class PlayerControlLockTests
{
    [Test]
    public void ResetState_AfterPushWithoutPop_IsUnlocked()
    {
        // Domain reload kapalı: oyun sonu kilidi (hiç Pop edilmez) sonraki Play'e taşınmamalı
        PlayerControlLock.Push();
        PlayerControlLock.ResetState();
        Assert.IsFalse(PlayerControlLock.IsLocked);
    }
}
