using UnityEngine;

// Kulübedeki yedek pil: feneri bir kez tam doldurur.
public class BatteryPickup : MonoBehaviour, IInteractable
{
    public string prompt = "Al: Yedek pil";

    public string GetPrompt() => prompt;

    public void Interact(Transform interactor)
    {
        var battery = interactor.GetComponentInChildren<FlashlightBattery>();
        if (battery == null) battery = Object.FindFirstObjectByType<FlashlightBattery>();
        if (battery != null) battery.Refill();
        else HudMessage.Show("Yedek pil alındı.");
        gameObject.SetActive(false);
    }
}
