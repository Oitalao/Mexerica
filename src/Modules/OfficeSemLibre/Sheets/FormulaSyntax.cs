using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Filee.Engines.Office.Sheets;

internal static class FormulaSyntax
{
    const string Cell = @"\$?[A-Za-z]{1,3}\$?[1-9][0-9]*";
    const string Sheet = @"\$?(?:'(?:(?:'')|[^'])*'|[\p{L}_][\p{L}\p{N}_.]*)";
    static readonly Regex References = new(@"(?<![\w.])(?:(?<sheet>" + Sheet + @")!)?(?<a>" + Cell + @")(?::(?<b>" + Cell + @"))?(?![\w.(])");
    static readonly Regex OdsReferences = new(@"\[(?<reference>[^\[\]]+)\]");

    internal static bool CanExport(string formula, ISet<string> sheets, ISet<string> definedNames)
    {
        var supported = true;
        OutsideStrings(formula, part =>
        {
            var remaining = References.Replace(part, m =>
            {
                if (m.Groups["sheet"].Success && !sheets.Contains(UnquoteSheet(m.Groups["sheet"].Value)))
                    supported = false;
                return " ";
            });
            foreach (Match name in Regex.Matches(remaining, @"[\p{L}_\\][\p{L}\p{N}_.\\]*"))
                if (definedNames.Contains(name.Value)) supported = false;
            return part;
        });
        return supported;
    }

    static string UnquoteSheet(string name)
    {
        name = name.TrimStart('$');
        return name.StartsWith('\'') ? name[1..^1].Replace("''", "'", StringComparison.Ordinal) : name;
    }

    internal static string RenameSheets(string formula, IReadOnlyDictionary<string, string> names) => OutsideStrings(formula,
        part => References.Replace(part, m =>
        {
            if (!m.Groups["sheet"].Success || !names.TryGetValue(UnquoteSheet(m.Groups["sheet"].Value), out var name)) return m.Value;
            return "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'!" + m.Groups["a"].Value +
                (m.Groups["b"].Success ? ":" + m.Groups["b"].Value : "");
        }));

    // Structured/external references and array literals need more than A1 syntax translation; keep their cache.
    internal static string? ToOds(string formula)
    {
        var supported = true;
        var converted = OutsideStrings(formula, part =>
        {
            if (part.IndexOfAny(['[', ']', '{', '}']) >= 0 ||
                Regex.IsMatch(part, @"(?<![\w.])(?:\$?[A-Za-z]{1,3}:\$?[A-Za-z]{1,3}|\$?[1-9][0-9]*:\$?[1-9][0-9]*)(?![\w.])"))
                supported = false;
            return ChangeSeparator(References.Replace(part, m =>
            {
                var sheet = m.Groups["sheet"].Success ? m.Groups["sheet"].Value + "." : ".";
                return "[" + sheet + m.Groups["a"].Value +
                    (m.Groups["b"].Success ? ":." + m.Groups["b"].Value : "") + "]";
            }), ',', ';');
        });
        return supported ? "of:=" + converted : null;
    }

    internal static string? FromOds(string formula)
    {
        if (formula.StartsWith("of:=", StringComparison.Ordinal)) formula = formula[4..];
        else if (formula.StartsWith("oooc:=", StringComparison.Ordinal)) formula = formula[6..];
        else if (formula.StartsWith('=')) formula = formula[1..];
        else return null;
        var supported = true;
        var converted = OutsideStrings(formula, part => ChangeSeparator(OdsReferences.Replace(part, m =>
        {
            // OpenFormula local references and quoted sheet names use '.' where Excel uses '!'.
            var reference = Regex.Match(m.Groups["reference"].Value, @"^(?<sheet>" + Sheet + @")?\.(?<a>" + Cell + @")(?::(?<sheet2>" + Sheet + @")?\.(?<b>" + Cell + @"))?$");
            if (!reference.Success) { supported = false; return m.Value; }
            string SheetName(string group) => reference.Groups[group].Success ? reference.Groups[group].Value.TrimStart('$') + "!" : "";
            return SheetName("sheet") + reference.Groups["a"].Value +
                (reference.Groups["b"].Success ? ":" + SheetName("sheet2") + reference.Groups["b"].Value : "");
        }), ';', ','));
        return supported ? converted : null;
    }

    internal static string Shift(string formula, int rows, int columns) => OutsideStrings(formula,
        part => References.Replace(part, m =>
        {
            string ShiftCell(string reference)
            {
                var parsed = Regex.Match(reference, @"^(?<column>\$?[A-Za-z]+)(?<row>\$?[0-9]+)$");
                var col = parsed.Groups["column"].Value;
                var row = parsed.Groups["row"].Value;
                if (!CellRange.TryParseCell(reference.Replace("$", "", StringComparison.Ordinal), out var r, out var c)) return reference;
                if (!row.StartsWith('$')) r += rows;
                if (!col.StartsWith('$')) c += columns;
                if (r < 0 || c < 0 || r >= 1048576 || c >= 16384) return "#REF!";
                return (col.StartsWith('$') ? "$" : "") + XlsxWriter.ColumnName(c) +
                    (row.StartsWith('$') ? "$" : "") + (r + 1).ToString(CultureInfo.InvariantCulture);
            }
            return (m.Groups["sheet"].Success ? m.Groups["sheet"].Value + "!" : "") + ShiftCell(m.Groups["a"].Value) +
                (m.Groups["b"].Success ? ":" + ShiftCell(m.Groups["b"].Value) : "");
        }));

    static string ChangeSeparator(string expression, char from, char to)
    {
        var result = expression.ToCharArray();
        char quote = '\0';
        for (var i = 0; i < result.Length; i++)
        {
            if (quote != '\0')
            {
                if (result[i] != quote) continue;
                if (i + 1 < result.Length && result[i + 1] == quote) { i++; continue; }
                quote = '\0';
            }
            else if (result[i] is '\'' or '"') quote = result[i];
            else if (result[i] == from) result[i] = to;
        }
        return new string(result);
    }

    static string OutsideStrings(string formula, Func<string, string> transform)
    {
        var result = new StringBuilder();
        var start = 0;
        for (var i = 0; i < formula.Length; i++)
        {
            // Quoted sheet names can contain double quotes; they are not string literals.
            if (formula[i] == '\'')
            {
                for (i++; i < formula.Length; i++)
                    if (formula[i] == '\'')
                    {
                        if (i + 1 < formula.Length && formula[i + 1] == '\'') { i++; continue; }
                        break;
                    }
                continue;
            }
            if (formula[i] != '"') continue;
            result.Append(transform(formula[start..i]));
            var quoted = i++;
            for (; i < formula.Length; i++)
            {
                if (formula[i] != '"') continue;
                if (i + 1 < formula.Length && formula[i + 1] == '"') { i++; continue; }
                break;
            }
            i = Math.Min(i, formula.Length - 1);
            result.Append(formula[quoted..(i + 1)]);
            start = i + 1;
        }
        return result.Append(transform(formula[start..])).ToString();
    }
}
