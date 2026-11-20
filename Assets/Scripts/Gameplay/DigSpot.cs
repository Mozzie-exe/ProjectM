using UnityEngine;

// Tarladaki kazı yeri: kürek + harita varken "E basılı tut" ile 3 saniye kazılır, gömülü kutu çıkar.
public class DigSpot : MonoBehaviour, IInteractable, IHoldInteractable
{
    public string requiredTool = ItemIds.Shovel;
    public string requiredClue = ItemIds.ClueMap;
    public float holdDuration = 3f;

    [Header("Görseller")]
    public GameObject mound;        // kazılmadan önceki kabarık toprak
    public GameObject hole;         // kazıdan sonra çukur
    public GameObject buriedObject; // ortaya çıkan kutu (içinde PickupItem)

    [Header("Yazılar")]
    public string noToolMessage = "Toprak burada biraz kabarık. Elimle kazamam, bir küreğe ihtiyacım var.";
    public string noClueMessage = "Rastgele kazmanın anlamı yok. Nereyi kazacağımı bilmem lazım.";
    public string doneMessage = "Küreğin ucu metale çarptı...";

    [Header("Ses (opsiyonel)")]
    public AudioClip digLoop;
    public AudioClip footstepsNearby;   // kazı ortasında yakında ayak sesi
    public float footstepsAt = 0.55f;

    bool dug;
    bool footstepsPlayed;
    AudioSource src;

    public float HoldDuration => holdDuration;

    void Awake()
    {
        if (hole != null) hole.SetActive(false);
        if (buriedObject != null) buriedObject.SetActive(false);
    }

    bool Ready => Progress.Has(requiredTool) && Progress.Has(requiredClue);

    public string GetPrompt()
    {
        if (dug) return null;
        return Ready ? "Kaz (basılı tut)" : "İncele";
    }

    public void Interact(Transform interactor)
    {
        if (dug || Ready) return; // hazırsa kazı basılı tutmayla olur
        HudMessage.Show(Progress.Has(requiredTool) ? noClueMessage : noToolMessage);
    }

    public bool CanHold(Transform interactor) => !dug && Ready;

    public void OnHoldProgress(float t)
    {
        if (digLoop != null)
        {
            if (src == null)
            {
                src = gameObject.AddComponent<AudioSource>();
                src.clip = digLoop;
                src.loop = true;
                src.spatialBlend = 1f;
            }
            if (t > 0f && !src.isPlaying) src.Play();
            if (t <= 0f && src.isPlaying) src.Stop();
        }

        if (!footstepsPlayed && t >= footstepsAt && footstepsNearby != null)
        {
            footstepsPlayed = true;
            var cam = Camera.main != null ? Camera.main.transform : transform;
            AudioSource.PlayClipAtPoint(footstepsNearby, cam.position - cam.forward * 6f);
        }
    }

    public void OnHoldComplete(Transform interactor)
    {
        dug = true;
        if (src != null) src.Stop();
        if (mound != null) mound.SetActive(false);
        if (hole != null) hole.SetActive(true);
        if (buriedObject != null) buriedObject.SetActive(true);
        HudMessage.Show(doneMessage);
    }
}
