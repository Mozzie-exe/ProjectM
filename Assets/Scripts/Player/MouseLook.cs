using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Range(0.1f, 10f)]
    public float sens = 2f;

    public Transform playerBody;

    float xRot = 0f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Skip look input while the cursor is unlocked (e.g. after pressing Escape)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // No Time.deltaTime: mouse delta is already per-frame
        float rotX = Input.GetAxisRaw("Mouse X") * sens;
        float rotY = Input.GetAxisRaw("Mouse Y") * sens;

        xRot -= rotY;
        xRot = Mathf.Clamp(xRot, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);

        playerBody.Rotate(Vector3.up * rotX);
    }
}