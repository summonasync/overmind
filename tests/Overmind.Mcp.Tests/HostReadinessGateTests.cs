using System.Diagnostics;
using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public sealed class HostReadinessGateTests
{
    [Fact]
    public async Task WaitUntilReady_HostReturnsSuccess_FirstAttempt_ReturnsTrue()
    {
        var handler = new SequenceHandler(_ => JsonResponse());
        var gate = new HostReadinessGate(Client(handler), TextWriter.Null);

        var ready = await gate.WaitUntilReadyAsync(
            TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        ready.ShouldBeTrue();
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task WaitUntilReady_HostUnavailableThenReady_RetriesUntilSuccess()
    {
        var handler = new SequenceHandler(call => call < 3 ? Unavailable() : JsonResponse());
        var gate = new HostReadinessGate(Client(handler), TextWriter.Null);

        var ready = await gate.WaitUntilReadyAsync(
            TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(10), CancellationToken.None);

        ready.ShouldBeTrue();
        handler.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task WaitUntilReady_HostNeverReady_GivesUpWithinBudget()
    {
        var handler = new SequenceHandler(_ => Unavailable());
        var gate = new HostReadinessGate(Client(handler), TextWriter.Null);
        var watch = Stopwatch.StartNew();

        var ready = await gate.WaitUntilReadyAsync(
            TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(50), CancellationToken.None);
        watch.Stop();

        ready.ShouldBeFalse();
        handler.Calls.ShouldBeGreaterThan(1);
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:4761") };

    private static HttpResponseMessage JsonResponse() =>
        new(HttpStatusCode.OK) { Content = new StringContent("""{"status":"ok"}""") };

    private static HttpResponseMessage Unavailable() => new(HttpStatusCode.ServiceUnavailable);

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            return Task.FromResult(respond(call));
        }
    }
}
