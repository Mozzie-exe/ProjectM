using UnityEngine;

// Kilitli bir Door'a eklenen kilit kuralları bunu uygular (anahtar, ipucu...).
public interface IDoorLock
{
    // Kilit açıldıysa true döner. Açılmadıysa oyuncuya mesajı kendisi gösterir.
    bool TryUnlock(Transform interactor);
}
