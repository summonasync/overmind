using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class ApiClientTimeoutClassificationTests
{
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private static async Task<ApiUnreachableException> SendAndCatchAsync(HttpMessageHandler handler)
    {
        var client = new OvermindApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = OvermindApiClient.Timeout
        });

        return await Should.ThrowAsync<ApiUnreachableException>(
            () => client.GetAsync("/api/projects", CancellationToken.None));
    }

    [Fact]
    public async Task ClientTimeout_NeverInvitesABlindRetry()
    {
        var ex = await SendAndCatchAsync(new ThrowingHandler(new TaskCanceledException()));

        ex.Message.ShouldContain("client timeout");
        ex.Message.ShouldContain("MAY have committed");
        ex.Message.ShouldContain("read the state back before retrying");
        ex.Message.ShouldNotContain(OvermindApiClient.RetryableMarker);
        ex.Message.ShouldNotContain("unreachable");
    }

    [Fact]
    public async Task ConnectionRefused_KeepsTheRetryableUnreachableText()
    {
        var ex = await SendAndCatchAsync(new ThrowingHandler(
            new HttpRequestException("Connection refused")));

        ex.Message.ShouldContain(OvermindApiClient.RetryableMarker);
        ex.Message.ShouldContain("unreachable");
        ex.Message.ShouldNotContain("client timeout");
    }
}
