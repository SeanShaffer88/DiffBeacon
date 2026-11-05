using System.Text;

namespace DiffBeacon.Core;

public enum Change { Equal, Changed, LeftOnly, RightOnly, Error }
public sealed record CompareOptions(bool IgnoreCase = false, bool IgnoreWhitespace = false, bool IgnoreBlankLines = false);
public sealed record DiffRow(int? LeftLine, int? RightLine, string? Left, string? Right, Change Status);
public sealed record TextResult(IReadOnlyList<DiffRow> Rows, bool PositionalFallback)
{
    public int Differences => Rows.Count(r => r.Status != Change.Equal);
    public string Summary => $"{Differences:N0} Differing Rows · {Rows.Count:N0} Rows" + (PositionalFallback ? " · Positional Fallback (Alignment Budget Exceeded)" : " · Aligned By Line");
}
public static class TextComparison
{
    public const int MaxCharacters = 4_000_000;
    public const int MaxLines = 100_000;
    public static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
    public static TextResult Compare(string left, string right, CompareOptions options, CancellationToken token = default, long maxCells = 2_000_000)
    {
        token.ThrowIfCancellationRequested();
        if (left.Length + (long)right.Length > MaxCharacters) throw new InvalidDataException("Text editing limit exceeded. Use Binary mode for a bounded full-file summary.");
        int CountLines(string value) { int count = 1; for (int i = 0; i < value.Length; i++) { if ((i & 255) == 0) token.ThrowIfCancellationRequested(); if (value[i] == '\r') { count++; if (i + 1 < value.Length && value[i + 1] == '\n') i++; } else if (value[i] == '\n') count++; } return count; }
        if (CountLines(left) + (long)CountLines(right) > MaxLines) throw new InvalidDataException("Line count exceeds the editing budget. Use Binary mode.");
        (string Raw, int Line)[] Prepare(string text)
        {
            var lines = Lines(text); var retained = new List<(string Raw, int Line)>();
            for (int i = 0; i < lines.Length; i++)
            {
                token.ThrowIfCancellationRequested(); bool blank = options.IgnoreBlankLines;
                if (blank) for (int j = 0; j < lines[i].Length; j++) { if ((j & 255) == 0) token.ThrowIfCancellationRequested(); if (!char.IsWhiteSpace(lines[i][j])) { blank = false; break; } }
                if (!blank) retained.Add((lines[i], i + 1));
            }
            return retained.ToArray();
        }
        var a = Prepare(left); var b = Prepare(right);
        if (a.Length + (long)b.Length > MaxLines) throw new InvalidDataException("Line count exceeds the editing budget. Use Binary mode.");
        string Normalize(string s) { var normalized = new StringBuilder(s.Length); for (int i = 0; i < s.Length; i++) { if ((i & 255) == 0) token.ThrowIfCancellationRequested(); if (!options.IgnoreWhitespace || !char.IsWhiteSpace(s[i])) normalized.Append(s[i]); } token.ThrowIfCancellationRequested(); return options.IgnoreCase ? normalized.ToString().ToUpperInvariant() : normalized.ToString(); }
        var an = a.Select(x => Normalize(x.Raw)).ToArray(); var bn = b.Select(x => Normalize(x.Raw)).ToArray();
        var rows = new List<DiffRow>();
        bool fallback = (a.Length + 1L) * (b.Length + 1L) > maxCells;
        if (fallback)
        {
            for (int k = 0; k < Math.Max(a.Length, b.Length); k++)
            { token.ThrowIfCancellationRequested(); rows.Add(new(k < a.Length ? a[k].Line : null, k < b.Length ? b[k].Line : null, k < a.Length ? a[k].Raw : null, k < b.Length ? b[k].Raw : null, k >= a.Length ? Change.RightOnly : k >= b.Length ? Change.LeftOnly : an[k] == bn[k] ? Change.Equal : Change.Changed)); }
        }
        else
        {
            var lcs = new int[a.Length + 1, b.Length + 1];
            for (int x = a.Length - 1; x >= 0; x--) { token.ThrowIfCancellationRequested(); for (int y = b.Length - 1; y >= 0; y--) { if ((y & 255) == 0) token.ThrowIfCancellationRequested(); lcs[x, y] = an[x] == bn[y] ? lcs[x + 1, y + 1] + 1 : Math.Max(lcs[x + 1, y], lcs[x, y + 1]); } }
            int i = 0, j = 0;
            while (i < a.Length || j < b.Length)
            {
                token.ThrowIfCancellationRequested();
                if (i < a.Length && j < b.Length && an[i] == bn[j]) { rows.Add(new(a[i].Line, b[j].Line, a[i].Raw, b[j].Raw, Change.Equal)); i++; j++; }
                else if (i < a.Length && j < b.Length && lcs[i + 1, j] == lcs[i, j + 1]) { rows.Add(new(a[i].Line, b[j].Line, a[i].Raw, b[j].Raw, Change.Changed)); i++; j++; }
                else if (i < a.Length && (j == b.Length || lcs[i + 1, j] >= lcs[i, j + 1])) { rows.Add(new(a[i].Line, null, a[i].Raw, null, Change.LeftOnly)); i++; }
                else { rows.Add(new(null, b[j].Line, null, b[j].Raw, Change.RightOnly)); j++; }
            }
        }
        token.ThrowIfCancellationRequested();
        return new(rows, fallback);
    }
}

