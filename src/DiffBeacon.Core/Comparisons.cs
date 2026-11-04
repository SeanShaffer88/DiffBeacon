using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        if (left.Length + (long)right.Length > MaxCharacters) throw new InvalidDataException("Text editing limit exceeded. Use Binary mode for a bounded full-file summary.");
        static int CountLines(string value) { int count = 1; for (int i = 0; i < value.Length; i++) { if (value[i] == '\r') { count++; if (i + 1 < value.Length && value[i + 1] == '\n') i++; } else if (value[i] == '\n') count++; } return count; }
        if (CountLines(left) + (long)CountLines(right) > MaxLines) throw new InvalidDataException("Line count exceeds the editing budget. Use Binary mode.");
        var a = Lines(left).Select((s, i) => (Raw: s, Line: i + 1)).Where(x => !options.IgnoreBlankLines || !string.IsNullOrWhiteSpace(x.Raw)).ToArray();
        var b = Lines(right).Select((s, i) => (Raw: s, Line: i + 1)).Where(x => !options.IgnoreBlankLines || !string.IsNullOrWhiteSpace(x.Raw)).ToArray();
        if (a.Length + (long)b.Length > MaxLines) throw new InvalidDataException("Line count exceeds the editing budget. Use Binary mode.");
        string Normalize(string s) { if (options.IgnoreWhitespace) s = string.Concat(s.Where(c => !char.IsWhiteSpace(c))); return options.IgnoreCase ? s.ToUpperInvariant() : s; }
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
            for (int x = a.Length - 1; x >= 0; x--) { token.ThrowIfCancellationRequested(); for (int y = b.Length - 1; y >= 0; y--) lcs[x, y] = an[x] == bn[y] ? lcs[x + 1, y + 1] + 1 : Math.Max(lcs[x + 1, y], lcs[x, y + 1]); }
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
        return new(rows, fallback);
    }
}

public sealed record FileStamp(long Length, DateTime ModifiedUtc, string Hash)
{
    public static FileStamp Capture(string path) { using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read); return new(stream.Length, File.GetLastWriteTimeUtc(path), Convert.ToHexString(SHA256.HashData(stream))); }
    public bool Matches(string path) => File.Exists(path) && this == Capture(path);
}
public sealed record TextDocument(string Path, string Text, Encoding Encoding, byte[] Preamble, FileStamp Stamp)
{
    public static TextDocument Load(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 16 * 1024 * 1024) throw new InvalidDataException("Large File: editing limited to 16 MiB per file. Use Binary mode for full-file streaming comparison.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        var before = File.GetLastWriteTimeUtc(path);
        Encoding encoding = new UTF8Encoding(false, true); int skip = 0;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); skip = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) { encoding = new UTF32Encoding(true, true, true); skip = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) { skip = 3; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) { encoding = new UnicodeEncoding(false, true, true); skip = 2; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) { encoding = new UnicodeEncoding(true, true, true); skip = 2; }
        string text;
        try { text = encoding.GetString(bytes, skip, bytes.Length - skip); } catch (DecoderFallbackException ex) { throw new InvalidDataException("Unsupported or invalid text encoding. Use Binary Compare.", ex); }
        if (text.Contains('\0')) throw new InvalidDataException("Binary NUL detected. Use Binary Compare.");
        if (text.Length > TextComparison.MaxCharacters) throw new InvalidDataException("Decoded text exceeds the editor budget. Use Binary mode.");
        return new(path, text, encoding, bytes[..skip], new(bytes.LongLength, before, Convert.ToHexString(SHA256.HashData(bytes))));
    }
    public static void SavePane(ref TextDocument? document, ref bool dirty, string text, string destination, TextDocument? other)
    {
        // The saving pane's original fingerprint wins when both panes reference the destination.
        var source = new[] { document, other }.FirstOrDefault(d => d != null && string.Equals(d.Path, destination, StringComparison.OrdinalIgnoreCase));
        var expected = source?.Stamp ?? (File.Exists(destination) ? FileStamp.Capture(destination) : null);
        byte[] bytes = document?.Encode(text) ?? new UTF8Encoding(false, true).GetBytes(text);
        SafeFiles.Write(destination, bytes, expected, File.Exists(destination));
        document = Load(destination);
        dirty = false;
    }
    public byte[] Encode(string text)
    {
        // RichTextBox normalizes line endings. Preserve the loaded file's newline convention;
        // refuse mixed conventions instead of silently rewriting an ambiguous input.
        var endings = System.Text.RegularExpressions.Regex.Matches(Text, "\\r\\n|\\r|\\n").Select(m => m.Value).Distinct().ToArray();
        if (endings.Length > 1) throw new InvalidDataException("Mixed line endings: save is disabled to avoid silent normalization. Copy content into a new snippet to explicitly convert.");
        string normalized = string.Join(endings.FirstOrDefault() ?? Environment.NewLine, TextComparison.Lines(text));
        return Preamble.Concat(Encoding.GetBytes(normalized)).ToArray();
    }
}

