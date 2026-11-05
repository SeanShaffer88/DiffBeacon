using System.Globalization;
using System.Text;

namespace DiffBeacon.Core;

public enum HighlightGranularity { Line, Word, Character }
public readonly record struct TextSpan(int Start, int Length);
public sealed record InlineLineDifference(IReadOnlyList<TextSpan> Left, IReadOnlyList<TextSpan> Right, bool IsFallback = false, string? FallbackReason = null);
public sealed record InlineDiffLimits(int MaxTokensPerPair = 16384, long MaxCellsPerPair = 250000, long MaxTotalCells = 2000000, int MaxTotalSpans = 20000);
public sealed record InlineDiffResult(IReadOnlyList<InlineLineDifference> Rows, int FallbackLines, long CellsUsed);

public static class InlineDifferences
{
    sealed record Part(string Key, IReadOnlyList<TextSpan> Raw);
    static TextSpan[] Whole(string? text) => string.IsNullOrEmpty(text) ? [] : [new(0, text.Length)];

    public static InlineDiffResult Compare(TextResult comparison, CompareOptions options, HighlightGranularity granularity, CancellationToken token = default, InlineDiffLimits? limits = null)
    {
        token.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(granularity)) throw new ArgumentOutOfRangeException(nameof(granularity));
        limits ??= new();
        if (limits.MaxTokensPerPair < 0 || limits.MaxCellsPerPair < 0 || limits.MaxTotalCells < 0 || limits.MaxTotalSpans < 0) throw new ArgumentOutOfRangeException(nameof(limits));
        var rows = new List<InlineLineDifference>(comparison.Rows.Count);
        long used = 0; int spans = 0, fallbacks = 0;
        foreach (var row in comparison.Rows)
        {
            token.ThrowIfCancellationRequested();
            if (row.Status == Change.Equal) { rows.Add(new([], [])); continue; }
            if (granularity == HighlightGranularity.Line || row.Left is null || row.Right is null) { rows.Add(new(Whole(row.Left), Whole(row.Right))); continue; }
            if (NormalizedEqual(row.Left, row.Right, options, token)) { rows.Add(new([], [])); continue; }
            int remainingSpans = limits.MaxTotalSpans - spans;
            var a = Tokenize(row.Left, options, granularity, token, limits.MaxTokensPerPair, remainingSpans, out var reason);
            if (reason != null) { Fallback(reason); continue; }
            var b = Tokenize(row.Right, options, granularity, token, limits.MaxTokensPerPair - a.Count, remainingSpans - a.Sum(p => p.Raw.Count), out reason);
            if (reason != null) { Fallback(reason); continue; }
            long cells = (a.Count + 1L) * (b.Count + 1L);
            long possibleSpans = a.Sum(p => (long)p.Raw.Count) + b.Sum(p => (long)p.Raw.Count);
            reason = a.Count + (long)b.Count > limits.MaxTokensPerPair ? "Token budget exceeded" : cells > Array.MaxLength ? "Cell storage budget exceeded" : cells > limits.MaxCellsPerPair ? "Pair cell budget exceeded" : cells > limits.MaxTotalCells - used ? "Total cell budget exceeded" : possibleSpans > limits.MaxTotalSpans - spans ? "Total span budget exceeded" : null;
            if (reason != null) { Fallback(reason); continue; }
            used += cells;
            var lcs = new int[checked((int)cells)]; int width = b.Count + 1;
            for (int i = a.Count - 1; i >= 0; i--)
            {
                token.ThrowIfCancellationRequested();
                for (int j = b.Count - 1; j >= 0; j--)
                {
                    if ((j & 255) == 0) token.ThrowIfCancellationRequested();
                    lcs[i * width + j] = a[i].Key == b[j].Key ? lcs[(i + 1) * width + j + 1] + 1 : Math.Max(lcs[(i + 1) * width + j], lcs[i * width + j + 1]);
                }
            }
            var left = new List<TextSpan>(); var right = new List<TextSpan>(); int x = 0, y = 0; bool over = false;
            while (x < a.Count || y < b.Count)
            {
                token.ThrowIfCancellationRequested();
                if (x < a.Count && y < b.Count && a[x].Key == b[y].Key) { x++; y++; }
                else if (x < a.Count && (y == b.Count || lcs[(x + 1) * width + y] >= lcs[x * width + y + 1])) Add(left, a[x++].Raw);
                else Add(right, b[y++].Raw);
                if (left.Count + (long)right.Count > limits.MaxTotalSpans - spans) { over = true; break; }
            }
            if (over) Fallback("Total span budget exceeded");
            else { spans += left.Count + right.Count; rows.Add(new(left.AsReadOnly(), right.AsReadOnly())); }
            void Fallback(string why) { fallbacks++; rows.Add(new(Whole(row.Left), Whole(row.Right), true, why)); }
        }
        token.ThrowIfCancellationRequested();
        return new(rows.AsReadOnly(), fallbacks, used);
    }

    static void Add(List<TextSpan> spans, IReadOnlyList<TextSpan> raw)
    {
        foreach (var span in raw)
        {
            if (spans.Count > 0 && spans[^1].Start + spans[^1].Length == span.Start) spans[^1] = new(spans[^1].Start, spans[^1].Length + span.Length);
            else spans.Add(span);
        }
    }

    static bool NormalizedEqual(string left, string right, CompareOptions options, CancellationToken token)
    {
        int x = 0, y = 0;
        while (true)
        {
            int a = Next(left, ref x), b = Next(right, ref y);
            if (a != b) return false;
            if (a == -1) return true;
        }
        int Next(string text, ref int offset)
        {
            while (offset < text.Length)
            {
                token.ThrowIfCancellationRequested();
                if (options.IgnoreWhitespace && char.IsWhiteSpace(text[offset])) { offset++; continue; }
                if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var rune, out int consumed) != System.Buffers.OperationStatus.Done) return -2 - text[offset++];
                offset += consumed;
                return (options.IgnoreCase ? Rune.ToUpperInvariant(rune) : rune).Value;
            }
            return -1;
        }
    }

    readonly record struct SourceRange(int NormalizedStart, int NormalizedLength, TextSpan Raw);

    static List<Part> Tokenize(string text, CompareOptions options, HighlightGranularity granularity, CancellationToken token, int maxTokens, int maxSpans, out string? reason)
    {
        reason = null;
        var result = new List<Part>();
        string normalized = text;
        List<SourceRange>? source = null;
        if (options.IgnoreWhitespace)
        {
            var key = new StringBuilder(); source = new();
            for (int offset = 0; offset < text.Length;)
            {
                token.ThrowIfCancellationRequested();
                int length = StringInfo.GetNextTextElementLength(text.AsSpan(offset));
                int start = key.Length;
                for (int i = offset; i < offset + length; i++)
                {
                    if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                    if (!char.IsWhiteSpace(text[i])) key.Append(text[i]);
                }
                int retained = key.Length - start;
                if (retained > 0)
                {
                    var range = new SourceRange(start, retained, new(offset, length));
                    if (source.Count > 0 && source[^1].Raw.Start + source[^1].Raw.Length == offset && source[^1].Raw.Length == source[^1].NormalizedLength && retained == length)
                    {
                        var last = source[^1];
                        source[^1] = new(last.NormalizedStart, last.NormalizedLength + retained, new(last.Raw.Start, last.Raw.Length + length));
                    }
                    else
                    {
                        if (source.Count >= maxSpans) { reason = "Total span budget exceeded"; return result; }
                        source.Add(range);
                    }
                }
                offset += length;
            }
            normalized = key.ToString();
        }
        StringBuilder? groupKey = null; List<TextSpan>? groupRaw = null;
        int groupKind = -1, sourceIndex = 0, rawCount = 0;
        for (int offset = 0; offset < normalized.Length;)
        {
            token.ThrowIfCancellationRequested();
            int length = StringInfo.GetNextTextElementLength(normalized.AsSpan(offset));
            var value = normalized.AsSpan(offset, length);
            Rune.DecodeFromUtf16(value, out var rune, out _);
            var category = Rune.GetUnicodeCategory(rune);
            int kind = category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark ? 1 : char.IsWhiteSpace(value[0]) ? 2 : 0;
            if (groupKey != null && (granularity == HighlightGranularity.Character || kind == 0 || groupKind != kind))
            {
                Finish();
                groupKey = null;
            }
            if (groupKey == null)
            {
                if (result.Count >= maxTokens) { reason = "Token budget exceeded"; return result; }
                groupKey = new(); groupRaw = new(); groupKind = kind;
            }
            groupKey.Append(value);
            if (source == null)
            {
                if (!AppendRaw(new(offset, length))) { reason = "Total span budget exceeded"; return result; }
            }
            else
            {
                int end = offset + length;
                while (sourceIndex < source.Count && source[sourceIndex].NormalizedStart < end)
                {
                    token.ThrowIfCancellationRequested();
                    var range = source[sourceIndex];
                    int from = Math.Max(offset, range.NormalizedStart), to = Math.Min(end, range.NormalizedStart + range.NormalizedLength);
                    var span = range.Raw.Length == range.NormalizedLength ? new TextSpan(range.Raw.Start + from - range.NormalizedStart, to - from) : range.Raw;
                    if (!AppendRaw(span)) { reason = "Total span budget exceeded"; return result; }
                    if (range.NormalizedStart + range.NormalizedLength <= end) sourceIndex++; else break;
                }
            }
            offset += length;
        }
        if (groupKey != null) Finish();
        return result;

        void Finish()
        {
            string key = groupKey!.ToString();
            result.Add(new(options.IgnoreCase ? key.ToUpperInvariant() : key, groupRaw!));
        }
        bool AppendRaw(TextSpan span)
        {
            if (groupRaw!.Count > 0 && span.Start <= groupRaw[^1].Start + groupRaw[^1].Length)
            {
                var last = groupRaw[^1];
                groupRaw[^1] = new(last.Start, Math.Max(last.Start + last.Length, span.Start + span.Length) - last.Start);
                return true;
            }
            if (rawCount >= maxSpans) return false;
            groupRaw.Add(span); rawCount++;
            return true;
        }
    }
}


