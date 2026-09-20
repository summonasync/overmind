using System.Net.Http;
using System.Net.Sockets;
using Overmind.Infrastructure.Http;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LoopbackHttpTests
{
    [Theory]
    [InlineData("http://localhost:6334")]
    [InlineData("http://127.0.0.1:6334")]
    [InlineData("http://127.42.0.9:6334")]
    [InlineData("http://[::1]:6334")]
    [InlineData("https://LOCALHOST:6334")]
    public void IsLoopback_True_ForLoopbackHosts(string url)
    {
        LoopbackHttp.IsLoopback(new Uri(url)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("http://qdrant.internal:6334")]
    [InlineData("https://api.openai.com")]
    [InlineData("http://10.0.0.5:6334")]
    [InlineData("http://[::2]:6334")]
    public void IsLoopback_False_ForRemoteHosts(string url)
    {
        LoopbackHttp.IsLoopback(new Uri(url)).ShouldBeFalse();
    }

    [Fact]
    public void CreateHandler_Loopback_DisablesProxy()
    {
        var handler = LoopbackHttp.CreateHandler(new Uri("http://127.0.0.1:11434"));

        var sockets = handler.ShouldBeOfType<SocketsHttpHandler>();
        sockets.UseProxy.ShouldBeFalse();
    }

    [Fact]
    public void CreateHandler_Remote_KeepsSystemProxy()
    {
        var handler = LoopbackHttp.CreateHandler(new Uri("https://api.openai.com"));

        var defaultHandler = handler.ShouldBeOfType<HttpClientHandler>();
        defaultHandler.UseProxy.ShouldBeTrue();
    }

    [Fact]
    public void CreateHandler_InvalidBaseUrl_FallsBackToSystemProxy()
    {
        var handler = LoopbackHttp.CreateHandler("not a url");

        handler.ShouldBeOfType<HttpClientHandler>();
    }
}
