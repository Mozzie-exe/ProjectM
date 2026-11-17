using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    CharacterController controller;
    Vector3 velocity;
    bool isGrounded;

    public Transform ground;
    public float distance;

    public float speed;
    public float sprintSpeed;
    public float JumpHeight;
    public float gravity;

    public LayerMask mask;

    public Stamina staminaController;

    [Header("Eğilme")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    public bool toggleCrouch = false;      // true: bas-bırak ile aç/kapa, false: basılı tut
    public float crouchCameraDrop = 0.8f;  // eğilince kamera ne kadar insin
    public float crouchSpeed = 3.5f;       // eğilirken yürüme hızı
    public float crouchTransitionSpeed = 10f;
    public Transform cameraTransform;      // boşsa otomatik bulunur

    public bool IsGrounded => isGrounded;
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }

    float standCamY;
    float crouchAmount; // 0 = ayakta, 1 = tam eğilmiş
    bool crouchToggled;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        if (cameraTransform != null)
            standCamY = cameraTransform.localPosition.y;

        if (staminaController == null)
        {
            staminaController = GetComponent<Stamina>();
        }
    }

    private void Update()
    {
        #region crouch
        // Not: CharacterController'ın boyutuna dokunmuyoruz (zıplama / ışınlanma yapıyordu).
        // Eğilme sadece kamerayı indirir ve hızı düşürür.
        if (toggleCrouch)
        {
            if (Input.GetKeyDown(crouchKey)) crouchToggled = !crouchToggled;
            IsCrouching = crouchToggled;
        }
        else
        {
            IsCrouching = Input.GetKey(crouchKey);
        }

        crouchAmount = Mathf.MoveTowards(crouchAmount, IsCrouching ? 1f : 0f, Time.deltaTime * crouchTransitionSpeed * 0.5f);

        if (cameraTransform != null)
        {
            float smooth = Mathf.SmoothStep(0f, 1f, crouchAmount);
            Vector3 p = cameraTransform.localPosition;
            p.y = standCamY - crouchCameraDrop * smooth;
            cameraTransform.localPosition = p;
        }
        #endregion

        #region movement
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        bool isMoving = move.sqrMagnitude > 0.01f;

        bool isSprinting = !IsCrouching && Input.GetKey(KeyCode.LeftShift) && isMoving && staminaController != null && staminaController.CanSprint;

        IsMoving = isMoving;
        IsSprinting = isSprinting;

        float currentSpeed = IsCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : speed);

        controller.Move(move * currentSpeed * Time.deltaTime);
        #endregion

        #region jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && !IsCrouching)
        {
            velocity.y = Mathf.Sqrt(JumpHeight * 2f * Mathf.Abs(gravity));
        }
        #endregion

        #region gravity
        if (ground != null)
        {
            isGrounded = Physics.CheckSphere(ground.position, distance, mask);
        }

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        #endregion
    }
}
