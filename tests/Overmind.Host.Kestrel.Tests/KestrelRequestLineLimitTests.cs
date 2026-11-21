using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class KestrelRequestLineLimitTests(KestrelApiFactory factory)
{
    [Fact]
    public async Task OverLimitRequestLine_IsRefusedByTheDefaultKestrelLimit()
    {
        var overLimitTarget = "/api/" + new string('a', 9000);

        var response = await KestrelRawWire.SendAsync(
            new Uri(factory.BaseUrl), $"GET {overLimitTarget} HTTP/1.1");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestUriTooLong);
    }
}
