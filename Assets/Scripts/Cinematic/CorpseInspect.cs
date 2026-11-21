using UnityEngine;

// Örtülü ceset: kapı bakışından sonra "[E] İncele"; E ile oyun sonu sinematiği başlar.
// Builder (Tools > End Cinematic > Build) örtüye ekler ve EndCinematic'e bağlar.
public class CorpseInspect : MonoBehaviour, IInteractable
{
    public EndCinematic ending;

    bool Ready => ending && ending.DoorLookDone && !ending.Started;

    public string GetPrompt() => Ready ? "İncele" : "";

    // PlayerInteractor yazı boşken de Interact çağırır: hazır değilse bir şey olmaz
    public void Interact(Transform interactor)
    {
        if (Ready) ending.BeginMain();
    }
}
