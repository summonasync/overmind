using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

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

        using var registration = shutdownToken.Register(() => lifetime.Shutdown(0));
        lifetime.Start([]);
    }
}
