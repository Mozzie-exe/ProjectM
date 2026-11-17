using UnityEngine;

// Menteşe etrafında dönen kapı. E'ye basınca oyuncudan uzağa doğru itilerek açılır.
// Kurulum: bu obje kapının ortasında (zeminde) durur, yerel X = kapı genişliği yönü,
// yerel Z = duvarın normali. "hinge" kapının bir kenarındaki menteşe noktasıdır.
public class Door : MonoBehaviour, IInteractable
{
    public Transform hinge;
    [Range(30f, 130f)] public float openAngle = 95f;
    public float smoothTime = 0.35f; // açılma/kapanma yumuşaklığı (saniye)

    public bool startOpen = false;
    public bool locked = false;

    [Header("Yazılar")]
    public string openText = "Kapıyı Aç";
    public string closeText = "Kapıyı Kapat";
    public string lockedText = "Kilitli";

    [Header("Ses (opsiyonel)")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    Quaternion closedRotation;
    float currentAngle;
    float targetAngle;
    float angleVelocity;
    AudioSource audioSource;

    public bool IsOpen => Mathf.Abs(targetAngle) > 0.01f;

    void Awake()
    {
        if (hinge == null) hinge = transform;
        closedRotation = hinge.localRotation;

        if (startOpen)
        {
            targetAngle = currentAngle = openAngle;
            ApplyRotation();
        }

        if (openSound != null || closeSound != null || lockedSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 25f;
        }
    }

    void Update()
    {
        if (Mathf.Abs(currentAngle - targetAngle) < 0.01f && Mathf.Abs(angleVelocity) < 0.01f) return;

        currentAngle = Mathf.SmoothDamp(currentAngle, targetAngle, ref angleVelocity, smoothTime);
        ApplyRotation();
    }

    void ApplyRotation()
    {
        hinge.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
    }

    public string GetPrompt()
    {
        if (locked) return lockedText;
        return IsOpen ? closeText : openText;
    }

    public void Interact(Transform interactor)
    {
        if (locked)
        {
            Play(lockedSound);
            return;
        }

        if (IsOpen)
        {
            targetAngle = 0f;
            Play(closeSound);
        }
        else
        {
            // Oyuncu kapının hangi tarafındaysa kapı ters tarafa itilir.
            // +açı kapının serbest ucunu yerel -Z yönüne döndürür.
            float side = transform.InverseTransformPoint(interactor.position).z;
            targetAngle = side >= 0f ? openAngle : -openAngle;
            Play(openSound);
        }
    }

    public void SetLocked(bool value) => locked = value;

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }
}
