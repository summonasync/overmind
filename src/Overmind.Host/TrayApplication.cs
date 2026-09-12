using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Overmind.Host;

public static class TrayApplication
{
    public static void Run(Uri openUri, Func<Task> quitAsync, CancellationToken shutdownToken)
    {
        TrayApp.OpenUri = openUri;
        TrayApp.QuitAsync = quitAsync;

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        AppBuilder.Configure<TrayApp>()
            .UsePlatformDetect()
            .SetupWithLifetime(lifetime);

        // Shutdown must run on the Avalonia UI thread; the token callback fires on a thread-pool
        // thread and a direct Shutdown() call deadlocks the Linux tray loop.
        using var registration = shutdownToken.Register(() =>
            Dispatcher.UIThread.Post(() => lifetime.Shutdown(0)));
        lifetime.Start([]);
    }
}
