using System.Text;

namespace Overmind.Infrastructure.Planning;

internal static class PlanningUnifiedDiff
{
    private const int ContextLines = 2;

    private enum OpKind
    {
        Equal,
        Delete,
        Insert
    }

    private readonly record struct DiffOp(OpKind Kind, int FromLine, int ToLine);

    public static string Build(string fromBody, string toBody)
    {
        var from = SplitLines(fromBody);
        var to = SplitLines(toBody);

        var ops = BuildOps(from, to);
        if (ops.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();

        foreach (var (start, end) in HunkWindows(ops))
        {
            AppendHunk(lines, ops, start, end, from, to);
        }

        return string.Join("\n", lines);
    }

    internal static string[] SplitLines(string body)
    {
        if (body.Length == 0)
        {
            return [];
        }

        var trimmed = body.EndsWith('\n') ? body[..^1] : body;
        if (trimmed.Length == 0)
        {
            return [];
        }

        return trimmed
            .Split('\n')
            .Select(line => line.EndsWith('\r') ? line[..^1] : line)
            .ToArray();
    }

    private static List<DiffOp> BuildOps(string[] from, string[] to)
    {
        var prefix = CommonPrefixLength(from, to);
        var suffix = CommonSuffixLength(from, to, prefix);

        var ops = new List<DiffOp>(from.Length + to.Length);

        for (var i = 0; i < prefix; i++)
        {
            ops.Add(new DiffOp(OpKind.Equal, i, i));
        }

        AppendMyersOps(ops, from, to, prefix, from.Length - suffix, prefix, to.Length - suffix);

        var fromBase = from.Length - suffix;
        var toBase = to.Length - suffix;
        for (var i = 0; i < suffix; i++)
        {
            ops.Add(new DiffOp(OpKind.Equal, fromBase + i, toBase + i));
        }

        return ops;
    }

    private static int CommonPrefixLength(string[] from, string[] to)
    {
        var max = Math.Min(from.Length, to.Length);
        var length = 0;

        while (length < max && from[length] == to[length])
        {
            length++;
        }

        return length;
    }

    private static int CommonSuffixLength(string[] from, string[] to, int prefix)
    {
        var max = Math.Min(from.Length, to.Length) - prefix;
        var length = 0;

        while (length < max && from[from.Length - 1 - length] == to[to.Length - 1 - length])
        {
            length++;
        }

        return length;
    }

    private static void AppendMyersOps(
        List<DiffOp> ops, string[] from, string[] to,
        int fromStart, int fromEnd, int toStart, int toEnd)
    {
        var n = fromEnd - fromStart;
        var m = toEnd - toStart;

        if (n == 0)
        {
            for (var j = 0; j < m; j++)
            {
                ops.Add(new DiffOp(OpKind.Insert, fromEnd, toStart + j));
            }

            return;
        }

        if (m == 0)
        {
            for (var i = 0; i < n; i++)
            {
                ops.Add(new DiffOp(OpKind.Delete, fromStart + i, toEnd));
            }

            return;
        }

        var offset = n + m;
        var v = new int[2 * offset + 1];
        var trace = new List<int[]>();
        var dFound = -1;

        for (var d = 0; d <= offset && dFound < 0; d++)
        {
            trace.Add((int[])v.Clone());

            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[k - 1 + offset] < v[k + 1 + offset]))
                {
                    x = v[k + 1 + offset];
                }
                else
                {
                    x = v[k - 1 + offset] + 1;
                }

                var y = x - k;
                while (x < n && y < m && from[fromStart + x] == to[toStart + y])
                {
                    x++;
                    y++;
                }

                v[k + offset] = x;

                if (x >= n && y >= m)
                {
                    dFound = d;
                    break;
                }
            }
        }

        CollectBacktrack(ops, trace, dFound, offset, from, to, fromStart, toStart, n, m);
    }

    private static void CollectBacktrack(
        List<DiffOp> ops, List<int[]> trace, int dFound, int offset,
        string[] from, string[] to, int fromStart, int toStart, int n, int m)
    {
        var collected = new List<DiffOp>();
        var x = n;
        var y = m;

        for (var d = dFound; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;

            int prevK;
            if (k == -d || (k != d && v[k - 1 + offset] < v[k + 1 + offset]))
            {
                prevK = k + 1;
            }
            else
            {
                prevK = k - 1;
            }

            var prevX = v[prevK + offset];
            var prevY = prevX - prevK;

            while (x > prevX && y > prevY)
            {
                x--;
                y--;
                collected.Add(new DiffOp(OpKind.Equal, fromStart + x, toStart + y));
            }

            if (d > 0)
            {
                if (x == prevX)
                {
                    collected.Add(new DiffOp(OpKind.Insert, fromStart + prevX, toStart + prevY));
                    y = prevY;
                }
                else
                {
                    collected.Add(new DiffOp(OpKind.Delete, fromStart + prevX, toStart + prevY));
                    x = prevX;
                }
            }
        }

        collected.Reverse();
        ops.AddRange(collected);
    }

    private static List<(int Start, int End)> HunkWindows(List<DiffOp> ops)
    {
        var windows = new List<(int, int)>();
        var i = 0;

        while (i < ops.Count)
        {
            if (ops[i].Kind == OpKind.Equal)
            {
                i++;
                continue;
            }

            var start = i;
            var end = i + 1;

            while (end < ops.Count)
            {
                if (ops[end].Kind != OpKind.Equal)
                {
                    end++;
                    continue;
                }

                var j = end;
                while (j < ops.Count && ops[j].Kind == OpKind.Equal)
                {
                    j++;
                }

                if (j < ops.Count && j - end <= ContextLines * 2)
                {
                    end = j;
                    continue;
                }

                break;
            }

            var windowStart = start;
            var leading = 0;
            while (windowStart > 0 && ops[windowStart - 1].Kind == OpKind.Equal && leading < ContextLines)
            {
                windowStart--;
                leading++;
            }

            var windowEnd = end;
            var trailing = 0;
            while (windowEnd < ops.Count && ops[windowEnd].Kind == OpKind.Equal && trailing < ContextLines)
            {
                windowEnd++;
                trailing++;
            }

            windows.Add((windowStart, windowEnd));
            i = end;
        }

        return windows;
    }

    private static void AppendHunk(
        List<string> lines, List<DiffOp> ops, int start, int end, string[] from, string[] to)
    {
        var fromFirst = -1;
        var fromCount = 0;
        var toFirst = -1;
        var toCount = 0;

        for (var i = start; i < end; i++)
        {
            var op = ops[i];
            if (op.Kind != OpKind.Insert)
            {
                if (fromFirst < 0)
                {
                    fromFirst = op.FromLine + 1;
                }

                fromCount++;
            }

            if (op.Kind != OpKind.Delete)
            {
                if (toFirst < 0)
                {
                    toFirst = op.ToLine + 1;
                }

                toCount++;
            }
        }

        lines.Add($"@@ -{FormatRange(fromFirst, fromCount, ops[start].FromLine)} " +
            $"+{FormatRange(toFirst, toCount, ops[start].ToLine)} @@");

        for (var i = start; i < end; i++)
        {
            var op = ops[i];
            switch (op.Kind)
            {
                case OpKind.Equal:
                    lines.Add($" {from[op.FromLine]}");
                    break;
                case OpKind.Delete:
                    lines.Add($"-{from[op.FromLine]}");
                    break;
                case OpKind.Insert:
                    lines.Add($"+{to[op.ToLine]}");
                    break;
            }
        }
    }

    private static string FormatRange(int firstLine, int count, int fallbackPosition)
    {
        var resolved = firstLine < 0 ? fallbackPosition + 1 : firstLine;

        return count switch
        {
            0 => $"{resolved - 1},0",
            1 => $"{resolved}",
            _ => $"{resolved},{count}"
        };
    }
}
