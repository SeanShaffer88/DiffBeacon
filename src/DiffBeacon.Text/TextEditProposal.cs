namespace DiffBeacon.Core;

public static class TextEditProposal
{
    public static string Replace(string destination, int start, int length, string replacement, string otherText)
    {
        if (start < 0 || length < 0 || start > destination.Length - length) throw new ArgumentOutOfRangeException(nameof(start));
        long size = destination.Length - (long)length + replacement.Length;
        if (size > 2_000_000 || size + otherText.Length > TextComparison.MaxCharacters) throw new InvalidDataException("Merge output exceeds the character budget.");
        int lines = 2;
        bool previousCr = false;
        void CountLines(ReadOnlySpan<char> text)
        {
            foreach (char c in text)
            {
                if (c == '\r' || (c == '\n' && !previousCr)) lines++;
                previousCr = c == '\r';
                if (lines > TextComparison.MaxLines) throw new InvalidDataException("Merge output exceeds the combined logical-line budget.");
            }
        }
        CountLines(destination.AsSpan(0, start));
        CountLines(replacement);
        CountLines(destination.AsSpan(start + length));
        previousCr = false;
        CountLines(otherText);
        return string.Concat(destination.AsSpan(0, start), replacement, destination.AsSpan(start + length));
    }
}
