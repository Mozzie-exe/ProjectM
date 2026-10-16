using UnityEngine;

public class HeadBob : MonoBehaviour
{
    public float walkBobSpeed;
    public float walkBobAmount;

    public float sprintBobSpeed;
    public float sprintBobAmount;

    public Stamina staminaController;

    private float defaultPosY;
    private float defaultPosX;
    private float timer;

    void Start()
    {
        defaultPosY = transform.localPosition.y;
        defaultPosX = transform.localPosition.x;

        if (staminaController == null)
            staminaController = GetComponentInParent<Stamina>();
    }

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        if (isMoving)
        {
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && (staminaController == null || staminaController.CanSprint);

            float speed = isSprinting ? sprintBobSpeed : walkBobSpeed;
            float amount = isSprinting ? sprintBobAmount : walkBobAmount;

            timer += Time.deltaTime * speed;

            float newY = defaultPosY + Mathf.Sin(timer) * amount;
            float newX = defaultPosX + Mathf.Cos(timer / 2f) * amount;

            transform.localPosition = new Vector3(newX, newY, transform.localPosition.z);
        }
        else
        {
            timer = 0f;
            Vector3 resetPos = new Vector3(defaultPosX, defaultPosY, transform.localPosition.z);
            transform.localPosition = Vector3.Lerp(transform.localPosition, resetPos, Time.deltaTime * 6f);
        }
    }
}