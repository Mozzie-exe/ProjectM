using UnityEngine;

// Oyuncu bu alanın içine girince kapı arkasından yavaşça kapanır (kulübe). Bir kez çalışır.
public class DoorCloseBehind : MonoBehaviour
{
    public Door door;
    public Vector3 areaSize = new Vector3(4f, 4f, 5f);   // yerel ölçüde alan
    public Vector3 areaCenter = Vector3.zero;
    public float delay = 1.2f;
    public float closeTime = 2.5f;                       // kapanma yumuşaklığı (saniye)
    public AudioClip creakSound;

    Transform player;
    float insideTimer = -1f;
    bool done;

    void Start()
    {
        if (PlayerInventory.Instance != null) player = PlayerInventory.Instance.transform;
    }

    void Update()
    {
        if (done || door == null) return;
        if (player == null)
        {
            if (PlayerInventory.Instance == null) return;
            player = PlayerInventory.Instance.transform;
        }

        Vector3 p = transform.InverseTransformPoint(player.position) - areaCenter;
        bool inside = Mathf.Abs(p.x) < areaSize.x * 0.5f && Mathf.Abs(p.y) < areaSize.y * 0.5f && Mathf.Abs(p.z) < areaSize.z * 0.5f;

        if (!inside) { insideTimer = -1f; return; }
        if (!door.IsOpen) return;

        if (insideTimer < 0f) insideTimer = 0f;
        insideTimer += Time.deltaTime;
        if (insideTimer < delay) return;

        done = true;
        door.Close(closeTime);
        if (creakSound != null) AudioSource.PlayClipAtPoint(creakSound, door.transform.position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawCube(areaCenter, areaSize);
    }
}
