using System.Collections.Generic;
using UnityEngine;

public enum CreditsKind { Title, Heading, Name, Gap }

public struct CreditsLine
{
    public CreditsKind kind;
    public string text;
    public CreditsLine(CreditsKind kind, string text) { this.kind = kind; this.text = text; }
}

public class CreditsContent
{
    public readonly List<string> Card = new List<string>();          // başta daktiloyla yazılan vaka dosyası
    public readonly List<CreditsLine> Lines = new List<CreditsLine>(); // kayan yazılar
}

// Credits metnini ayrıştırır ve zamanlamayı hesaplar (Resources/Credits/Credits.txt).
//   "> "  vaka dosyası kartı    "## " büyük başlık    "# " bölüm başlığı
//   düz satır: isim             boş satır: boşluk     "//": yorum
public static class CreditsMath
{
    public static CreditsContent Parse(string src)
    {
        var c = new CreditsContent();
        if (string.IsNullOrEmpty(src)) return c;
        foreach (var raw in src.Replace("\r", "").Split('\n'))
        {
            string s = raw.Trim();
            if (s.StartsWith("//")) continue;
            if (s.StartsWith(">")) { c.Card.Add(s.Substring(1).Trim()); continue; }
            if (s.Length == 0)
            {
                // baştaki, sondaki ve art arda boşluklar tek boşluk
                if (c.Lines.Count > 0 && c.Lines[c.Lines.Count - 1].kind != CreditsKind.Gap)
                    c.Lines.Add(new CreditsLine(CreditsKind.Gap, ""));
                continue;
            }
            if (s.StartsWith("##")) c.Lines.Add(new CreditsLine(CreditsKind.Title, s.Substring(2).Trim()));
            else if (s.StartsWith("#")) c.Lines.Add(new CreditsLine(CreditsKind.Heading, s.Substring(1).Trim()));
            else c.Lines.Add(new CreditsLine(CreditsKind.Name, s));
        }
        if (c.Lines.Count > 0 && c.Lines[c.Lines.Count - 1].kind == CreditsKind.Gap) c.Lines.RemoveAt(c.Lines.Count - 1);
        return c;
    }

    // Kayma süresi: müzik, kart + sondaki bekleme + kararma çıkınca kalan; çok kısa müzikte en az minDuration
    public static float ScrollDuration(float musicLength, float cardTime, float endHold, float fadeOut, float minDuration) =>
        Mathf.Max(minDuration, musicLength - cardTime - endHold - fadeOut);

    public static int TypedChars(float t, float charsPerSecond, int length) =>
        Mathf.Clamp(Mathf.FloorToInt(t * charsPerSecond + 1e-4f), 0, length);

    // İçeriğin yukarı kayma miktarı: 0'da tepesi ekranın altında, sonda son satırı ekranın ortasında
    public static float ScrollOffset(float t, float duration, float contentHeight, float viewHeight) =>
        Mathf.Clamp01(t / Mathf.Max(0.01f, duration)) * (contentHeight + viewHeight * 0.5f);
}
