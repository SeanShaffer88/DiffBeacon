using System.Text.Json;

namespace DiffBeacon.Core;

public sealed record Session(int Version, string Name, string Mode, string Left, string Right, CompareOptions Options, string FileFilter = "", string IgnoredDirectories = "", HighlightGranularity Granularity = HighlightGranularity.Line, bool SyncScrolling = true, bool WordWrap = false, string TableSeparator = ",")
{
    public void Save(string path)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(Validate(this), new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > 64 * 1024) throw new InvalidDataException("Session exceeds metadata limit.");
        SafeFiles.Write(path, bytes, File.Exists(path) ? FileStamp.Capture(path) : null, true);
    }
    static Session Validate(Session value)
    {
        if (value.Version is not (1 or 2 or 3) || value.Name is null || value.Left is null || value.Right is null || value.Options is null || value.Mode is not ("Text" or "Folders" or "Binary" or "Tables")) throw new InvalidDataException("Unsupported or invalid session.");
        value = value with { FileFilter = value.FileFilter ?? "", IgnoredDirectories = value.IgnoredDirectories ?? "" };
        if (!Enum.IsDefined(value.Granularity) || value.TableSeparator is null || value.TableSeparator.Length != 1 || value.TableSeparator[0] is '"' or '\r' or '\n' or '\0') throw new InvalidDataException("Invalid session display preferences or table separator.");
        if (value.Version < 3 && (value.Granularity != HighlightGranularity.Line || !value.SyncScrolling || value.WordWrap || value.TableSeparator != ",")) throw new InvalidDataException("Version 3 is required to preserve display preferences and table separator.");
        var filters = FolderFilters.Parse(value.FileFilter, value.IgnoredDirectories);
        if (value.Version == 1 && filters.IsFiltered) throw new InvalidDataException("Version 1 sessions cannot contain folder filters. Use version 2 to preserve this scope.");
        return value;
    }
    public static Session Load(string path)
    {
        using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > 64 * 1024) throw new InvalidDataException("Session exceeds metadata limit.");
        byte[] bytes = new byte[checked((int)input.Length)]; input.ReadExactly(bytes);
        int skip = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0;
        var value = JsonSerializer.Deserialize<Session>(bytes.AsSpan(skip)) ?? throw new InvalidDataException("Empty session.");
        return Validate(value);
    }
}
