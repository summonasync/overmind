using System.Net;
using System.Net.Sockets;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly string _portFilePath =
        Path.Combine(Path.GetTempPath(), $"overmind-single-{Guid.NewGuid():N}.port");

    [Fact]
    public void TryAcquire_FailsWhileFirstLockHeld()
    {
        using var first = SingleInstance.TryAcquire();
        first.ShouldNotBeNull();

        IDisposable? second = null;
        var thread = new Thread(() => second = SingleInstance.TryAcquire());
        thread.Start();
        thread.Join();

        second.ShouldBeNull();
    }

    [Fact]
    public void TryAcquire_SucceedsAfterFirstLockReleased()
    {
        var first = SingleInstance.TryAcquire();
        first.ShouldNotBeNull();
        first.Dispose();

        using var second = SingleInstance.TryAcquire();
        second.ShouldNotBeNull();
    }

    [Fact]
    public void TryGetRunningInstanceUri_ReturnsNull_WhenPortFileMissing()
    {
        SingleInstance.TryGetRunningInstanceUri(_portFilePath).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-port")]
    [InlineData("0")]
    [InlineData("70000")]
    public void TryGetRunningInstanceUri_ReturnsNull_WhenPortFileInvalid(string content)
    {
        File.WriteAllText(_portFilePath, content);

        SingleInstance.TryGetRunningInstanceUri(_portFilePath).ShouldBeNull();
    }

    [Fact]
    public void TryGetRunningInstanceUri_ReturnsNull_WhenPortIsFree()
    {
        File.WriteAllText(_portFilePath, "47610");

        SingleInstance.TryGetRunningInstanceUri(_portFilePath).ShouldBeNull();
    }

    [Fact]
    public void TryGetRunningInstanceUri_ReturnsUri_WhenPortIsTaken()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            File.WriteAllText(_portFilePath, port.ToString());

            var uri = SingleInstance.TryGetRunningInstanceUri(_portFilePath);

            uri.ShouldNotBeNull();
            uri!.Port.ShouldBe(port);
            uri.Host.ShouldBe("127.0.0.1");
        }
        finally
        {
            listener.Stop();
        }
    }

    public void Dispose()
    {
        if (File.Exists(_portFilePath))
        {
            File.Delete(_portFilePath);
        }
    }
}
