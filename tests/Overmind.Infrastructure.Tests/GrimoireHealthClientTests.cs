using System.Net;
using System.Net.Sockets;
using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireHealthClientTests
{
    [Fact]
    public async Task IsHealthyAsync_NoListenerOnPort_ReturnsFalse()
    {
        var client = new GrimoireHealthClient();

        var healthy = await client.IsHealthyAsync(FreePort(), CancellationToken.None);

        healthy.ShouldBeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ListenerNeverResponds_TimesOutToFalse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var client = new GrimoireHealthClient(
                new HttpClient { Timeout = TimeSpan.FromMilliseconds(200) });

            var healthy = await client.IsHealthyAsync(port, CancellationToken.None);

            healthy.ShouldBeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
