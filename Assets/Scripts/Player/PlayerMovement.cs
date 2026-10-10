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

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        if (staminaController == null)
        {
            staminaController = GetComponent<Stamina>();
        }

        if (staminaController == null)
        {
            
        }
    }

    private void Update()
    {
        #region movement
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        bool isMoving = move.sqrMagnitude > 0.01f;

     
        bool isSprinting = Input.GetKey(KeyCode.LeftShift) && isMoving && staminaController != null && staminaController.CanSprint;

        float currentSpeed = isSprinting ? sprintSpeed : speed;

        controller.Move(move * currentSpeed * Time.deltaTime);
        #endregion

        #region jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
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