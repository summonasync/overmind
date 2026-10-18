using System.Net;
using System.Text;

namespace Overmind.Api.Tests;

internal sealed record StubOvermindRequest(
    string Method,
    string Path,
    string? Query,
    string? SessionHeader,
    string? Body);

internal sealed class StubOvermindApi : HttpMessageHandler
{
    private readonly List<(string Method, string Path, Func<HttpResponseMessage, Task> Respond)> _routes = [];
    private readonly List<StubOvermindRequest> _requests = [];

    public IReadOnlyList<StubOvermindRequest> Requests => _requests;

    public StubOvermindApi On(string method, string path, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, path, (request) =>
        {
            request.StatusCode = status;
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        });

    public StubOvermindApi On(
        string method, string path, Action<HttpResponseMessage> respond)
    {
        _routes.Add((method.ToUpperInvariant(), path, (request) =>
        {
            respond(request);
            return Task.CompletedTask;
        }));
        return this;
    }

    public string? BodyOf(Func<StubOvermindRequest, bool> predicate) =>
        _requests.Where(predicate).Select(r => r.Body).FirstOrDefault(body => body is not null);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new StubOvermindRequest(
            request.Method.Method.ToUpperInvariant(),
            request.RequestUri!.AbsolutePath,
            string.IsNullOrEmpty(request.RequestUri.Query) ? null : request.RequestUri.Query.TrimStart('?'),
            request.Headers.TryGetValues("X-Overmind-Session", out var values)
                ? values.FirstOrDefault()
                : null,
            body));

        var route = _routes.FirstOrDefault(candidate =>
            candidate.Method == request.Method.Method.ToUpperInvariant()
            && candidate.Path == request.RequestUri!.AbsolutePath);
        if (route.Path is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(
                    "{\"title\":\"stub route not configured\"}", Encoding.UTF8, "application/json"),
            };
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        await route.Respond(response);
        return response;
    }
}
