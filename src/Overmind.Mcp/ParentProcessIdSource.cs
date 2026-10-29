namespace Overmind.Mcp;

internal sealed class ParentProcessIdSource : IParentProcessIdSource
{
    public int? Resolve()
    {
        if (OperatingSystem.IsWindows())
        {
            return NtProcessInterop.TryGetParentProcessId(Environment.ProcessId);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            return FromProcStat();
        }

        return null;
    }

    private static int? FromProcStat()
    {
        try
        {
            return ProcStatParser.ParentProcessIdOf(
                File.ReadAllText($"/proc/{Environment.ProcessId}/stat"));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
