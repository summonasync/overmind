namespace Overmind.Host.Api.Contracts;

public sealed record CeremonyResponse(
    Guid ProjectId,
    string ProjectName,
    string FeatureSlug,
    string? Number,
    string Title,
    string[] Reasons,
    string? Url);

public sealed record CeremoniesResponse(CeremonyResponse[] Features);
