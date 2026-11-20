using UnityEngine;

// İleri-geri kayan çekmece. E'ye basınca açılır, tekrar basınca kapanır.
// Kurulum: çekmece mesh objesine eklenir (üzerinde bir Collider olmalı).
// openOffset: tamamen açıkken çekmecenin kapalı konumuna göre ne kadar kaydığı (parent'ın yerel uzayında).
[DisallowMultipleComponent]
public class Drawer : MonoBehaviour, IInteractable
{
    [Tooltip("Tam açıkken kayma miktarı ve yönü (parent'ın yerel uzayında). Sıfırsa modelin ön yüzüne doğru otomatik hesaplanır.")]
    public Vector3 openOffset = Vector3.zero;
    public float smoothTime = 0.22f; // açılma/kapanma yumuşaklığı (saniye)

    public bool startOpen = false;
    public bool locked = false;

    [Header("Yazılar")]
    public string openText = "Çekmeceyi Aç";
    public string closeText = "Çekmeceyi Kapat";
    public string lockedText = "Kilitli";

    [Header("Ses (opsiyonel)")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip lockedSound;

    Vector3 closedLocalPos;
    float current;   // 0 = kapalı, 1 = açık
    float target;
    float velocity;
    AudioSource audioSource;

    public bool IsOpen => target > 0.5f;

    void Awake()
    {
        closedLocalPos = transform.localPosition;

        if (openOffset == Vector3.zero)
        {
            // Blender modellerinde ön yüz yerel -Y yönündedir
            Vector3 worldOffset = -transform.up * 0.4f;
            openOffset = transform.parent != null ? transform.parent.InverseTransformVector(worldOffset) : worldOffset;
        }

        if (startOpen)
        {
            target = current = 1f;
            Apply();
        }

        if (openSound != null || closeSound != null || lockedSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            GameAudio.Apply3D(audioSource, 1.5f, 15f);
            audioSource.outputAudioMixerGroup = GameAudio.Group(AudioChannel.SFX);
        }
    }

    void Update()
    {
        if (Mathf.Abs(current - target) < 0.001f && Mathf.Abs(velocity) < 0.001f) return;

        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime);
        Apply();
    }

    void Apply()
    {
        transform.localPosition = closedLocalPos + openOffset * current;
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
            // Kapılarla aynı kilit sistemi (anahtar vb.)
            bool unlocked = false;
            foreach (var l in GetComponents<IDoorLock>())
                if (l.TryUnlock(interactor)) { unlocked = true; break; }

            if (!unlocked)
            {
                Play(lockedSound);
                return;
            }
            locked = false;
        }

        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        target = 1f;
        Play(openSound);
    }

    public void Close()
    {
        if (!IsOpen) return;
        target = 0f;
        Play(closeSound);
    }

    public void SetLocked(bool value) => locked = value;

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 basePos = Application.isPlaying && transform.parent != null
            ? transform.parent.TransformPoint(closedLocalPos)
            : transform.position;
        Vector3 off = transform.parent != null ? transform.parent.TransformVector(openOffset) : openOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(basePos, basePos + off);
        Gizmos.DrawWireSphere(basePos + off, 0.05f);
    }
}
