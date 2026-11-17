using UnityEngine;

// Kamera tutucusuna (CamHolder) ya da kameraya eklenir.
// Yürürken kafayı yukarı-aşağı ve hafif sağa-sola sallar (8 çizer gibi).
public class HeadBob : MonoBehaviour
{
    [Header("Yürüme")]
    public float walkFrequency = 10f;   // adım hızı
    public float walkAmplitude = 0.05f; // dikey sallanma miktarı

    [Header("Koşma")]
    public float sprintFrequency = 15f;
    public float sprintAmplitude = 0.09f;

    [Header("Eğilme")]
    [Range(0f, 1f)] public float crouchAmplitudeMultiplier = 0.5f;
    [Range(0.1f, 1f)] public float crouchFrequencyMultiplier = 0.7f;

    [Header("Genel")]
    [Range(0f, 1f)] public float horizontalRatio = 0.5f; // yanal sallanma / dikey sallanma
    public float blendSpeed = 8f;  // başlama/durma yumuşaklığı
    public float returnSpeed = 6f; // dururken merkeze dönüş hızı

    public PlayerMovement player;

    Vector3 startLocalPos;
    float timer;
    float currentAmplitude;
    float currentFrequency;
    float lastGroundedTime;

    void Start()
    {
        startLocalPos = transform.localPosition;

        if (player == null)
            player = GetComponentInParent<PlayerMovement>();
    }

    void Update()
    {
        // Zemin kontrolü yürürken bir anlık kesilirse sallanma durup başlamasın
        if (player == null || player.IsGrounded) lastGroundedTime = Time.time;
        bool groundedRecently = Time.time - lastGroundedTime < 0.2f;

        bool bobbing = player != null ? (player.IsMoving && groundedRecently) : IsMovingFallback();
        bool sprinting = player != null && player.IsSprinting;

        bool crouching = player != null && player.IsCrouching;

        float targetAmp = bobbing ? (sprinting ? sprintAmplitude : walkAmplitude) : 0f;
        float targetFreq = sprinting ? sprintFrequency : walkFrequency;

        if (crouching)
        {
            targetAmp *= crouchAmplitudeMultiplier;
            targetFreq *= crouchFrequencyMultiplier;
        }

        currentAmplitude = Mathf.Lerp(currentAmplitude, targetAmp, Time.deltaTime * blendSpeed);
        currentFrequency = Mathf.Lerp(currentFrequency, targetFreq, Time.deltaTime * blendSpeed);

        if (bobbing)
        {
            timer += Time.deltaTime * currentFrequency;

            // Dikey: her adımda bir iniş-çıkış. Yanal: iki adımda bir sağ-sol.
            float y = Mathf.Sin(timer) * currentAmplitude;
            float x = Mathf.Cos(timer * 0.5f) * currentAmplitude * horizontalRatio;

            transform.localPosition = startLocalPos + new Vector3(x, y, 0f);
        }
        else
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, startLocalPos, Time.deltaTime * returnSpeed);

            // Tekrar yürümeye başlayınca sıçrama olmasın diye zamanlayıcıyı nötr noktaya getir
            if ((transform.localPosition - startLocalPos).sqrMagnitude < 0.00001f)
                timer = 0f;
        }
    }

    bool IsMovingFallback()
    {
        return Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
    }
}
