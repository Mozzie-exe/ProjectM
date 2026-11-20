using UnityEngine;

// Kilitli kapıyı doğru anahtar envanterdeyse otomatik açar. Door ile aynı objeye eklenir.
[RequireComponent(typeof(Door))]
public class KeyLock : MonoBehaviour, IDoorLock
{
    public string requiredKey = ItemIds.KeyShed;
    public bool consumeKey = false;

    [Header("Yazılar")]
    public string unlockMessage = "Anahtar uydu.";
    public string wrongKeyMessage = "Bu anahtar uymuyor.";
    [Tooltip("Hiç anahtar yokken gösterilir. Boşsa kapının lockedText'i kullanılır.")]
    public string noKeyMessage = "";

    [Header("Kilit açılınca")]
    public GameObject[] hideOnUnlock;  // asma kilit, zincir...
    public AudioClip unlockSound;

    public bool TryUnlock(Transform interactor)
    {
        var inv = PlayerInventory.Instance;
        if (inv != null && inv.Has(requiredKey))
        {
            if (consumeKey) inv.Remove(requiredKey);
            foreach (var go in hideOnUnlock)
                if (go != null) go.SetActive(false);
            if (unlockSound != null) GameAudio.PlayAt(unlockSound, transform.position, 1f, AudioChannel.SFX, 20f, 1.5f, false);
            HudMessage.Show(unlockMessage);
            return true;
        }

        if (inv != null && inv.HasAnyKey()) HudMessage.Show(wrongKeyMessage);
        else HudMessage.Show(string.IsNullOrEmpty(noKeyMessage) ? GetComponent<Door>().lockedText : noKeyMessage);
        return false;
    }
}
