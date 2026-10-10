using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    CharacterController controller;
    Vector3 velocity;
    bool isGrounded;

    public Transform ground;
    public float distance = 0.4f;

    public float speed;
    public float sprintSpeed; // Koşma hızı için eklendi
    public float JumpHeight;
    public float gravity;

    public LayerMask mask;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        #region movement

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : speed;

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        controller.Move(move * currentSpeed * Time.deltaTime);
        #endregion

        #region jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y += Mathf.Sqrt(JumpHeight * -3.0f * gravity);
        }
        #endregion

        #region gravity

        isGrounded = Physics.CheckSphere(ground.position, distance, mask);
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = 0f;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        #endregion
    }
}