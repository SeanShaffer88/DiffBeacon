using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace DiffBeacon.Core;

public enum MergeChoice { Local, Remote, Both }
public enum MergeSegmentKind { Unchanged, Automatic, Conflict }
public sealed record MergeSegment(string Id, int BaseStart, int BaseCount, MergeSegmentKind Kind,
    ImmutableArray<string> BaseLines, ImmutableArray<string> LocalLines,
    ImmutableArray<string> RemoteLines, ImmutableArray<string> MergedLines);

public sealed record ThreeWayResult(ImmutableArray<MergeSegment> Segments)
{
    public int ConflictCount => Segments.Count(s => s.Kind == MergeSegmentKind.Conflict);
    /// <summary>Returns logical text using LF line endings. Every conflict requires an explicit choice.</summary>
    public string Render(IReadOnlyDictionary<string, MergeChoice>? choices = null, CancellationToken token = default)
    {
        var conflictIds = Segments.Where(s => s.Kind == MergeSegmentKind.Conflict).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (choices != null && choices.Keys.Any(id => !conflictIds.Contains(id))) throw new ArgumentException("Resolution choices contain a stale or unknown conflict ID.", nameof(choices));
        var lines = new List<string>();
        foreach (var segment in Segments)
        {
            token.ThrowIfCancellationRequested();
            if (segment.Kind != MergeSegmentKind.Conflict) { lines.AddRange(segment.MergedLines); continue; }
            if (choices == null || !choices.TryGetValue(segment.Id, out var choice)) throw new InvalidOperationException("Unresolved merge conflict: " + segment.Id);
            switch (choice)
            {
                case MergeChoice.Local: lines.AddRange(segment.LocalLines); break;
                case MergeChoice.Remote: lines.AddRange(segment.RemoteLines); break;
                case MergeChoice.Both: lines.AddRange(segment.LocalLines); lines.AddRange(segment.RemoteLines); break;
                default: throw new ArgumentOutOfRangeException(nameof(choices), "Unknown merge choice.");
            }
        }
        return string.Join('\n', lines);
    }
}

/// <summary>Conservative three-way line merging. No file IO, input mutation, ignored text, or positional fallback.</summary>
public static class ThreeWayMerge
{
    sealed record Hunk(int Start, int Count, ImmutableArray<string> Replacement, bool Local)
    { public int End => Start + Count; }

    public static string Render(ThreeWayResult plan, IReadOnlyDictionary<string, MergeChoice>? choices = null, CancellationToken token = default) => plan.Render(choices, token);

    public static ThreeWayResult Create(string baseText, string localText, string remoteText, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (baseText.Length + (long)localText.Length + remoteText.Length > TextComparison.MaxCharacters) throw new InvalidDataException("Three-way inputs exceed the 4 million combined character budget.");
        int CountLines(string text)
        {
            int count = 1;
            for (int i = 0; i < text.Length; i++) { if ((i & 4095) == 0) token.ThrowIfCancellationRequested(); if (text[i] == '\r') { count++; if (i + 1 < text.Length && text[i + 1] == '\n') i++; } else if (text[i] == '\n') count++; }
            return count;
        }
        if (CountLines(baseText) + (long)CountLines(localText) + CountLines(remoteText) > TextComparison.MaxLines) throw new InvalidDataException("Three-way inputs exceed the 100,000 combined line budget.");
        var left = TextComparison.Compare(baseText, localText, new(), token);
        var right = TextComparison.Compare(baseText, remoteText, new(), token);
        if (left.PositionalFallback || right.PositionalFallback) throw new InvalidDataException("Three-way alignment budget exceeded. A positional comparison cannot safely drive a merge.");
        string[] baseline = TextComparison.Lines(baseText);
        var hunks = Hunks(left, true).Concat(Hunks(right, false)).OrderBy(h => h.Start).ThenBy(h => h.End).ToArray();
        var segments = ImmutableArray.CreateBuilder<MergeSegment>(); int cursor = 0, hunkIndex = 0;
        while (hunkIndex < hunks.Length)
        {
            token.ThrowIfCancellationRequested();
            var group = new List<Hunk> { hunks[hunkIndex++] };
            while (hunkIndex < hunks.Length && group.Any(h => Overlaps(h, hunks[hunkIndex]))) group.Add(hunks[hunkIndex++]);
            int start = group.Min(h => h.Start), end = group.Max(h => h.End);
            if (cursor < start) { var unchanged = baseline[cursor..start].ToImmutableArray(); segments.Add(new("unchanged-" + cursor, cursor, start - cursor, MergeSegmentKind.Unchanged, unchanged, unchanged, unchanged, unchanged)); }
            var original = baseline[start..end].ToImmutableArray();
            var local = Apply(baseline, start, end, group.Where(h => h.Local));
            var remote = Apply(baseline, start, end, group.Where(h => !h.Local));
            bool localChanged = group.Any(h => h.Local), remoteChanged = group.Any(h => !h.Local);
            bool conflict = localChanged && remoteChanged && !local.SequenceEqual(remote);
            var merged = conflict ? ImmutableArray<string>.Empty : localChanged ? local : remote;
            string id = conflict ? ConflictId(start, end - start, original, local, remote) : "automatic-" + start;
            segments.Add(new(id, start, end - start, conflict ? MergeSegmentKind.Conflict : MergeSegmentKind.Automatic, original, local, remote, merged));
            cursor = end;
        }
        if (cursor < baseline.Length) { var rest = baseline[cursor..].ToImmutableArray(); segments.Add(new("unchanged-" + cursor, cursor, baseline.Length - cursor, MergeSegmentKind.Unchanged, rest, rest, rest, rest)); }
        return new(segments.ToImmutable());
    }
    static IEnumerable<Hunk> Hunks(TextResult diff, bool local)
    {
        int cursor = 0, start = -1, count = 0; var replacement = ImmutableArray.CreateBuilder<string>();
        foreach (var row in diff.Rows)
        {
            if (row.Status == Change.Equal)
            {
                if (start >= 0) { yield return new(start, count, replacement.ToImmutable(), local); start = -1; count = 0; replacement.Clear(); }
                cursor++;
            }
            else
            {
                if (start < 0) start = cursor;
                if (row.LeftLine.HasValue) { count++; cursor++; }
                if (row.RightLine.HasValue) replacement.Add(row.Right!);
            }
        }
        if (start >= 0) yield return new(start, count, replacement.ToImmutable(), local);
    }
    static bool Overlaps(Hunk a, Hunk b)
    {
        if (a.Count == 0) return a.Start >= b.Start && a.Start <= b.End;
        if (b.Count == 0) return b.Start >= a.Start && b.Start <= a.End;
        return a.Start < b.End && b.Start < a.End;
    }
    static ImmutableArray<string> Apply(string[] baseline, int start, int end, IEnumerable<Hunk> source)
    {
        var output = ImmutableArray.CreateBuilder<string>(); int cursor = start;
        foreach (var hunk in source.OrderBy(h => h.Start)) { output.AddRange(baseline[cursor..hunk.Start]); output.AddRange(hunk.Replacement); cursor = hunk.End; }
        output.AddRange(baseline[cursor..end]); return output.ToImmutable();
    }
    static string ConflictId(int start, int count, params ImmutableArray<string>[] versions)
    {
        var text = new StringBuilder().Append(start).Append(':').Append(count).Append(':');
        foreach (var lines in versions) { text.Append(lines.Length).Append('['); foreach (var line in lines) text.Append(line.Length).Append(':').Append(line); text.Append(']'); }
        return "conflict-" + start + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
