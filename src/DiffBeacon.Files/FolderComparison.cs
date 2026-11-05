namespace DiffBeacon.Core;

public sealed record FolderRow(string RelativePath, Change Status, long? LeftSize, long? RightSize, string Detail);
public sealed record FolderScanCounts(long VisitedEntries, long ExcludedFiles, long PrunedDirectories);
public sealed record FolderResult(IReadOnlyList<FolderRow> Rows, bool Complete, FolderFilterOptions? Filters = null,
    FolderScanCounts? LeftCounts = null, FolderScanCounts? RightCounts = null, IReadOnlyList<string>? IncompleteReasons = null)
{
    public string Summary
    {
        get
        {
            var scope = Filters?.IsFiltered == true ? "Filtered Scope · " : "All Files · ";
            string counts = $" · Left Excluded Files {LeftCounts?.ExcludedFiles ?? 0:N0}, Pruned Directories {LeftCounts?.PrunedDirectories ?? 0:N0}; Right Excluded Files {RightCounts?.ExcludedFiles ?? 0:N0}, Pruned Directories {RightCounts?.PrunedDirectories ?? 0:N0}";
            string empty = Filters?.IsFiltered == true && !Rows.Any(r => r.Status != Change.Error) ? Complete ? "No Files Match These Filters · " : "No In-Scope Files Retained · Incomplete Scan · " : "";
            return scope + empty + $"{Rows.Count(r => r.Status != Change.Equal):N0} Differing Or Error Entries · {Rows.Count:N0} Retained Entries · Content Comparison · " +
                (Complete ? "Complete" : "INCOMPLETE — " + string.Join("; ", IncompleteReasons ?? ["Errors Or Limits"])) + " · " + (Filters ?? FolderFilters.Parse("", "")).Description + counts;
        }
    }
}
public static class FolderComparison
{
    public const int MaxFiles = 50_000;
    public const int MaxVisitedEntries = 1_000_000;
    public static FolderResult Compare(string left, string right, CancellationToken token = default) => Compare(left, right, FolderFilters.Parse("", ""), token);
    public static FolderResult Compare(string left, string right, FolderFilterOptions filters, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var errors = new List<FolderRow>(); bool complete = true; var reasons = new HashSet<string>(StringComparer.Ordinal);
        void Incomplete(string reason) { complete = false; reasons.Add(reason); }
        (Dictionary<string, string> Files, FolderScanCounts Counts) Scan(string root, string side)
        {
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(root));
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point roots are not supported.");
            long visited = 0, excluded = 0, pruned = 0;
            (Dictionary<string, string>, FolderScanCounts) Done() => (found, new(visited, excluded, pruned));
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested(); string folder = pending.Pop();
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
                    {
                        token.ThrowIfCancellationRequested();
                        if (visited >= MaxVisitedEntries) { Incomplete(side + " Visited-Entry Limit (1,000,000)"); return Done(); } visited++;
                        var attr = File.GetAttributes(entry); bool directory = (attr & FileAttributes.Directory) != 0; string basename = System.IO.Path.GetFileName(entry);
                        if (directory && filters.PrunesDirectory(basename)) { pruned++; continue; }
                        if (!directory && !filters.IncludesFile(basename)) { excluded++; continue; }
                        if ((attr & FileAttributes.ReparsePoint) != 0) { Incomplete(side + " In-Scope Reparse Points Skipped"); if (errors.Count < 1000) errors.Add(new(System.IO.Path.GetRelativePath(root, entry), Change.Error, null, null, "Reparse Point Skipped")); continue; }
                        if (directory) pending.Push(entry);
                        else { if (found.Count >= MaxFiles) { Incomplete(side + " Retained-File Limit (50,000)"); return Done(); } found.Add(System.IO.Path.GetRelativePath(root, entry), entry); }
                        if (pending.Count > MaxFiles) { Incomplete(side + " Pending-Directory Limit (50,000)"); return Done(); }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Incomplete(side + " Enumeration Errors"); if (errors.Count < 1000) errors.Add(new(System.IO.Path.GetRelativePath(root, folder), Change.Error, null, null, ex.Message)); }
            }
            return Done();
        }
        var leftScan = Scan(left, "Left"); var rightScan = Scan(right, "Right"); var a = leftScan.Files; var b = rightScan.Files; var rows = new List<FolderRow>();
        foreach (string name in a.Keys.Union(b.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested(); if (rows.Count >= MaxFiles) { Incomplete("Combined Retained-Result Limit (50,000)"); break; }
            bool hasA = a.TryGetValue(name, out var ap), hasB = b.TryGetValue(name, out var bp);
            try { rows.Add(new(name, !hasA ? Change.RightOnly : !hasB ? Change.LeftOnly : BinaryComparison.Compare(ap!, bp!, token).DifferentBytes == 0 ? Change.Equal : Change.Changed, hasA ? new FileInfo(ap!).Length : null, hasB ? new FileInfo(bp!).Length : null, "")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Incomplete("In-Scope Content/Metadata Errors"); rows.Add(new(name, Change.Error, null, null, ex.Message)); }
        }
        rows.AddRange(errors); return new(rows.AsReadOnly(), complete, filters, leftScan.Counts, rightScan.Counts, reasons.Order(StringComparer.Ordinal).ToArray());
    }
}

