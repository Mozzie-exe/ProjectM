// Bir şartın sağlanıp sağlanmadığını tek yerden sorar: envanterde eşya mı var, not defterinde ipucu mu?
public static class Progress
{
    public static bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return true; // şart yoksa sağlanmış say
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.Has(id)) return true;
        if (Notebook.Instance != null && Notebook.Instance.Has(id)) return true;
        return false;
    }

    public static bool HasAll(string[] ids)
    {
        if (ids == null) return true;
        foreach (var id in ids)
            if (!Has(id)) return false;
        return true;
    }
}
