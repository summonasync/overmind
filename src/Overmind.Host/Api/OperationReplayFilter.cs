using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api;

public sealed class OperationReplayFilter : IEndpointFilter
{
    public const string HeaderName = "X-Operation-Id";
    public const string PendingItemKey = "Overmind:PendingOperationRecord";

    public static readonly OperationReplayFilter Instance = new();

    private OperationReplayFilter()
    {
    }

    public sealed record PendingOperation(
        Guid ProjectId, Guid OperationId, string RequestHash, ResponseCaptureStream Capture);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue(HeaderName, out var raw) || raw.Count == 0)
        {
            return await next(context);
        }

        if (!Guid.TryParse(raw[^1], out var operationId))
        {
            return PlanningErrors.Map(PlanningException.InvalidOperationId(raw.ToString()));
        }

        var projectId = await ResolveProjectAsync(http, http.RequestAborted);
        if (projectId is null)
        {
            return await next(context);
        }

        var store = http.RequestServices.GetRequiredService<PlanningOperationStore>();
        var requestHash = await HashRequestAsync(http.Request, http.RequestAborted);
        var existing = await store.FindAsync(projectId.Value, operationId, http.RequestAborted);
        if (existing is not null)
        {
            return existing.RequestHash == requestHash
                ? Replayed(existing)
                : PlanningErrors.Map(PlanningException.OperationIdConflict(operationId));
        }

        var capture = new ResponseCaptureStream(http.Response.Body);
        http.Response.Body = capture;
        http.Items[PendingItemKey] = new PendingOperation(projectId.Value, operationId, requestHash, capture);

        return await next(context);
    }

    private static async Task<Guid?> ResolveProjectAsync(HttpContext http, CancellationToken ct)
    {
        var route = http.Request.RouteValues;
        if (Guid.TryParse(route["projectId"]?.ToString(), out var projectId))
        {
            return projectId;
        }

        if (Guid.TryParse(route["id"]?.ToString(), out var artifactId))
        {
            var store = http.RequestServices.GetRequiredService<PlanningOperationStore>();
            return await store.ResolveProjectByArtifactAsync(artifactId, ct);
        }

        return null;
    }

    private static async Task<string> HashRequestAsync(HttpRequest request, CancellationToken ct)
    {
        request.Body.Position = 0;
        using var buffered = new MemoryStream();
        await request.Body.CopyToAsync(buffered, ct);
        request.Body.Position = 0;

        var prefix = Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path.Value}\n");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(prefix);
        sha.AppendData(buffered.GetBuffer(), 0, (int)buffered.Length);
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    private static IResult Replayed(PlanningOperationRecord record)
    {
        var body = record.ResponseBody;
        try
        {
            if (JsonNode.Parse(record.ResponseBody) is JsonObject payload)
            {
                payload["replayed"] = true;
                body = payload.ToJsonString();
            }
        }
        catch (JsonException)
        {
        }

        return Results.Content(body, "application/json", Encoding.UTF8, record.StatusCode);
    }
}
