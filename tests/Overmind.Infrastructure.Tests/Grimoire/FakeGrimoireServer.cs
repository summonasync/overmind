using System.Net;
using System.Net.Http;
using System.Text;

namespace Overmind.Infrastructure.Tests.Grimoire;

public sealed class FakeGrimoireServer : HttpMessageHandler
{
    public sealed record Captured(HttpMethod Method, string Path, string Query, string Body);

    public List<Captured> Requests { get; } = [];
    public HttpStatusCode HealthStatus { get; set; } = HttpStatusCode.OK;
    public HttpStatusCode RememberStatus { get; set; } = HttpStatusCode.Created;
    public bool CandidatesEndpointExists { get; set; } = true;
    public string MemoryItemsJson { get; set; } =
        """{"id":"11111111-1111-1111-1111-111111111111","content":"crew ships on branches","kind":"fact","tags":[],"createdAt":"2026-11-01T10:00:00Z","lastAccessedAt":"2026-11-01T10:00:00Z","actor":{"alias":"billy-herrington","vendor":null}}""";
    public string CandidateGroupsJson { get; set; } = "";
    public Guid? NextRememberId { get; set; }
    public Guid LastRememberId { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        Requests.Add(new Captured(
            request.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, body));

        return Task.FromResult(Route(request.Method, request.RequestUri.AbsolutePath));
    }

    public Captured Single(HttpMethod method, string path) =>
        Requests.Single(request => request.Method == method && request.Path == path);

    private HttpResponseMessage Route(HttpMethod method, string path)
    {
        if (method == HttpMethod.Get && path == "/health")
        {
            return Json(HealthStatus, "{}");
        }

        if (method == HttpMethod.Post && path == "/api/memories")
        {
            LastRememberId = NextRememberId ?? Guid.NewGuid();
            return Json(RememberStatus, $$"""{"record":{"id":"{{LastRememberId}}"},"tome":"crew","supersededId":null}""");
        }

        if (method == HttpMethod.Get && path == "/api/memories")
        {
            return Json(HttpStatusCode.OK, $$"""{"tome":"crew","count":1,"items":[{{MemoryItemsJson}}]}""");
        }

        if (method == HttpMethod.Post && path.StartsWith("/api/memories/") && path.EndsWith("/forget"))
        {
            return Json(HttpStatusCode.OK, "{}");
        }

        if (method == HttpMethod.Get && path == "/api/compaction/candidates")
        {
            return CandidatesEndpointExists
                ? Json(HttpStatusCode.OK,
                    $$"""{"tome":"crew","groups":[{{CandidateGroupsJson}}],"candidateCount":0}""")
                : Json(HttpStatusCode.NotFound,
                    """{"error":{"code":"not_found","message":"No endpoint for '/api/compaction/candidates'."}}""");
        }

        return Json(HttpStatusCode.NotFound, """{"error":{"code":"not_found","message":"No endpoint."}}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string payload) => new(status)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };
}
