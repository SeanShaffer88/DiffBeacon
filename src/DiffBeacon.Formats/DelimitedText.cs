using System.Text;

namespace DiffBeacon.Core;

public static class DelimitedText
{
    private const int MaxCharacters = 4_000_000;
    public static IReadOnlyList<string[]> Parse(string input, char delimiter = ',')
    {
        if (delimiter is '\r' or '\n' or '"' or '\0') throw new ArgumentException("Invalid separator.");
        if (input.Length > MaxCharacters) throw new InvalidDataException("Table input exceeds memory budget.");
        var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false, afterQuote = false; int cells = 0;
        void Field() { row.Add(field.ToString()); field.Clear(); afterQuote = false; if (++cells > 200_000) throw new InvalidDataException("Table exceeds 200,000 cells."); }
        void Row() { Field(); rows.Add(row.ToArray()); row.Clear(); if (rows.Count > 50_000) throw new InvalidDataException("Table exceeds 50,000 rows."); }
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (quoted) { if (c == '"') { if (i + 1 < input.Length && input[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; afterQuote = true; } } else field.Append(c); }
            else if (c == delimiter) Field();
            else if (c is '\r' or '\n') { if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++; Row(); }
            else if (c == '"') { if (field.Length > 0 || afterQuote) throw new InvalidDataException($"Unexpected quote at character {i + 1}."); quoted = true; }
            else { if (afterQuote) throw new InvalidDataException($"Unexpected character after closing quote at character {i + 1}."); field.Append(c); }
        }
        if (quoted) throw new InvalidDataException("Unclosed quoted field at end of table.");
        if (field.Length > 0 || row.Count > 0 || afterQuote || input.Length == 0 || input[^1] == delimiter) Row();
        return rows;
    }
}
