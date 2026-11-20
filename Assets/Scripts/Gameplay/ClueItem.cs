using UnityEngine;

// İncelenebilir ipucu: ekranda büyük gösterir ve not defterine ekler. İstenirse alınıp sahneden kalkar.
public class ClueItem : MonoBehaviour, IInteractable
{
    public Notebook.Entry entry = new Notebook.Entry();
    public string prompt = "İncele";
    public bool removeAfterReading = false;

    [Header("Şart (opsiyonel)")]
    public string requiredId = "";
    [TextArea] public string gatedMessage = "";

    [Tooltip("Okununca envantere de bir şey eklensin mi (ör. harita)")]
    public string grantItemId = "";

    public AudioClip paperSound;

    public string GetPrompt() => prompt;

    public void Interact(Transform interactor)
    {
        if (!Progress.Has(requiredId))
        {
            HudMessage.Show(gatedMessage);
            return;
        }

        if (paperSound != null) AudioSource.PlayClipAtPoint(paperSound, transform.position);
        if (Notebook.Instance != null) Notebook.Instance.Add(entry);
        if (!string.IsNullOrEmpty(grantItemId) && PlayerInventory.Instance != null)
            PlayerInventory.Instance.Add(grantItemId, entry.title);

        ReadableView.Show(entry);
        if (removeAfterReading) gameObject.SetActive(false);
    }
}
