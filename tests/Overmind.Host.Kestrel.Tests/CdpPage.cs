using System.Text.Json;

namespace Overmind.Host.Kestrel.Tests;

public sealed class CdpPage(CdpConnection connection)
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(30);

    private bool _enabled;

    public async Task EnableAsync(CancellationToken ct)
    {
        if (_enabled)
        {
            return;
        }

        await connection.SendAsync("Page.enable", null, ct);
        await connection.SendAsync("Runtime.enable", null, ct);
        _enabled = true;
    }

    public async Task NavigateAsync(string url, CancellationToken ct)
    {
        var before = connection.EventSequence;
        await connection.SendAsync("Page.navigate", new { url }, ct);
        await connection.WaitForEventAsync("Page.loadEventFired", before, LoadTimeout, ct);
    }

    public async Task<JsonElement> EvaluateAsync(string expression, CancellationToken ct)
    {
        var result = await connection.SendAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = true,
            awaitPromise = true,
        }, ct);

        if (result.TryGetProperty("exceptionDetails", out var exceptionDetails))
        {
            throw new InvalidOperationException(
                $"Runtime.evaluate threw: {DescribeException(exceptionDetails)}");
        }

        var remoteObject = result.GetProperty("result");
        return remoteObject.TryGetProperty("value", out var value)
            ? value
            : JsonDocument.Parse("null").RootElement;
    }

    public Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken ct) =>
        connection.SendAsync(method, parameters, ct);

    private static string DescribeException(JsonElement exceptionDetails) =>
        exceptionDetails.TryGetProperty("exception", out var exception)
        && exception.TryGetProperty("description", out var description)
            ? description.GetString() ?? "unknown runtime exception"
            : exceptionDetails.TryGetProperty("text", out var text)
                ? text.GetString() ?? "unknown runtime exception"
                : "unknown runtime exception";
}
