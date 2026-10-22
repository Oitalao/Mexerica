namespace Filee.Engines.Models3D;

internal static class SpanSplit
{
    public static List<Range> SplitAny(this ReadOnlySpan<char> text, char first, char second)
    {
        var ranges = new List<Range>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
            if (text[i] == first || text[i] == second) { ranges.Add(start..i); start = i + 1; }
        ranges.Add(start..text.Length);
        return ranges;
    }

    public static List<Range> Split(this ReadOnlySpan<char> text, char separator)
    {
        var ranges = new List<Range>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
            if (text[i] == separator) { ranges.Add(start..i); start = i + 1; }
        ranges.Add(start..text.Length);
        return ranges;
    }
}
