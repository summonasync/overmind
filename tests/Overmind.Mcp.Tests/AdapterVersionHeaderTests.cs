using System.Net;
using System.Reflection;
using System.Text;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

[Collection(nameof(ProcessEnvironment))]
public sealed class AdapterVersionHeaderTests : IDisposable
{
    private readonly List<(string? SessionHeader, string? VersionHeader)> Requests = [];

    public void Dispose()
    {
        AdapterSession.Id = null;
    }

    [Fact]
    public async Task Send_StampsVersionHeader_OnEveryCall_RegardlessOfSessionState()
    {
        var http = Client();

        AdapterSession.Id = null;
        await http.GetAsync("/api/health");

        AdapterSession.Id = Guid.NewGuid();
        await http.GetAsync("/api/health");

        Requests.Count.ShouldBe(2);
        Requests.ShouldAllBe(r => r.VersionHeader == AdapterVersion.Current);
    }

    [Fact]
    public async Task Send_SessionAndVersionHeaders_TravelTogether()
    {
        AdapterSession.Id = Guid.NewGuid();
        var http = Client();

        await http.GetAsync("/api/health");

        var (sessionHeader, versionHeader) = Requests.Single();
        sessionHeader.ShouldBe(AdapterSession.Id!.Value.ToString());
        versionHeader.ShouldBe(AdapterVersion.Current);
    }

    [Fact]
    public void Current_MatchesAssemblyInformationalVersion()
    {
        var expected = typeof(AdapterVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        AdapterVersion.Current.ShouldBe(expected);
        AdapterVersion.Current.ShouldNotBeNullOrWhiteSpace();
    }

    private HttpClient Client()
    {
        return new HttpClient(new AdapterSessionHandler(new RecordingHandler(Requests)))
        {
            BaseAddress = new Uri("http://127.0.0.1:4761"),
        };
    }

    private sealed class RecordingHandler(List<(string? SessionHeader, string? VersionHeader)> requests)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add((
                request.Headers.TryGetValues(AdapterSession.HeaderName, out var session)
                    ? session.Single()
                    : null,
                request.Headers.TryGetValues(AdapterVersion.HeaderName, out var version)
                    ? version.Single()
                    : null));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
