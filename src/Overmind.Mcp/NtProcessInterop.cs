using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Overmind.Mcp;

internal static class NtProcessInterop
{
    private const int ProcessBasicInformationClass = 0;

    public static int? TryGetParentProcessId(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return QueryParentProcessId(processId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public IntPtr AffinityMask;
        public IntPtr BasePriority;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    private static int? QueryParentProcessId(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var info = new ProcessBasicInformation();
        var status = NtQueryInformationProcess(
            process.Handle,
            ProcessBasicInformationClass,
            ref info,
            Marshal.SizeOf<ProcessBasicInformation>(),
            out _);

        if (status != 0)
        {
            return null;
        }

        return (long)info.InheritedFromUniqueProcessId is > 0 and <= int.MaxValue
            ? (int)info.InheritedFromUniqueProcessId
            : null;
    }
}
