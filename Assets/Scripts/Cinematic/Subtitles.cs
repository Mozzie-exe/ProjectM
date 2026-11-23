using UnityEngine;

[System.Serializable]
public struct SubtitleLine
{
    public float start, end;
    [TextArea] public string text;
    public SubtitleLine(float start, float end, string text) { this.start = start; this.end = end; this.text = text; }
}

public static class Subtitles
{
    // Satır başında ve sonunda yumuşak açılış/kapanış (sn)
    public const float Fade = 0.15f;

    // t anında gösterilecek satır (start <= t < end), yoksa -1
    public static int IndexAt(SubtitleLine[] lines, float t)
    {
        if (lines == null) return -1;
        for (int i = 0; i < lines.Length; i++)
            if (t >= lines[i].start && t < lines[i].end) return i;
        return -1;
    }

    public static float Alpha(SubtitleLine line, float t)
    {
        float a = Mathf.Min((t - line.start) / Fade, (line.end - t) / Fade);
        return Mathf.Clamp01(a);
    }
}
