using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Overmind.Host;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PortSelectionTests
{
    [Fact]
    public void ResolvePreferredPort_ReturnsPreferred_WhenFree()
    {
        var freePort = ReserveEphemeralPort();

        OvermindPorts.ResolvePreferredPort(freePort).ShouldBe(freePort);
    }

    [Fact]
    public void ResolvePreferredPort_FallsBackToZero_WhenTaken()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var takenPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            OvermindPorts.ResolvePreferredPort(takenPort).ShouldBe(0);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static int ReserveEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}

public sealed class PortFileTests
{
    [Fact]
    public async Task Startup_WritesBoundPortToPortFile()
    {
        var portFilePath = Path.Combine(Path.GetTempPath(), $"overmind-port-{Guid.NewGuid():N}.txt");
        var port = ReserveFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            Args = [],
        });
        builder.Services.AddOvermind();
        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        await using var app = builder.Build();
        app.MapProjectsApi();
        app.MapEntriesApi();
        app.MapSearchApi();
        app.MapHealthApi();

        await app.StartAsync();
        try
        {
            PortFile.Write(app, portFilePath);

            File.Exists(portFilePath).ShouldBeTrue();
            var content = (await File.ReadAllTextAsync(portFilePath)).Trim();
            int.Parse(content).ShouldBe(port);

            using var client = new HttpClient();
            using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/api/health"));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await app.StopAsync();
            if (File.Exists(portFilePath))
            {
                File.Delete(portFilePath);
            }
        }
    }

    private static int ReserveFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
