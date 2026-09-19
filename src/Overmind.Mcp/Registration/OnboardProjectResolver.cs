namespace Overmind.Mcp.Registration;

public sealed class OnboardProjectResolver
{
    private readonly OvermindApiClient _api;

    public OnboardProjectResolver(OvermindApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    public static OnboardProjectResolver FromEnvironment() =>
        new(new OvermindApiClient(OvermindApiClient.CreateHttpClient(ApiBaseUrl.Resolve())));

    public async Task<OnboardProjectInfo?> ResolveAsync(string repoRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);

        try
        {
            var payload = await _api.PostAsync("/api/onboard", new { cwd = repoRoot }, cancellationToken);
            var projectId = payload.GetProperty("projectId").GetString();
            var projectName = payload.GetProperty("projectName").GetString();

            return string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(projectName)
                ? null
                : new OnboardProjectInfo { ProjectId = projectId, ProjectName = projectName };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
