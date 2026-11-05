namespace DiffBeacon.Core;

public sealed record ByteSample(long Offset, byte? Left, byte? Right);
public sealed record BinaryResult(long LeftLength, long RightLength, long DifferentBytes, long? FirstDifference, IReadOnlyList<ByteSample> Samples)
{
    public string Summary => $"{DifferentBytes:N0} Different Bytes · Left {LeftLength:N0} Bytes · Right {RightLength:N0} Bytes · First: {(FirstDifference.HasValue ? "0x" + FirstDifference.Value.ToString("X") : "None")} · Samples {Samples.Count:N0} (Maximum 512)";
}
public static class BinaryComparison
{
    public static BinaryResult Compare(string left, string right, CancellationToken token = default)
    {
        using var a = File.Open(left, FileMode.Open, FileAccess.Read, FileShare.Read); using var b = File.Open(right, FileMode.Open, FileAccess.Read, FileShare.Read);
        var am = File.GetLastWriteTimeUtc(left); var bm = File.GetLastWriteTimeUtc(right);
        long al = a.Length, bl = b.Length, offset = 0, differences = 0; long? first = null;
        byte[] ab = new byte[65536], bb = new byte[65536]; var samples = new List<ByteSample>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int ac = ReadBlock(a, ab), bc = ReadBlock(b, bb); if (ac == 0 && bc == 0) break;
            for (int i = 0; i < Math.Max(ac, bc); i++) if (i >= ac || i >= bc || ab[i] != bb[i]) { differences++; first ??= offset + i; if (samples.Count < 512) samples.Add(new(offset + i, i < ac ? ab[i] : null, i < bc ? bb[i] : null)); }
            offset += Math.Max(ac, bc);
        }
        if (a.Length != al || b.Length != bl || File.GetLastWriteTimeUtc(left) != am || File.GetLastWriteTimeUtc(right) != bm) throw new IOException("An input changed during comparison. Retry.");
        return new(al, bl, differences, first, samples);
    }
    static int ReadBlock(Stream input, byte[] buffer) { int total = 0, read; while (total < buffer.Length && (read = input.Read(buffer, total, buffer.Length - total)) != 0) total += read; return total; }
}
