using UnityEngine;

// "E basılı tut" ile yapılan etkileşimler (kazı gibi). PlayerInteractor tuş basılı kaldıkça süreyi sayar.
public interface IHoldInteractable
{
    float HoldDuration { get; }

    // Şu an basılı tutma başlatılabilir mi? (ör. kürek var mı)
    bool CanHold(Transform interactor);

    // 0..1 ilerleme; bırakılınca 0 ile çağrılır
    void OnHoldProgress(float t);

    void OnHoldComplete(Transform interactor);
}
