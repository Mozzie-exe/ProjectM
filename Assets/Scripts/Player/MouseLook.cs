using UnityEngine;

public class MouseLook : MonoBehaviour
{

    [Range(50, 500)]

    public float sens;

    public Transform playerBody;

    float xRot = 0f;

    private void Update()
    {
        float rotX = Input.GetAxisRaw("Mouse X") * sens * Time.deltaTime;
        float rotY = Input.GetAxisRaw("Mouse Y") * sens * Time.deltaTime;

        xRot -= rotY;

        xRot = Mathf.Clamp(xRot, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);

        playerBody.Rotate(Vector3.up * rotX);
    }
}
