using System.Collections.Generic;
using UnityEngine;

// Okuma ekranı / not defteri açıkken oyuncunun yürümesini, bakmasını ve etkileşimini durdurur.
// Birden fazla şey aynı anda kilitleyebilir; son kilit kalkınca eski hal geri gelir.
public static class PlayerControlLock
{
    static int count;
    static readonly List<Behaviour> locked = new List<Behaviour>();
    static readonly List<bool> wasEnabled = new List<bool>();

    public static bool IsLocked => count > 0;

    public static void Push()
    {
        count++;
        if (count > 1) return;

        locked.Clear();
        wasEnabled.Clear();
        Collect<PlayerMovement>();
        Collect<MouseLook>();
        Collect<PlayerInteractor>();
        Collect<HeadBob>();

        for (int i = 0; i < locked.Count; i++)
        {
            wasEnabled.Add(locked[i].enabled);
            locked[i].enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void Pop()
    {
        if (count == 0) return;
        count--;
        if (count > 0) return;

        for (int i = 0; i < locked.Count; i++)
            if (locked[i] != null) locked[i].enabled = wasEnabled[i];
        locked.Clear();
        wasEnabled.Clear();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    static void Collect<T>() where T : Behaviour
    {
        foreach (var b in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            locked.Add(b);
    }
}
