using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class BrowserUriTests
{
    [Theory]
    [InlineData("http://127.0.0.1:4761/", "http://overmind.localhost:4761/")]
    [InlineData("http://localhost:4761/", "http://overmind.localhost:4761/")]
    [InlineData("http://[::1]:4761/", "http://overmind.localhost:4761/")]
    public void FromLoopback_RewritesLoopbackHost(string bound, string expected)
    {
        BrowserUri.FromLoopback(new Uri(bound)).ShouldBe(new Uri(expected));
    }

    [Fact]
    public void FromLoopback_KeepsCustomHost()
    {
        var bound = new Uri("http://192.168.1.10:9000/");

        BrowserUri.FromLoopback(bound).ShouldBe(bound);
    }
}