public static class SafeFiles
{
    public static void Write(string destination, byte[] bytes, FileStamp? expected, bool allowOverwrite)
    {
        destination = System.IO.Path.GetFullPath(destination); RejectReparseAncestors(destination);
        bool exists = File.Exists(destination);
        if (exists && !allowOverwrite) throw new IOException("Destination exists. Confirm overwrite first.");
        if (expected != null && !expected.Matches(destination)) throw new IOException("File changed externally. Save to a new path.");
        var temp = destination + ".diffbeacon-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { output.Write(bytes); output.Flush(true); }
            if (expected != null && !expected.Matches(destination)) throw new IOException("Destination changed during save.");
            if (exists) File.Replace(temp, destination, destination + ".diffbeacon-backup-" + Guid.NewGuid().ToString("N"));
            else File.Move(temp, destination, false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static void RejectReparseAncestors(string path)
    {
        for (string? p = System.IO.Path.GetFullPath(path); p != null; p = System.IO.Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point paths are not permitted for writes: " + p);
    }
}

public sealed record ByteSample(long Offset, byte? Left, byte? Right);
public sealed record BinaryResult(long LeftLength, long RightLength, long DifferentBytes, long? FirstDifference, IReadOnlyList<ByteSample> Samples)
{
    public string Summary => $"{DifferentBytes:N0} Different Bytes · Left {LeftLength:N0} Bytes · Right {RightLength:N0} Bytes · First: {(FirstDifference.HasValue ? "0x" + FirstDifference.Value.ToString("X") : "None")} · Samples {Samples.Count:N0} (Maximum 512)";
}
public static class BinaryComparison
{
    public static BinaryResult Compare(string left, string right, CancellationToken token = default)
    {
        using var a = File.Open(left, FileMode.Open, FileAccess.Read, FileShare.Read); using var b = File.Open(right, FileMode.Open, FileAccess.Read, FileShare.Read);
        var am = File.GetLastWriteTimeUtc(left); var bm = File.GetLastWriteTimeUtc(right);
        long al = a.Length, bl = b.Length, offset = 0, differences = 0; long? first = null;
        byte[] ab = new byte[65536], bb = new byte[65536]; var samples = new List<ByteSample>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int ac = ReadBlock(a, ab), bc = ReadBlock(b, bb); if (ac == 0 && bc == 0) break;
            for (int i = 0; i < Math.Max(ac, bc); i++) if (i >= ac || i >= bc || ab[i] != bb[i]) { differences++; first ??= offset + i; if (samples.Count < 512) samples.Add(new(offset + i, i < ac ? ab[i] : null, i < bc ? bb[i] : null)); }
            offset += Math.Max(ac, bc);
        }
        if (a.Length != al || b.Length != bl || File.GetLastWriteTimeUtc(left) != am || File.GetLastWriteTimeUtc(right) != bm) throw new IOException("An input changed during comparison. Retry.");
        return new(al, bl, differences, first, samples);
    }
    static int ReadBlock(Stream input, byte[] buffer) { int total = 0, read; while (total < buffer.Length && (read = input.Read(buffer, total, buffer.Length - total)) != 0) total += read; return total; }
}

public sealed record FolderRow(string RelativePath, Change Status, long? LeftSize, long? RightSize, string Detail);
public sealed record FolderResult(IReadOnlyList<FolderRow> Rows, bool Complete)
{ public string Summary => $"{Rows.Count(r => r.Status != Change.Equal):N0} Differing Or Error Entries · {Rows.Count:N0} Files · Content Comparison · {(Complete ? "Complete" : "INCOMPLETE — Errors Or 50,000 File Limit")}"; }
public static class FolderComparison
{
    public const int MaxFiles = 50_000;
    public static FolderResult Compare(string left, string right, CancellationToken token = default)
    {
        var errors = new List<FolderRow>(); bool complete = true;
        Dictionary<string, string> Scan(string root)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(root));
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point roots are not supported.");
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested(); string folder = pending.Pop();
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
                    {
                        token.ThrowIfCancellationRequested(); var attr = File.GetAttributes(entry);
                        if ((attr & FileAttributes.ReparsePoint) != 0) { complete = false; if (errors.Count < 1000) errors.Add(new(System.IO.Path.GetRelativePath(root, entry), Change.Error, null, null, "Reparse Point Skipped")); continue; }
                        if ((attr & FileAttributes.Directory) != 0) pending.Push(entry);
                        else { if (found.Count >= MaxFiles) { complete = false; return found; } found.Add(System.IO.Path.GetRelativePath(root, entry), entry); }
                        if (pending.Count > MaxFiles) { complete = false; return found; }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { complete = false; if (errors.Count < 1000) errors.Add(new(System.IO.Path.GetRelativePath(root, folder), Change.Error, null, null, ex.Message)); }
            }
            return found;
        }
        var a = Scan(left); var b = Scan(right); var rows = new List<FolderRow>();
        foreach (string name in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested(); if (rows.Count >= MaxFiles) { complete = false; break; }
            bool hasA = a.TryGetValue(name, out var ap), hasB = b.TryGetValue(name, out var bp);
            try { rows.Add(new(name, !hasA ? Change.RightOnly : !hasB ? Change.LeftOnly : BinaryComparison.Compare(ap!, bp!, token).DifferentBytes == 0 ? Change.Equal : Change.Changed, hasA ? new FileInfo(ap!).Length : null, hasB ? new FileInfo(bp!).Length : null, "")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { complete = false; rows.Add(new(name, Change.Error, null, null, ex.Message)); }
        }
        rows.AddRange(errors); return new(rows, complete);
    }
}

public static class DelimitedText
{
    public static IReadOnlyList<string[]> Parse(string input, char delimiter = ',')
    {
        if (delimiter is '\r' or '\n' or '"' or '\0') throw new ArgumentException("Invalid separator.");
        if (input.Length > TextComparison.MaxCharacters) throw new InvalidDataException("Table input exceeds memory budget.");
        var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false, afterQuote = false; int cells = 0;
        void Field() { row.Add(field.ToString()); field.Clear(); afterQuote = false; if (++cells > 200_000) throw new InvalidDataException("Table exceeds 200,000 cells."); }
        void Row() { Field(); rows.Add(row.ToArray()); row.Clear(); if (rows.Count > 50_000) throw new InvalidDataException("Table exceeds 50,000 rows."); }
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (quoted) { if (c == '"') { if (i + 1 < input.Length && input[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; afterQuote = true; } } else field.Append(c); }
            else if (c == delimiter) Field();
            else if (c is '\r' or '\n') { if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++; Row(); }
            else if (c == '"') { if (field.Length > 0 || afterQuote) throw new InvalidDataException($"Unexpected quote at character {i + 1}."); quoted = true; }
            else { if (afterQuote) throw new InvalidDataException($"Unexpected character after closing quote at character {i + 1}."); field.Append(c); }
        }
        if (quoted) throw new InvalidDataException("Unclosed quoted field at end of table.");
        if (field.Length > 0 || row.Count > 0 || afterQuote || input.Length == 0 || input[^1] == delimiter) Row();
        return rows;
    }
}

public sealed record Session(int Version, string Name, string Mode, string Left, string Right, CompareOptions Options)
{
    public void Save(string path) => SafeFiles.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, new JsonSerializerOptions { WriteIndented = true }), File.Exists(path) ? FileStamp.Capture(path) : null, true);
    public static Session Load(string path)
    {
        using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 64 * 1024) throw new InvalidDataException("Session exceeds metadata limit.");
        byte[] bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
        var value = JsonSerializer.Deserialize<Session>(bytes) ?? throw new InvalidDataException("Empty session.");
        if (value.Version != 1 || value.Name is null || value.Left is null || value.Right is null || value.Options is null || value.Mode is not ("Text" or "Folders" or "Binary" or "Tables")) throw new InvalidDataException("Unsupported or invalid session.");
        return value;
    }
}
public static class Reports
{
    public static string Html(string title, string summary, IEnumerable<IEnumerable<string?>> rows)
    {
        string E(string? x) => WebUtility.HtmlEncode(x ?? "");
        return "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>" + E(title) + "</title><style>body{font:14px Segoe UI;margin:32px;color:#183238}table{border-collapse:collapse;width:100%}td{padding:6px;border:1px solid #ccd8da;white-space:pre-wrap}tr:nth-child(even){background:#f3f7f7}</style><h1>" + E(title) + "</h1><p>" + E(summary) + "</p><table>" + string.Concat(rows.Select(r => "<tr>" + string.Concat(r.Select(c => "<td>" + E(c) + "</td>")) + "</tr>")) + "</table></html>";
    }
}
