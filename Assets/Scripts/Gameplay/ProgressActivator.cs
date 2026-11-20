using UnityEngine;

// Bir ipucu/eşya bulununca objeleri açar veya kapatır (ör. fotoğraftan sonra korkuluğun cebinde anahtar görünür).
public class ProgressActivator : MonoBehaviour
{
    public string requiredId = ItemIds.CluePhoto;
    public GameObject[] enableWhenMet;
    public GameObject[] disableWhenMet;

    bool applied;

    void Start() => Apply(Progress.Has(requiredId));

    void Update()
    {
        if (applied) return;
        if (Time.frameCount % 15 == 0 && Progress.Has(requiredId)) Apply(true);
    }

    void Apply(bool met)
    {
        foreach (var go in enableWhenMet) if (go != null) go.SetActive(met);
        foreach (var go in disableWhenMet) if (go != null) go.SetActive(!met);
        applied = met;
    }
}
