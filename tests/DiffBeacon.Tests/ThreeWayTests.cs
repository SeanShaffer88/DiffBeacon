using DiffBeacon.Core;

static class ThreeWayTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool condition) { if (!condition) throw new Exception("Three-way assertion failed."); }
        static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        test("Three-way unchanged and empty", () => { Check(ThreeWayMerge.Create("", "", "").Render() == ""); Check(ThreeWayMerge.Create("a\nb\n", "a\nb\n", "a\nb\n").Render() == "a\nb\n"); });
        test("Three-way one-sided edits", () => { Check(ThreeWayMerge.Create("a\nb", "a\nlocal", "a\nb").Render() == "a\nlocal"); Check(ThreeWayMerge.Create("a\nb", "a\nb", "remote\nb").Render() == "remote\nb"); });
        test("Three-way disjoint changes merge automatically", () => Check(ThreeWayMerge.Create("a\nb\nc\nd", "A\nb\nc\nd", "a\nb\nc\nD").Render() == "A\nb\nc\nD"));
        test("Three-way identical edits resolve once", () => { var r = ThreeWayMerge.Create("a\nb", "x\nb", "x\nb"); Check(r.ConflictCount == 0 && r.Render() == "x\nb"); });
        test("Three-way adjacent nonzero spans independent", () => { var r = ThreeWayMerge.Create("a\nb\nc", "A\nb\nc", "a\nB\nc"); Check(r.ConflictCount == 0 && r.Render() == "A\nB\nc"); });
        test("Three-way competing changes require explicit choice", () => { var r = ThreeWayMerge.Create("a\nb", "L\nb", "R\nb"); Check(r.ConflictCount == 1); Throws<InvalidOperationException>(() => r.Render()); string id = r.Segments.Single(s => s.Kind == MergeSegmentKind.Conflict).Id; Check(r.Render(new Dictionary<string, MergeChoice> { [id] = MergeChoice.Local }) == "L\nb"); Check(r.Render(new Dictionary<string, MergeChoice> { [id] = MergeChoice.Remote }) == "R\nb"); Check(r.Render(new Dictionary<string, MergeChoice> { [id] = MergeChoice.Both }) == "L\nR\nb"); });
        test("Three-way same-position insertion conflicts", () => { var r = ThreeWayMerge.Create("a\nb", "a\nL\nb", "a\nR\nb"); Check(r.ConflictCount == 1); Check(r.Segments.Single(s => s.Kind == MergeSegmentKind.Conflict).BaseCount == 0); });
        test("Three-way identical insertion emitted once", () => Check(ThreeWayMerge.Create("a\nb", "a\nx\nb", "a\nx\nb").Render() == "a\nx\nb"));
        test("Three-way insertion touching replacement conflicts", () => Check(ThreeWayMerge.Create("a\nb\nc", "a\nx\nb\nc", "a\nB\nc").ConflictCount == 1));
        test("Three-way insertion touching deletion end conflicts", () => Check(ThreeWayMerge.Create("a\nb\nc", "a\nc", "a\nb\nx\nc").ConflictCount == 1));
        test("Three-way transitive overlap forms one conflict", () => { var r = ThreeWayMerge.Create("a\nb\nc\nd\ne\nf", "a\nB\nc\nD\ne\nf", "a\nR\ne\nf"); Check(r.ConflictCount == 1); var c = r.Segments.Single(s => s.Kind == MergeSegmentKind.Conflict); Check(c.BaseStart == 1 && c.BaseCount == 3 && c.LocalLines.SequenceEqual(new[] { "B", "c", "D" })); });
        test("Three-way stable conflict IDs and ordered segments", () => { var r = ThreeWayMerge.Create("a\nb\nc\nd\ne", "A\nb\nc\nD\ne", "X\nb\nc\nY\ne"); var again = ThreeWayMerge.Create("a\nb\nc\nd\ne", "A\nb\nc\nD\ne", "X\nb\nc\nY\ne"); Check(r.ConflictCount == 2 && r.Segments.Select(s => s.Id).SequenceEqual(again.Segments.Select(s => s.Id))); Check(r.Segments.Zip(r.Segments.Skip(1)).All(p => p.First.BaseStart + p.First.BaseCount <= p.Second.BaseStart)); });
        test("Three-way CRLF normalized deliberately", () => Check(ThreeWayMerge.Create("a\r\nb\r\n", "A\r\nb\r\n", "a\r\nb\r\n").Render() == "A\nb\n"));
        test("Three-way refuses positional fallback", () => { string input = string.Join('\n', Enumerable.Range(0, 1500)); Throws<InvalidDataException>(() => ThreeWayMerge.Create(input, input, input)); });
        test("Three-way combined character and line guards", () => { Throws<InvalidDataException>(() => ThreeWayMerge.Create(new string('a', 2_000_000), new string('a', 2_000_000), "x")); Throws<InvalidDataException>(() => ThreeWayMerge.Create(new string('\n', 40_000), new string('\n', 40_000), new string('\n', 40_000))); });
        test("Three-way cancellation", () => Throws<OperationCanceledException>(() => ThreeWayMerge.Create("a", "b", "c", new CancellationToken(true))));
        test("Three-way stale choice IDs rejected", () => { var resolved = ThreeWayMerge.Create("a", "a", "a"); Throws<ArgumentException>(() => resolved.Render(new Dictionary<string, MergeChoice> { ["stale"] = MergeChoice.Local })); var conflicted = ThreeWayMerge.Create("a", "b", "c"); var id = conflicted.Segments.Single(s => s.Kind == MergeSegmentKind.Conflict).Id; Throws<ArgumentException>(() => conflicted.Render(new Dictionary<string, MergeChoice> { [id] = MergeChoice.Local, ["unknown"] = MergeChoice.Remote })); });
    }
}
