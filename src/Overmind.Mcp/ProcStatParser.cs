namespace Overmind.Mcp;

internal static class ProcStatParser
{
    public static int? ParentProcessIdOf(string statLine)
    {
        var commEnd = statLine.LastIndexOf(')');
        if (commEnd < 0)
        {
            return null;
        }

        var fields = statLine[(commEnd + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields.Length > 1 && int.TryParse(fields[1], out var parentPid) && parentPid > 0
            ? parentPid
            : null;
    }
}
