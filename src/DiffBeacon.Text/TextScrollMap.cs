namespace DiffBeacon.Core;

public sealed class TextScrollMap
{
    readonly int[] leftToRight, rightToLeft;
    TextScrollMap(int[] left, int[] right) { leftToRight = left; rightToLeft = right; }
    public int MapLeftToRight(int zeroBasedLine) => leftToRight.Length == 0 ? 0 : leftToRight[Math.Clamp(zeroBasedLine, 0, leftToRight.Length - 1)];
    public int MapRightToLeft(int zeroBasedLine) => rightToLeft.Length == 0 ? 0 : rightToLeft[Math.Clamp(zeroBasedLine, 0, rightToLeft.Length - 1)];
    public static TextScrollMap Create(TextResult result, int leftLineCount, int rightLineCount)
    {
        if (leftLineCount < 0 || rightLineCount < 0) throw new ArgumentOutOfRangeException(nameof(leftLineCount));
        var anchors = result.Rows.Where(r => r.LeftLine.HasValue && r.RightLine.HasValue).Select(r => (Left: r.LeftLine!.Value - 1, Right: r.RightLine!.Value - 1)).ToArray();
        int[] Build(int count, int otherCount, bool reverse)
        {
            var map = new int[count]; int next = 0;
            for (int line = 0; line < count; line++)
            {
                int Source(int index) => reverse ? anchors[index].Right : anchors[index].Left;
                int Target(int index) => reverse ? anchors[index].Left : anchors[index].Right;
                while (next < anchors.Length && Source(next) < line) next++;
                int chosen = next == 0 ? 0 : next == anchors.Length ? next - 1 : line - Source(next - 1) <= Source(next) - line ? next - 1 : next;
                map[line] = anchors.Length == 0 ? 0 : Math.Clamp(Target(chosen), 0, Math.Max(0, otherCount - 1));
            }
            return map;
        }
        return new(Build(leftLineCount, rightLineCount, false), Build(rightLineCount, leftLineCount, true));
    }
}
