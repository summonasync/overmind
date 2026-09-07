using System.Diagnostics;

namespace Overmind.Host;

public static class SingleInstance
{
    private const string MutexNamePrefix = @"Local\Overmind.Host.";

    public static IDisposable? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, $"{MutexNamePrefix}{Environment.UserName}");
        var acquired = false;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (acquired)
        {
            return new InstanceLock(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public static Uri? TryGetRunningInstanceUri() =>
        TryGetRunningInstanceUri(OvermindPaths.PortFilePath);

    public static Uri? TryGetRunningInstanceUri(string portFilePath)
    {
        string text;
        try
        {
            text = File.ReadAllText(portFilePath).Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (!int.TryParse(text, out var port) || port is < 1 or > 65535)
        {
            return null;
        }

        return OvermindPorts.IsPortAvailable(port) ? null : new Uri($"http://127.0.0.1:{port}");
    }

    public static void OpenInDefaultBrowser(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private sealed class InstanceLock(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.Dispose();
        }
    }
}
