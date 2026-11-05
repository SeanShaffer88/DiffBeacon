namespace DiffBeacon.Core;

public sealed class BlockMergePlan
{
    internal string LeftSnapshot { get; }
    internal string RightSnapshot { get; }
    public bool ToRight { get; }
    /// <summary>Zero-based logical-line coordinates. A zero count represents an absent span.</summary>
    public int SourceStart { get; }
    public int SourceCount { get; }
    public int DestinationStart { get; }
    public int DestinationCount { get; }
    public int FirstAlignedRow { get; }
    public int AlignedRowCount { get; }
    public string ProposedOutput { get; }
    internal BlockMergePlan(string left, string right, bool toRight, int sourceStart, int sourceCount, int destinationStart, int destinationCount, int firstRow, int rowCount, string output)
    { LeftSnapshot = left; RightSnapshot = right; ToRight = toRight; SourceStart = sourceStart; SourceCount = sourceCount; DestinationStart = destinationStart; DestinationCount = destinationCount; FirstAlignedRow = firstRow; AlignedRowCount = rowCount; ProposedOutput = output; }
}

public static class AlignedBlockMerge
{
    public static BlockMergePlan Plan(string left, string right, TextResult result, CompareOptions options, int differenceOrdinal, bool toRight, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(options);
        if (options.IgnoreCase || options.IgnoreWhitespace || options.IgnoreBlankLines) throw new InvalidOperationException("Block merge requires all ignore options to be off.");
        if (result.PositionalFallback) throw new InvalidOperationException("Block merge requires exact alignment; positional fallback is read-only for blocks.");
        if (differenceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(differenceOrdinal));
        // Validate against the current raw snapshots. A result produced with other inputs/options,
        // modified rows, or missing/error rows cannot be used to construct a write proposal.
        var fresh = TextComparison.Compare(left, right, new(), token);
        if (fresh.PositionalFallback || result.Rows.Count != fresh.Rows.Count) throw new InvalidOperationException("The comparison is stale or does not provide strict aligned rows.");
        int selected = -1, ordinal = 0;
        for (int i = 0; i < fresh.Rows.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            if (result.Rows[i].Status == Change.Error || result.Rows[i] != fresh.Rows[i]) throw new InvalidOperationException("The comparison is stale or contains invalid/error rows.");
            if (fresh.Rows[i].Status != Change.Equal && ordinal++ == differenceOrdinal) selected = i;
        }
        if (selected < 0) throw new ArgumentOutOfRangeException(nameof(differenceOrdinal), "Select a current differing row before merging a block.");
        int first = selected, end = selected + 1;
        while (first > 0 && fresh.Rows[first - 1].Status != Change.Equal) first--;
        while (end < fresh.Rows.Count && fresh.Rows[end].Status != Change.Equal) end++;
        int sourceStart = 0, destinationStart = 0, sourceCount = 0, destinationCount = 0;
        for (int i = 0; i < end; i++)
        {
            token.ThrowIfCancellationRequested(); var row = fresh.Rows[i];
            bool source = (toRight ? row.LeftLine : row.RightLine).HasValue;
            bool destination = (toRight ? row.RightLine : row.LeftLine).HasValue;
            if (i < first) { if (source) sourceStart++; if (destination) destinationStart++; }
            else { if (source) sourceCount++; if (destination) destinationCount++; }
        }
        var sourceLines = TextComparison.Lines(toRight ? left : right); var destinationLines = TextComparison.Lines(toRight ? right : left);
        int outputLineCount = checked(destinationLines.Length - destinationCount + sourceCount);
        // Joining zero tokens represents the empty editor, whose logical split still has one token.
        int canonicalOutputLines = Math.Max(1, outputLineCount);
        if (sourceLines.Length + (long)canonicalOutputLines > TextComparison.MaxLines) throw new InvalidDataException("Block merge output exceeds the combined logical-line budget.");
        long characters = Math.Max(0L, outputLineCount - 1L);
        for (int i = 0; i < destinationLines.Length; i++) { token.ThrowIfCancellationRequested(); if (i < destinationStart || i >= destinationStart + destinationCount) characters = checked(characters + destinationLines[i].Length); }
        for (int i = sourceStart; i < sourceStart + sourceCount; i++) { token.ThrowIfCancellationRequested(); characters = checked(characters + sourceLines[i].Length); }
        string sourceSnapshot = toRight ? left : right;
        if (characters > TextComparison.MaxCharacters / 2) throw new InvalidDataException("Block merge output exceeds the 2 million character destination-editor limit.");
        if (characters > TextComparison.MaxCharacters || characters + sourceSnapshot.Length > TextComparison.MaxCharacters) throw new InvalidDataException("Block merge output exceeds the combined character budget.");
        var output = new List<string>(outputLineCount);
        output.AddRange(destinationLines.Take(destinationStart)); output.AddRange(sourceLines.Skip(sourceStart).Take(sourceCount)); output.AddRange(destinationLines.Skip(destinationStart + destinationCount));
        string proposed = string.Join('\n', output);
        string destinationSnapshot = toRight ? right : left;
        if (string.Equals(proposed, destinationSnapshot, StringComparison.Ordinal)) throw new InvalidOperationException("The selected block would not change the destination.");
        token.ThrowIfCancellationRequested();
        return new(left, right, toRight, sourceStart, sourceCount, destinationStart, destinationCount, first, end - first, proposed);
    }
    public static string Apply(BlockMergePlan plan, string currentLeft, string currentRight, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan); token.ThrowIfCancellationRequested();
        if (!string.Equals(plan.LeftSnapshot, currentLeft, StringComparison.Ordinal) || !string.Equals(plan.RightSnapshot, currentRight, StringComparison.Ordinal)) throw new InvalidOperationException("Editor contents changed after planning. Compare again before merging.");
        return plan.ProposedOutput;
    }
}
