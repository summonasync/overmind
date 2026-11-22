using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Grimoire;

namespace Overmind.Host.Api;

public static class GrimoireApi
{
    public const int MaxBookNameLength = 100;
    public const int MaxChannelLength = 300;

    public static RouteGroupBuilder MapGrimoireApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");
        group.MapGet("/grimoire/status", GetStatus);
        group.MapPost("/grimoire/wire", Wire);
        group.MapPost("/grimoire/assisted-install", AssistedInstall);
        group.MapPost("/grimoire/disconnect", Disconnect);
        return group;
    }

    private static async Task<IResult> GetStatus(GrimoireIntegrationService service, CancellationToken ct) =>
        Results.Ok(ToResponse(await service.GetStatusAsync(ct)));

    private static async Task<IResult> Wire(
        GrimoireWireRequest request,
        GrimoireIntegrationService service,
        CancellationToken ct)
    {
        var bookName = request.BookName?.Trim() ?? string.Empty;
        if (bookName.Length == 0)
        {
            return Invalid(nameof(request.BookName), "Book name is required — the memory book is keyed to its owner.");
        }

        if (bookName.Length > MaxBookNameLength)
        {
            return Invalid(nameof(request.BookName), $"Must be at most {MaxBookNameLength} characters.");
        }

        var result = await service.WireAsync(bookName, request.OverridePath, ct);
        return Results.Ok(new GrimoireWireResponse(
            result.Success,
            result.Wired,
            result.Agents
                .Select(outcome => new GrimoireAgentOutcomeResponse(
                    outcome.Agent, outcome.Outcome, outcome.ConfigPath, outcome.Error))
                .ToList(),
            result.Error));
    }

    private static async Task<IResult> AssistedInstall(
        GrimoireAssistedInstallRequest? request,
        GrimoireIntegrationService service,
        CancellationToken ct)
    {
        if (request is null || !TryParseIntent(request.Intent, out var intent))
        {
            return HttpErrors.BadRequest("intent must be one of: manual, assisted.");
        }

        if (request.Channel is { Length: > MaxChannelLength })
        {
            return Invalid(nameof(request.Channel), $"Must be at most {MaxChannelLength} characters.");
        }

        var result = await service.AssistedInstallAsync(intent, request.Channel, ct);
        return Results.Ok(new GrimoireAssistedInstallResponse(
            result.Degraded,
            result.Success,
            result.Detection?.Status,
            result.Detection?.BinaryPath,
            result.Instructions,
            result.Error));
    }

    private static bool TryParseIntent(string? raw, out GrimoireInstallIntent intent)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "manual":
                intent = GrimoireInstallIntent.Manual;
                return true;
            case "assisted":
                intent = GrimoireInstallIntent.Assisted;
                return true;
            default:
                intent = GrimoireInstallIntent.Assisted;
                return false;
        }
    }

    private static async Task<IResult> Disconnect(GrimoireIntegrationService service, CancellationToken ct)
    {
        var result = await service.DisconnectAsync(ct);
        return Results.Ok(new GrimoireDisconnectResponse(result.Success, result.Wired, result.Error));
    }

    private static GrimoireStatusResponse ToResponse(GrimoireStatusSnapshot snapshot) =>
        new(
            snapshot.Detection.Status,
            snapshot.Detection.BinaryPath,
            snapshot.Detection.Version,
            snapshot.Detection.Port,
            snapshot.BookName,
            snapshot.OverridePath,
            snapshot.Wired,
            snapshot.InstallChannel);

    private static IResult Invalid(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
