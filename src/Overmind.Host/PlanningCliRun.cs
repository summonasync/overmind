using System.Text.Json;

namespace Overmind.Host;

internal sealed class PlanningCliRun(
    PlanningCliArgs args,
    PlanningCliClient client,
    PlanningCliSession sessions,
    TextWriter output,
    TextWriter error)
{
    private const string ArtifactAddressProblem =
        "This command addresses an artifact by GUID or alias — pass <slug>/S, <slug>/P, <slug>/R, <slug>/T<order> or a GUID.";

    public PlanningCliArgs Args { get; } = args;

    public PlanningCliClient Client { get; } = client;

    public PlanningCliSession Sessions { get; } = sessions;

    public TextWriter Output { get; } = output;

    public TextWriter Error { get; } = error;

    public bool Failed => Args.Problems.Count > 0;

    public async Task<int> ReportUsageAsync()
    {
        foreach (var problem in Args.Problems)
        {
            await Error.WriteLineAsync(problem);
        }

        return 2;
    }

    public async Task<int> UnknownAsync()
    {
        await Error.WriteLineAsync(string.IsNullOrEmpty(Args.Command)
            ? "No planning command given."
            : $"Unknown planning command '{Args.Command}'.");
        await Error.WriteAsync(PlanningCliCommands.Usage);
        return 2;
    }

    public async Task<Guid?> AddressArtifactAsync(CancellationToken ct)
    {
        var selector = Args.RequirePositional(0, ArtifactAddressProblem);
        var project = Args.ProjectId();
        if (Failed || selector is null || project is null)
        {
            return null;
        }

        if (Guid.TryParse(selector, out var direct))
        {
            return direct;
        }

        if (!PlanningCliArtifactAlias.TryParse(selector, out var alias))
        {
            Args.Problems.Add(
                $"'{selector}' is neither an artifact GUID nor an alias — use <slug>/S, /P, /R, /T<order>.");
            return null;
        }

        return await PlanningCliArtifacts.ResolveAsync(Client, project.Value, alias, selector, ct);
    }

    public async Task WriteAsync(PlanningCliResponse response, Func<JsonElement, string> render, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(response.Body))
        {
            return;
        }

        if (Args.Json)
        {
            await Output.WriteAsync(response.Body.AsMemory(), ct);
            if (!response.Body.EndsWith('\n'))
            {
                await Output.WriteAsync("\n".AsMemory(), ct);
            }

            return;
        }

        using var document = JsonDocument.Parse(response.Body);
        await Output.WriteAsync(render(document.RootElement).AsMemory(), ct);
    }
}
