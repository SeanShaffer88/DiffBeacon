using System.Text;
using DiffBeacon.Core;

int passed = 0, failed = 0;
void Test(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); } }
void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
string root = Path.Combine(Path.GetTempPath(), "DiffBeacon-Tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
string Write(string name, string content) { string path = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content, new UTF8Encoding(false)); return path; }
try
{
    Test("Aligned insertion", () => { var r = TextComparison.Compare("a\nb\nc", "a\nx\nb\nc", new()); Check(!r.PositionalFallback && r.Differences == 1 && r.Rows[1].Status == Change.RightOnly); });
    Test("Aligned deletion and replacement", () => { Check(TextComparison.Compare("a\nx\nb", "a\nb", new()).Rows[1].Status == Change.LeftOnly); Check(TextComparison.Compare("a\nb", "a\nc", new()).Rows[1].Status == Change.Changed); });
    Test("Ignore preserves raw source", () => { var r = TextComparison.Compare(" A \n\nb", "a\nB", new(true, true, true)); Check(r.Differences == 0 && r.Rows[0].Left == " A " && r.Rows[1].LeftLine == 3); });
    Test("Budget fallback explicit", () => Check(TextComparison.Compare("a\nb", "a\nc", new(), maxCells: 1).PositionalFallback));
    Test("Text cancellation", () => Throws<OperationCanceledException>(() => TextComparison.Compare("a", "b", new(), new CancellationToken(true))));
    Test("Empty and trailing newline distinguished", () => Check(TextComparison.Compare("", "\n", new()).Differences == 1));
    Test("Text character limit", () => Throws<InvalidDataException>(() => TextComparison.Compare(new string('x', TextComparison.MaxCharacters + 1), "", new())));
    Test("Line limit checked before splitting", () => Throws<InvalidDataException>(() => TextComparison.Compare(new string('\n', 500_000), "", new())));
    Test("Strict Unicode BOM round trip", () => { foreach (var encoding in new Encoding[] { new UTF8Encoding(true, true), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true), new UTF32Encoding(false, true, true), new UTF32Encoding(true, true, true) }) { var path = Path.Combine(root, "日本語-" + encoding.CodePage); File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes("海\r\nBeacon")).ToArray()); var d = TextDocument.Load(path); Check(d.Text == "海\r\nBeacon"); Check(d.Encode("海\nBeacon").SequenceEqual(File.ReadAllBytes(path))); } });
    Test("Invalid UTF8 rejected", () => { var p = Path.Combine(root, "invalid"); File.WriteAllBytes(p, [0xff, 0x80]); Throws<InvalidDataException>(() => TextDocument.Load(p)); });
    Test("Mixed newline save rejected", () => { var d = TextDocument.Load(Write("mixed", "a\r\nb\nc")); Throws<InvalidDataException>(() => d.Encode("a\nb\nc")); });
    Test("Atomic save and backup", () => { string p = Write("safe", "original"); var s = FileStamp.Capture(p); SafeFiles.Write(p, Encoding.UTF8.GetBytes("updated"), s, true); Check(File.ReadAllText(p) == "updated"); Check(Directory.GetFiles(root, "safe.diffbeacon-backup-*").Any(x => File.ReadAllText(x) == "original")); });
    Test("Stale save preserves destination", () => { string p = Write("stale", "first"); var s = FileStamp.Capture(p); File.WriteAllText(p, "external"); Throws<IOException>(() => SafeFiles.Write(p, [42], s, true)); Check(File.ReadAllText(p) == "external"); });
    Test("No implicit overwrite", () => { string p = Write("exists", "keep"); Throws<IOException>(() => SafeFiles.Write(p, [42], null, false)); Check(File.ReadAllText(p) == "keep"); });
    Test("Failed save preserves file", () => { string p = Write("locked", "keep"); var s = FileStamp.Capture(p); using var locked = File.Open(p, FileMode.Open, FileAccess.Read, FileShare.None); Throws<IOException>(() => SafeFiles.Write(p, [42], s, true)); });
    foreach (bool saveRight in new[] { false, true })
    foreach (bool otherDirty in new[] { false, true })
    {
        Test($"Pane snapshot isolation right={saveRight} otherDirty={otherDirty}", () =>
        {
            string prefix = $"pane-{saveRight}-{otherDirty}";
            string leftPath = Write(prefix + "-left", "left\noriginal");
            string rightPath = Path.Combine(root, prefix + "-right");
            var utf16 = new UnicodeEncoding(false, true, true);
            File.WriteAllBytes(rightPath, utf16.GetPreamble().Concat(utf16.GetBytes("right\r\noriginal")).ToArray());
            TextDocument? left = TextDocument.Load(leftPath), right = TextDocument.Load(rightPath);
            bool dirtyLeft = saveRight ? otherDirty : true, dirtyRight = saveRight ? true : otherDirty;
            string leftText = saveRight && !otherDirty ? left.Text : "left\npending";
            string rightText = !saveRight && !otherDirty ? right.Text : "right\npending";
            var untouched = saveRight ? left : right;
            string pending = saveRight ? leftText : rightText;
            string destination = saveRight ? leftPath : rightPath;
            byte[] expected = (saveRight ? right : left).Encode(saveRight ? rightText : leftText);
            void Save(bool toRight, string path)
            {
                if (toRight) { TextDocument.SavePane(ref right, ref dirtyRight, rightText, path, left); rightPath = path; }
                else { TextDocument.SavePane(ref left, ref dirtyLeft, leftText, path, right); leftPath = path; }
            }
            Save(saveRight, destination);
            Check(File.ReadAllBytes(destination).SequenceEqual(expected), "Saving pane encoding/newlines changed");
            Check(!(saveRight ? dirtyRight : dirtyLeft), "Saving pane remains dirty");
            Check(ReferenceEquals(saveRight ? left : right, untouched), "Other pane snapshot replaced");
            Check((saveRight ? dirtyLeft : dirtyRight) == otherDirty, "Other dirty state changed");
            Check((saveRight ? leftText : rightText) == pending, "Other pending text changed");
            Check((saveRight ? leftPath : rightPath) == untouched.Path, "Other path changed");
            Save(saveRight, destination); // Paths now converge; the saving pane must win stamp selection.
            byte[] saved = File.ReadAllBytes(destination);
            Throws<IOException>(() => Save(!saveRight, destination));
            Check(File.ReadAllBytes(destination).SequenceEqual(saved), "Stale pane overwrote saved content");
            Check(ReferenceEquals(saveRight ? left : right, untouched), "Conflict changed stale snapshot");
            Check((saveRight ? dirtyLeft : dirtyRight) == otherDirty && (saveRight ? leftText : rightText) == pending, "Conflict lost pending state");
            if (saveRight) { rightText = "right\nsecond edit"; dirtyRight = true; }
            else { leftText = "left\nsecond edit"; dirtyLeft = true; }
            Save(saveRight, destination);
            string external = "external\nchange";
            File.WriteAllText(destination, external);
            var beforeConflict = saveRight ? right : left;
            if (saveRight) { rightText = "right\nunsaved"; dirtyRight = true; }
            else { leftText = "left\nunsaved"; dirtyLeft = true; }
            Throws<IOException>(() => Save(saveRight, destination));
            Check(File.ReadAllText(destination) == external && ReferenceEquals(saveRight ? right : left, beforeConflict), "External conflict changed file or snapshot");
            Check(saveRight ? dirtyRight && rightText == "right\nunsaved" : dirtyLeft && leftText == "left\nunsaved", "External conflict lost edits");
            string recovery = Path.Combine(root, prefix + "-recovery");
            Save(!saveRight, recovery);
            Check(File.ReadAllBytes(recovery).SequenceEqual(untouched.Encode(pending)), "Stale pane lost its BOM/newline convention");
        });
    }
    Test("Binary empty/equal/unequal", () => { string a = Write("empty", ""), b = Write("one", "x"); Check(BinaryComparison.Compare(a, a).DifferentBytes == 0); var r = BinaryComparison.Compare(a, b); Check(r.DifferentBytes == 1 && r.FirstDifference == 0 && r.Samples[0].Left == null); });
    Test("Binary late mismatch and sample cap", () => { string a = Path.Combine(root, "bytes-a"), b = Path.Combine(root, "bytes-b"); File.WriteAllBytes(a, new byte[200000]); var content = new byte[200000]; Array.Fill(content, (byte)1, 70000, 130000); File.WriteAllBytes(b, content); var r = BinaryComparison.Compare(a, b); Check(r.FirstDifference == 70000 && r.DifferentBytes == 130000 && r.Samples.Count == 512); });
    Test("Binary cancellation", () => Throws<OperationCanceledException>(() => BinaryComparison.Compare(Write("can-a", "a"), Write("can-b", "a"), new CancellationToken(true))));
    Test("Recursive folder and missing files", () => { Write("left/sub/equal", "a"); Write("right/sub/equal", "a"); Write("left/changed", "a"); Write("right/changed", "b"); Write("left/only", "c"); var r = FolderComparison.Compare(Path.Combine(root, "left"), Path.Combine(root, "right")); Check(r.Complete && r.Rows.Count == 3 && r.Rows.Count(x => x.Status == Change.Equal) == 1 && r.Rows.Count(x => x.Status == Change.Changed) == 1 && r.Rows.Count(x => x.Status == Change.LeftOnly) == 1); });
    Test("Folder missing root error", () => Throws<DirectoryNotFoundException>(() => FolderComparison.Compare(root, Path.Combine(root, "absent"))));
    Test("Folder cancellation", () => Throws<OperationCanceledException>(() => FolderComparison.Compare(root, root, new CancellationToken(true))));
    Test("CSV quotes multiline escaped trailing empty", () => { var r = DelimitedText.Parse("a,\"b,c\",\"d\"\"e\",\r\n\"multi\nline\",x,,"); Check(r.Count == 2 && r[0].Length == 4 && r[0][1] == "b,c" && r[0][2] == "d\"e" && r[0][3] == "" && r[1][0] == "multi\nline" && r[1].Length == 4); });
    Test("TSV Unicode and ragged rows", () => { var r = DelimitedText.Parse("海\tx\ny", '\t'); Check(r[0][0] == "海" && r[1].Length == 1); });
    Test("Malformed CSV rejected", () => { Throws<InvalidDataException>(() => DelimitedText.Parse("\"unclosed")); Throws<InvalidDataException>(() => DelimitedText.Parse("\"a\"junk,b")); Throws<InvalidDataException>(() => DelimitedText.Parse("a\"b,c")); });
    Test("Session metadata round trip", () => { string p = Path.Combine(root, "session.json"); var s = new Session(1, "Test", "Text", "left", "right", new(true)); s.Save(p); Check(Session.Load(p) == s); });
    Test("Oversized session save preserves existing file", () => { string p = Write("oversized-session.json", "keep"); var s = new Session(3, new string('海', 12_000), "Text", "left", "right", new()); Throws<InvalidDataException>(() => s.Save(p)); Check(File.ReadAllText(p) == "keep"); Check(!Directory.GetFiles(root, "oversized-session.json.diffbeacon-backup-*").Any()); });
    Test("Version 3 session round trip", () => { string p = Path.Combine(root, "session-v3.json"); var s = new Session(3, "海", "Text", "left", "right", new(), Granularity: HighlightGranularity.Character, SyncScrolling: false, WordWrap: true, TableSeparator: "\t"); s.Save(p); Check(Session.Load(p) == s); });
    Test("Edit line rejection allocation bound", () => { string replacement = new('\n', 2_000_000); long before = GC.GetAllocatedBytesForCurrentThread(); Throws<InvalidDataException>(() => TextEditProposal.Replace("", 0, 0, replacement, "")); Check(GC.GetAllocatedBytesForCurrentThread() - before < 100_000, "Line rejection allocated more than 100 KB"); });
    Test("Edit line budget merges CRLF across segments", () => { string other = new('\n', TextComparison.MaxLines - 3); Check(TextEditProposal.Replace("\rX", 1, 1, "\n", other) == "\r\n"); Check(TextEditProposal.Replace("X\n", 0, 1, "\r", other) == "\r\n"); Check(TextEditProposal.Replace("\rX\n", 1, 1, "", other) == "\r\n"); Throws<InvalidDataException>(() => TextEditProposal.Replace("\rX\n", 1, 1, "x", other)); Throws<InvalidDataException>(() => TextEditProposal.Replace("", 0, 0, "", new string('\n', TextComparison.MaxLines - 1))); });
    Test("Invalid sessions rejected", () => { Throws<System.Text.Json.JsonException>(() => Session.Load(Write("bad.json", "{"))); Throws<InvalidDataException>(() => Session.Load(Write("unsupported.json", "{\"Version\":2}"))); });
    Test("HTML escapes hostile content", () => { string html = Reports.Html("<script>", "a&b", new[] { new[] { "<img onerror='x'>" } }); Check(!html.Contains("<script>") && !html.Contains("<img") && html.Contains("&lt;img") && html.Contains("a&amp;b")); });
    ModuleCompatibilityTests.Run(Test);
    ThreeWayTests.Run(Test);
    FolderFilterTests.Run(Test);
    BlockMergeTests.Run(Test);
    InlineDiffTests.Run(Test);
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{passed} Passed, {failed} Failed"); return failed == 0 ? 0 : 1;
