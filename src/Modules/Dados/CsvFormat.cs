// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores). Origem: Filee.Engines/Office/Sheets/CsvFormat.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Filee.Engines.Data;

internal static class CsvFormat
{
    public static void WriteRows(IEnumerable<IEnumerable<string>> rows, string path, char delimiter = ',')
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.Append(string.Join(delimiter, row.Select(value => Quote(value, delimiter)))).Append("\r\n");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
            }
            else if (ch == '"' && field.Length == 0) quoted = true;
            else if (ch == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new();
            }
            else field.Append(ch);
        }
        if (quoted) throw new InvalidDataException("CSV com aspas sem fechamento.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    public static char DetectDelimiter(string text)
    {
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        var quoted = false;
        foreach (var ch in text.Take(4096))
        {
            if (ch == '"') quoted = !quoted;
            else if (!quoted && counts.ContainsKey(ch)) counts[ch]++;
        }
        return counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key == ',' ? 0 : 1).First().Key;
    }

    static string Quote(string value, char delimiter) =>
        value.IndexOfAny([delimiter, '"', '\r', '\n']) >= 0 || value.StartsWith(' ') || value.EndsWith(' ')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
}
