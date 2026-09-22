using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Overmind.Host;

[SupportedOSPlatform("windows")]
internal static partial class ConsoleAttach
{
    private const uint ParentProcess = 0xFFFFFFFF;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint dwProcessId);

    public static void TryAttachParent()
    {
        if (Console.IsOutputRedirected || Console.IsErrorRedirected)
        {
            return;
        }

        if (!AttachConsole(ParentProcess))
        {
            return;
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }
}
