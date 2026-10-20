namespace TaskTimer.Services;

public enum DiffKind { Same, Added, Removed }

/// <param name="OldNumber">1-based line number in the old file; null for added lines.</param>
/// <param name="NewNumber">1-based line number in the new file; null for removed lines.</param>
public sealed record DiffLine(DiffKind Kind, int? OldNumber, int? NewNumber, string Text);

/// <summary>Line-by-line comparison of two texts (Myers' algorithm), as shown in a code review.</summary>
public static class LineDiff
{
    /// <summary>Above this many lines (both sides together) the comparison is not attempted.</summary>
    public const int MaxLines = 60000;

    public static string[] SplitLines(string? text) =>
        string.IsNullOrEmpty(text) ? Array.Empty<string>() : text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    public static List<DiffLine> Compute(string[] oldLines, string[] newLines)
    {
        // Unchanged lines at the start and end are matched directly; only the middle is searched.
        var prefix = 0;
        while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix]) prefix++;
        var suffix = 0;
        while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix &&
               oldLines[^(suffix + 1)] == newLines[^(suffix + 1)]) suffix++;

        var result = new List<DiffLine>(Math.Max(oldLines.Length, newLines.Length));
        for (var i = 0; i < prefix; i++) result.Add(new DiffLine(DiffKind.Same, i + 1, i + 1, oldLines[i]));

        var a = oldLines[prefix..(oldLines.Length - suffix)];
        var b = newLines[prefix..(newLines.Length - suffix)];
        foreach (var (kind, x, y) in Myers(a, b))
        {
            var text = kind == DiffKind.Added ? b[y] : a[x];
            result.Add(new DiffLine(kind,
                kind == DiffKind.Added ? null : prefix + x + 1,
                kind == DiffKind.Removed ? null : prefix + y + 1,
                text));
        }

        for (var i = 0; i < suffix; i++)
        {
            var oldIndex = oldLines.Length - suffix + i;
            result.Add(new DiffLine(DiffKind.Same, oldIndex + 1, newLines.Length - suffix + i + 1, oldLines[oldIndex]));
        }
        return result;
    }

    /// <summary>The shortest edit script from a to b, in order; x/y index into a/b.</summary>
    private static List<(DiffKind Kind, int X, int Y)> Myers(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length, max = n + m;
        var steps = new List<(DiffKind, int, int)>();
        if (max == 0) return steps;

        var offset = max;
        var v = new int[2 * max + 2];
        // trace[d] holds v for diagonals -d..d as they were before step d.
        var trace = new List<int[]>();
        var found = false;
        for (var d = 0; d <= max && !found; d++)
        {
            trace.Add(v[(offset - d)..(offset + d + 1)]);
            for (var k = -d; k <= d; k += 2)
            {
                var x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[offset + k] = x;
                if (x >= n && y >= m) { found = true; break; }
            }
        }

        // Walk back from the end, collecting each step, then reverse.
        int cx = n, cy = m;
        for (var d = trace.Count - 1; d >= 0; d--)
        {
            var saved = trace[d];
            int V(int k) => saved[k + d];
            var kNow = cx - cy;
            int prevX, prevY;
            if (d == 0)
            {
                prevX = 0;
                prevY = 0;
            }
            else
            {
                var prevK = kNow == -d || (kNow != d && V(kNow - 1) < V(kNow + 1)) ? kNow + 1 : kNow - 1;
                prevX = V(prevK);
                prevY = prevX - prevK;
            }

            while (cx > prevX && cy > prevY)
            {
                cx--;
                cy--;
                steps.Add((DiffKind.Same, cx, cy));
            }
            if (d > 0)
            {
                if (cx == prevX) steps.Add((DiffKind.Added, cx, --cy));
                else steps.Add((DiffKind.Removed, --cx, cy));
            }
        }
        steps.Reverse();
        return steps;
    }
}
