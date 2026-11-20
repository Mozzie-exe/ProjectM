using UnityEngine;
using UnityEngine.Events;

// Alınabilir eşya (anahtar, kürek...). İstenirse bir şart sağlanana kadar alınamaz (korkuluğun cebi gibi).
public class PickupItem : MonoBehaviour, IInteractable
{
    public string itemId = ItemIds.KeyShed;
    public string displayName = "Kulübe anahtarı";
    public string prompt = "Al";
    [TextArea] public string pickupMessage = "";

    [Header("Şart (opsiyonel)")]
    [Tooltip("Bu eşya/ipucu bulunmadan alınamaz.")]
    public string requiredId = "";
    public string gatedPrompt = "İncele";
    [TextArea] public string gatedMessage = "";

    [Header("Alınınca")]
    public GameObject hideObject;      // boşsa bu obje gizlenir
    public AudioClip pickupSound;
    public UnityEvent onPickedUp;

    bool taken;

    bool Allowed => Progress.Has(requiredId);

    public string GetPrompt()
    {
        if (taken) return null;
        return Allowed ? prompt + ": " + displayName : gatedPrompt;
    }

    public void Interact(Transform interactor)
    {
        if (taken) return;
        if (!Allowed)
        {
            HudMessage.Show(gatedMessage);
            return;
        }

        taken = true;
        if (PlayerInventory.Instance != null) PlayerInventory.Instance.Add(itemId, displayName);
        HudMessage.Show(string.IsNullOrEmpty(pickupMessage) ? displayName + " alındı." : pickupMessage);
        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        onPickedUp?.Invoke();
        (hideObject != null ? hideObject : gameObject).SetActive(false);
    }
}
