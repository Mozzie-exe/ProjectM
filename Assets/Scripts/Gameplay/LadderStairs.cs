using System.Collections;
using UnityEngine;

// Dar ve dik bodrum merdiveni: oyuncu E ile iner/çıkar, yol noktaları boyunca yavaşça taşınır.
// Merdiven karakter kapsülü için fazla dik ve kapağın kenarı başa çarptığı için yürüyerek inilmez.
// Bu obje: kapak deliğini örten ince bir collider (üstte) veya sahanlıktaki bir collider (altta).
public class LadderStairs : MonoBehaviour, IInteractable
{
    [Tooltip("Üstten alta sırayla. Oyuncunun AYAK noktaları.")]
    public Transform[] waypoints;
    public bool goesDown = true;           // true: üstteki giriş (iner), false: alttaki giriş (çıkar)
    public Door hatch;                     // kapak kapalıyken kullanılamaz
    public float secondsPerMeter = 0.55f;
    public float bobAmount = 0.05f;
    public AudioClip[] creaks;             // her basamakta rastgele gıcırtı (opsiyonel)
    public float creakEvery = 0.55f;

    public string downPrompt = "Bodruma in";
    public string upPrompt = "Yukarı çık";

    bool busy;

    public string GetPrompt()
    {
        if (busy) return null;
        if (hatch != null && !hatch.IsOpen) return null;
        return goesDown ? downPrompt : upPrompt;
    }

    public void Interact(Transform interactor)
    {
        if (busy || waypoints == null || waypoints.Length < 2) return;
        if (hatch != null && !hatch.IsOpen) return;

        var cc = interactor.GetComponentInParent<CharacterController>();
        if (cc == null) return;
        StartCoroutine(Climb(cc, interactor));
    }

    IEnumerator Climb(CharacterController cc, Transform cam)
    {
        busy = true;
        var player = cc.transform;
        var move = player.GetComponent<PlayerMovement>();
        var battery = cam.GetComponentInChildren<FlashlightBattery>();
        bool moveWas = move != null && move.enabled;
        if (move != null) move.enabled = false;
        cc.enabled = false;
        if (goesDown && battery != null) battery.SetForcedFlicker(true);

        // Ayak -> kapsül merkezi farkı
        float footToCenter = cc.height * 0.5f * player.lossyScale.y - cc.center.y * player.lossyScale.y;

        int n = waypoints.Length;
        Vector3 start = player.position;
        Vector3 first = goesDown ? waypoints[0].position : waypoints[n - 1].position;

        // Önce girişe hizalan
        yield return MoveTo(player, start, first + Vector3.up * footToCenter, 0.35f, 0f);

        float creakTimer = 0f;
        for (int k = 0; k < n - 1; k++)
        {
            int a = goesDown ? k : n - 1 - k;
            int b = goesDown ? k + 1 : n - 2 - k;
            Vector3 pa = waypoints[a].position + Vector3.up * footToCenter;
            Vector3 pb = waypoints[b].position + Vector3.up * footToCenter;
            float dur = Mathf.Max(0.2f, Vector3.Distance(pa, pb) * secondsPerMeter);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                player.position = Vector3.Lerp(pa, pb, e) + Vector3.up * Mathf.Sin(t * Mathf.PI * 2f) * bobAmount;

                creakTimer += Time.deltaTime;
                if (creakTimer >= creakEvery && creaks != null && creaks.Length > 0)
                {
                    creakTimer = 0f;
                    var clip = creaks[Random.Range(0, creaks.Length)];
                    if (clip != null) AudioSource.PlayClipAtPoint(clip, player.position, 0.8f);
                }
                yield return null;
            }
        }

        cc.enabled = true;
        if (move != null) move.enabled = moveWas;
        if (goesDown && battery != null) battery.SetForcedFlicker(false);
        busy = false;
    }

    static IEnumerator MoveTo(Transform p, Vector3 from, Vector3 to, float dur, float bob)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            p.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Length - 1; i++)
            if (waypoints[i] && waypoints[i + 1]) Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
    }
}
