using System.Security.Cryptography;
using System.Text;

namespace DiffBeacon.Core;

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

