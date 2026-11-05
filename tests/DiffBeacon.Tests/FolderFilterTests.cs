using System.Text;
using System.Text.Json;
using DiffBeacon.Core;

static class FolderFilterTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool condition) { if (!condition) throw new Exception("Folder filter assertion failed."); }
        static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        test("Folder blank filters preserve all basenames", () => { var f = FolderFilters.Parse(" ; ", " ; "); Check(!f.IsFiltered && f.IncludesFile("LICENSE") && f.IncludesFile(".gitignore") && !f.PrunesDirectory("bin")); });
        test("Folder incomplete empty scope never claims no matches", () => { var filter = FolderFilters.Parse("*.cs", ""); var errorOnly = new FolderResult(new[] { new FolderRow("link", Change.Error, null, null, "Reparse Point Skipped") }, false, filter, new(1, 0, 0), new(1, 0, 0), new[] { "In-Scope Reparse Points Skipped" }); Check(!errorOnly.Summary.Contains("No Files Match These Filters") && errorOnly.Summary.Contains("No In-Scope Files Retained · Incomplete Scan")); var limitOnly = errorOnly with { Rows = Array.Empty<FolderRow>(), IncompleteReasons = new[] { "Visited-Entry Limit (1,000,000)" } }; Check(!limitOnly.Summary.Contains("No Files Match These Filters") && limitOnly.Summary.Contains("Incomplete Scan")); Check((limitOnly with { Complete = true }).Summary.Contains("No Files Match These Filters")); });
        test("Folder include OR case insensitive with NOT precedence", () => { var f = FolderFilters.Parse(" *.CS ; *.txt ; not  *.g.cs ;NOT secret*", ""); Check(f.IncludesFile("Beacon.Cs") && f.IncludesFile("readme.TXT") && !f.IncludesFile("BEACON.G.CS") && !f.IncludesFile("Secret.cs") && !f.IncludesFile("asset.png")); Check(f.FilterText == "*.CS;*.txt;NOT *.g.cs;NOT secret*"); });
        test("Folder exclusions imply include all and literal star dot star", () => { var f = FolderFilters.Parse("NOT *.bak", ""); Check(f.IncludesFile("LICENSE") && !f.IncludesFile("a.BAK")); var dotted = FolderFilters.Parse("*.*", ""); Check(!dotted.IncludesFile("LICENSE") && dotted.IncludesFile("a.txt") && dotted.IncludesFile(".gitignore")); });
        test("Folder question glob and Unicode matching", () => { var f = FolderFilters.Parse("文?.txt;Ä.txt", ""); Check(f.IncludesFile("文档.txt") && !f.IncludesFile("文.txt") && f.IncludesFile("ä.TXT")); });
        test("Folder NOT requires separate whitespace token", () => { Check(FolderFilters.Parse("NOTfile", "").IncludesFile("NOTfile")); foreach (string value in new[] { "NOT", " not  ", "a;NOT" }) Throws<ArgumentException>(() => FolderFilters.Parse(value, "")); });
        test("Folder parser rejects unsupported syntax", () => { foreach (string value in new[] { "a/b", "a\\b", "C:x", "[ab]", "{a,b}", "**.cs", "a\0b", "a\nb", "NOT\t*.bak" }) Throws<ArgumentException>(() => FolderFilters.Parse(value, "")); });
        test("Folder exact ignored directory names", () => { var f = FolderFilters.Parse("", " .git ; BIN ; bin ; obj "); Check(f.PrunesDirectory("bin") && f.PrunesDirectory(".GIT") && !f.PrunesDirectory("binary") && f.IgnoredDirectories.Length == 3 && f.IgnoredDirectoriesText == ".git;BIN;obj"); });
        test("Folder rejects wildcard/path directory names", () => { foreach (string value in new[] { ".", "..", "*", "b?n", "a/b", "a\\b", "C:", "[bin]", "a\rb" }) Throws<ArgumentException>(() => FolderFilters.Parse("", value)); });
        test("Folder parser field item and operand bounds", () => { Throws<ArgumentException>(() => FolderFilters.Parse(new string('a', 4097), "")); Throws<ArgumentException>(() => FolderFilters.Parse("", new string('a', 4097))); Throws<ArgumentException>(() => FolderFilters.Parse(new string('a', 257), "")); Throws<ArgumentException>(() => FolderFilters.Parse("", new string('a', 257))); Throws<ArgumentException>(() => FolderFilters.Parse(string.Join(';', Enumerable.Repeat("a", 65)), "")); Throws<ArgumentException>(() => FolderFilters.Parse("", string.Join(';', Enumerable.Repeat("a", 65)))); Check(FolderFilters.Parse(string.Join(';', Enumerable.Repeat("a", 64)), "").Includes.Length == 64); });
        test("Folder pruning basename matching stats and zero-scope", () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-Filters-" + Guid.NewGuid().ToString("N"));
            try
            {
                void Write(string relative, string text) { string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, text); }
                foreach (string side in new[] { "left", "right" }) { Write(side + "/source/Beacon.cs", side); Write(side + "/source/Auto.g.cs", "excluded"); Write(side + "/notes.txt", "excluded"); Write(side + "/BIN/secret.cs", "pruned"); Write(side + "/bin-file.cs", "same"); }
                string a = Path.Combine(root, "left"), b = Path.Combine(root, "right");
                var f = FolderFilters.Parse("*.cs;NOT *.g.cs", "bin"); var result = FolderComparison.Compare(a, b, f);
                Check(result.Complete && result.Rows.Count == 2 && result.Rows.Count(r => r.Status == Change.Changed) == 1 && result.LeftCounts?.ExcludedFiles == 2 && result.RightCounts?.PrunedDirectories == 1 && result.LeftCounts?.PrunedDirectories == 1 && result.Summary.Contains("Filtered Scope") && result.Summary.Contains("*.cs;NOT *.g.cs"));
                var empty = FolderComparison.Compare(a, b, FolderFilters.Parse("*.png", "bin")); Check(empty.Rows.Count == 0 && empty.Summary.Contains("No Files Match These Filters") && empty.Complete);
                var all = FolderComparison.Compare(a, b); Check(all.Rows.Count == 5 && all.LeftCounts?.ExcludedFiles == 0 && all.LeftCounts.PrunedDirectories == 0);
                // A selected root whose own name is ignored is still traversed.
                var namedRoot = FolderComparison.Compare(Path.Combine(a, "BIN"), Path.Combine(b, "BIN"), FolderFilters.Parse("*.cs", "BIN")); Check(namedRoot.Rows.Count == 1 && namedRoot.LeftCounts?.PrunedDirectories == 0);
                string report = Reports.Html("Scope", result.Summary, result.Rows.Select(r => new[] { r.RelativePath })); Check(report.Contains("Filtered Scope") && report.Contains("Left Excluded Files 2"));
                var blank = FolderComparison.Compare(a, b, FolderFilters.Parse("", "")); Check(blank.Rows.Select(r => r.RelativePath).SequenceEqual(all.Rows.Select(r => r.RelativePath)));
                Throws<OperationCanceledException>(() => FolderComparison.Compare(a, b, f, new CancellationToken(true)));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        });
        test("Folder excludes files before opening content", () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-LockedFilter-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { string p = Path.Combine(root, "excluded.bak"); File.WriteAllText(p, "locked"); using var held = File.Open(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None); var result = FolderComparison.Compare(root, root, FolderFilters.Parse("NOT *.bak", "")); Check(result.Complete && result.Rows.Count == 0 && result.LeftCounts?.ExcludedFiles == 1); }
            finally { Directory.Delete(root, true); }
        });
        test("Folder sessions v1 migration v2 roundtrip validation", () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-SessionFilter-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); string path = Path.Combine(root, "session.json");
            try
            {
                File.WriteAllText(path, "{\"Version\":1,\"Name\":\"Legacy\",\"Mode\":\"Folders\",\"Left\":\"a\",\"Right\":\"b\",\"Options\":{}}", Encoding.UTF8); var legacy = Session.Load(path); Check(legacy.FileFilter == "" && legacy.IgnoredDirectories == "");
                var s = new Session(2, "Scoped", "Folders", "a", "b", new(), "*.cs;NOT *.g.cs", ".git;bin;obj"); s.Save(path); Check(Session.Load(path) == s);
                File.WriteAllText(path, JsonSerializer.Serialize(s with { FileFilter = "**" })); Throws<ArgumentException>(() => Session.Load(path));
                File.WriteAllText(path, JsonSerializer.Serialize(s with { Version = 1 })); Throws<InvalidDataException>(() => Session.Load(path));
                Throws<ArgumentException>(() => (s with { IgnoredDirectories = ".." }).Save(path));
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
