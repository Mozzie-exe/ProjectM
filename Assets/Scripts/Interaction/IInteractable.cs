using UnityEngine;

// E ile etkileşime girilebilen her şey bunu uygular (kapı, sandık, ışık düğmesi...)
public interface IInteractable
{
    // Ekranda "[E] ..." yanında gösterilecek yazı. Boş dönerse yazı gösterilmez.
    string GetPrompt();

    // E'ye basılınca çağrılır. interactor: oyuncunun kamerası
    void Interact(Transform interactor);
}
