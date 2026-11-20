using System;
using System.Collections.Generic;
using UnityEngine;

// Bulunan ipuçlarının kaydı. Player objesine eklenir; NotebookUI bunu gösterir.
public class Notebook : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string id;
        public string title;
        public Texture2D image;
        public Texture2D backImage;
        [TextArea] public string text;     // notun okunur hali
        [TextArea] public string comment;  // karakterin kısa yorumu
    }

    public static Notebook Instance { get; private set; }

    readonly List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;
    public event Action<Entry> EntryAdded;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool Has(string id)
    {
        foreach (var e in entries)
            if (e.id == id) return true;
        return false;
    }

    // Yeni eklendiyse true döner
    public bool Add(Entry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id) || Has(entry.id)) return false;
        entries.Add(entry);
        EntryAdded?.Invoke(entry);
        return true;
    }
}
