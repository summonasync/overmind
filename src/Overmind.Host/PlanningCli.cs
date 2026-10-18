using System.Text.Json;

namespace Overmind.Host;

internal static class PlanningCli
{
    public static bool IsRequested(string[] args) => PlanningCliArgs.IsRequested(args);

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var baseUrl = PlanningCliBaseUrl.Resolve();
        using var http = PlanningCliClient.CreateHttpClient(baseUrl);
        var client = new PlanningCliClient(http, baseUrl);
        return await ExecuteAsync(
            args, client, PlanningCliOptions.FromEnvironment(), Console.Out, Console.Error, ct);
    }

    internal static async Task<int> ExecuteAsync(
        string[] args,
        PlanningCliClient client,
        PlanningCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        var parsed = PlanningCliArgs.Parse(args);
        var run = new PlanningCliRun(parsed, client, new PlanningCliSession(client, options), output, error);

        if (!PlanningCliCommands.IsKnown(parsed.Command))
        {
            return await run.UnknownAsync();
        }

        try
        {
            Func<PlanningCliRun, CancellationToken, Task<int>> handler =
                PlanningCliCommands.IsDraftMutation(parsed.Command)
                    ? PlanningCliWriteCommands.RunAsync
                    : PlanningCliReadCommands.RunAsync;
            return await handler(run, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PlanningCliUnreachableException ex)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (PlanningCliApiException ex)
        {
            await error.WriteLineAsync(ex.Message);
            return 1;
        }
        catch (JsonException ex)
        {
            await error.WriteLineAsync($"overmind host returned malformed JSON: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"planning '{parsed.Command}' failed: {ex.Message}");
            return 1;
        }
    }
}
