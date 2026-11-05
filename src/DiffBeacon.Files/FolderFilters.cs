using System.Collections.Immutable;
using System.IO.Enumeration;

namespace DiffBeacon.Core;

public sealed class FolderFilterOptions
{
    public ImmutableArray<string> Includes { get; }
    public ImmutableArray<string> Excludes { get; }
    public ImmutableArray<string> IgnoredDirectories { get; }
    public string FilterText { get; }
    public string IgnoredDirectoriesText { get; }
    public bool IsFiltered => Includes.Length > 0 || Excludes.Length > 0 || IgnoredDirectories.Length > 0;
    public string Description => $"File Filter: {(FilterText.Length == 0 ? "* (Implicit All Files)" : FilterText)}; Ignore Directories: {(IgnoredDirectoriesText.Length == 0 ? "None" : IgnoredDirectoriesText)}";
    internal FolderFilterOptions(ImmutableArray<string> includes, ImmutableArray<string> excludes, ImmutableArray<string> ignored, string normalized)
    { Includes = includes; Excludes = excludes; IgnoredDirectories = ignored; FilterText = normalized; IgnoredDirectoriesText = string.Join(';', ignored); }
    public bool IncludesFile(string basename) => (Includes.Length == 0 || Includes.Any(p => FileSystemName.MatchesSimpleExpression(p, basename, true))) && !Excludes.Any(p => FileSystemName.MatchesSimpleExpression(p, basename, true));
    public bool PrunesDirectory(string basename) => IgnoredDirectories.Contains(basename, StringComparer.OrdinalIgnoreCase);
}

public static class FolderFilters
{
    public static FolderFilterOptions Parse(string? filterText, string? ignoredDirectoriesText)
    {
        static string[] Items(string? text, string label)
        {
            text ??= "";
            if (text.Length > 4096) throw new ArgumentException(label + " exceeds 4096 characters.");
            if (text.Any(char.IsControl)) throw new ArgumentException(label + " cannot contain control characters.");
            var items = text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (items.Length > 64) throw new ArgumentException(label + " exceeds 64 clauses/items.");
            return items;
        }
        static void Validate(string value, bool directory)
        {
            if (value.Length == 0 || value.Length > 256) throw new ArgumentException("Each filter operand/directory must contain 1–256 characters.");
            if (value.IndexOfAny(['/', '\\', ':', '[', ']', '{', '}']) >= 0 || value.Contains("**", StringComparison.Ordinal)) throw new ArgumentException("Only basename glob patterns are supported; paths, [], {}, and ** are not allowed.");
            if (directory && (value is "." or ".." || value.IndexOfAny(['*', '?']) >= 0)) throw new ArgumentException("Ignored directories must be exact names, without wildcards, . or ..");
        }
        var include = ImmutableArray.CreateBuilder<string>(); var exclude = ImmutableArray.CreateBuilder<string>(); var clauses = new List<string>();
        foreach (var item in Items(filterText, "File Filter"))
        {
            bool not = item.Length > 3 && item.StartsWith("NOT", StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(item[3]);
            if (item.Equals("NOT", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("NOT requires a glob operand.");
            string operand = not ? item[3..].Trim() : item; Validate(operand, false);
            if (not) exclude.Add(operand); else include.Add(operand); clauses.Add(not ? "NOT " + operand : operand);
        }
        var dirs = ImmutableArray.CreateBuilder<string>();
        foreach (var item in Items(ignoredDirectoriesText, "Ignore Directories")) { Validate(item, true); if (!dirs.Contains(item, StringComparer.OrdinalIgnoreCase)) dirs.Add(item); }
        return new(include.ToImmutable(), exclude.ToImmutable(), dirs.ToImmutable(), string.Join(';', clauses));
    }
}
