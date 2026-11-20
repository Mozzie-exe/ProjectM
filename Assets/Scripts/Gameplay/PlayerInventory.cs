using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncunun taşıdığı anahtarlar ve araçlar. Player objesine eklenir.
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    readonly Dictionary<string, string> items = new Dictionary<string, string>(); // id -> görünen ad

    public event Action<string> ItemAdded;

    public IEnumerable<string> ItemIdsHeld => items.Keys;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Has(string id) => !string.IsNullOrEmpty(id) && items.ContainsKey(id);

    public void Add(string id, string displayName)
    {
        if (string.IsNullOrEmpty(id) || items.ContainsKey(id)) return;
        items[id] = string.IsNullOrEmpty(displayName) ? id : displayName;
        ItemAdded?.Invoke(id);
    }

    public bool Remove(string id) => items.Remove(id);

    public string DisplayName(string id) => items.TryGetValue(id, out var n) ? n : id;

    // Kilitli kapıda "bu anahtar uymuyor" diyebilmek için
    public bool HasAnyKey()
    {
        foreach (var id in items.Keys)
            if (id.StartsWith("key_")) return true;
        return false;
    }
}
