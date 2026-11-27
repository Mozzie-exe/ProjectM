using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Range(0.1f, 10f)]
    public float sens = 2f;

    public Transform playerBody;

    float xRot = 0f;

    // Dikey bakış açısı. Kamerayı dışarıdan çeviren olaylar (ahır) bittiğinde bununla senkronlanır,
    // yoksa MouseLook açıldığında eski açıya sıçrar.
    public float Pitch
    {
        get => xRot;
        set => xRot = Mathf.Clamp(value, -90f, 90f);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Esc artık duraklatma menüsünü açar (PauseMenu); imleci o yönetir.
        // Pencere odağı kaybolup imleç serbest kalırsa tıklayınca geri kilitlenir.
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !PauseMenu.IsPaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Skip look input while the cursor is unlocked
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