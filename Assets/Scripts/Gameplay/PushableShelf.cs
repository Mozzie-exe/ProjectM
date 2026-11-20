using System.Collections;
using UnityEngine;

// Kilerdeki raf: kat planı bulununca itilebilir, altındaki kapak ortaya çıkar.
public class PushableShelf : MonoBehaviour, IInteractable
{
    public string requiredClue = ItemIds.ClueFloorPlan;
    public Vector3 pushOffset = new Vector3(0f, 0f, 3f);   // yerel ölçüde ne kadar kayacak
    public float pushTime = 2f;

    public string lockedPrompt = "İncele";
    [TextArea] public string lockedMessage = "Ağır, eski bir raf. Kavanozlarla dolu.";
    public string pushPrompt = "Rafı İt";
    [TextArea] public string pushedMessage = "Rafın altında tahta bir kapak var.";
    public AudioClip scrapeSound;

    bool pushed;
    bool moving;

    public string GetPrompt()
    {
        if (pushed || moving) return null;
        return Progress.Has(requiredClue) ? pushPrompt : lockedPrompt;
    }

    public void Interact(Transform interactor)
    {
        if (pushed || moving) return;
        if (!Progress.Has(requiredClue))
        {
            HudMessage.Show(lockedMessage);
            return;
        }
        StartCoroutine(Push());
    }

    IEnumerator Push()
    {
        moving = true;
        if (scrapeSound != null) GameAudio.PlayAt(scrapeSound, transform.position, 1f, AudioChannel.SFX, 30f, 2f);
        Vector3 start = transform.localPosition;
        Vector3 end = start + pushOffset;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pushTime;
            // ağır raf: takılarak kayar
            float e = Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * 40f) * 0.004f * (1f - t);
            transform.localPosition = Vector3.LerpUnclamped(start, end, e);
            yield return null;
        }
        transform.localPosition = end;
        moving = false;
        pushed = true;
        HudMessage.Show(pushedMessage);
    }
}
