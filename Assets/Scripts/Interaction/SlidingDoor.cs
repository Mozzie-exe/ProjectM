using UnityEngine;

// Ray üstünde kayan ağır kapı (ahırın aralık duran sürgülü kapısı). E'ye basınca bir kere açılır, kapanmaz.
// Aynı objede WindRattle varsa kayma yönünü ondan alır ve açılınca onu durdurur.
public class SlidingDoor : MonoBehaviour, IInteractable
{
    public Vector3 localAxis = Vector3.zero;   // ebeveyn uzayında kayma yönü; sıfırsa WindRattle'dan ya da sağdan
    public float distance = BarnScareMath.DoorSlideDistance; // işaret yönü seçer
    public float duration = BarnScareMath.DoorSlideDuration;
    public string openText = "Kapıyı Aç";
    public AudioClip slideSound;

    public bool IsOpen { get; private set; }

    Vector3 startPos;
    float elapsed = -1f;

    void Awake()
    {
        if (localAxis == Vector3.zero)
        {
            var rattle = GetComponent<WindRattle>();
            localAxis = rattle != null ? rattle.localAxis : Vector3.right;
        }
    }

    public string GetPrompt() => IsOpen ? "" : openText;

    public void Interact(Transform interactor)
    {
        if (IsOpen) return;
        IsOpen = true;

        var rattle = GetComponent<WindRattle>();
        if (rattle != null) rattle.enabled = false;

        startPos = transform.localPosition;
        elapsed = 0f;

        if (slideSound != null)
            GameAudio.PlayAt(slideSound, transform.position, 1f, AudioChannel.SFX, 30f, 2f, false);
    }

    void Update()
    {
        if (elapsed < 0f) return;
        elapsed += Time.deltaTime;
        transform.localPosition = startPos + localAxis.normalized * BarnScareMath.SlideOffset(elapsed, duration, distance);
        if (elapsed >= duration) elapsed = -1f;
    }
}
