using System.Text.Json;
using System.Text.Json.Nodes;

using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public sealed class JsonConfigUpsertEngine : IConfigUpsertEngine
{
    private const string ServersPropertyName = "mcpServers";
    private const string CommandPropertyName = "command";
    private const string ArgsPropertyName = "args";
    private const int DefaultIndentSize = 2;

    private readonly ConfigBackupTracker _backupTracker;

    public JsonConfigUpsertEngine(ConfigBackupTracker backupTracker)
    {
        _backupTracker = backupTracker;
    }

    public AgentConfigFormat Format => AgentConfigFormat.Json;

    public async Task<ConfigUpsertResult> UpsertAsync(
        string filePath,
        string commandPath,
        bool dryRun,
        CancellationToken cancellationToken,
        ManagedServerKey key = ManagedServerKey.Overmind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);

        var content = await ReadAsync(filePath, cancellationToken);
        if (!TryParseRoot(content, out var root, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var servers = EnsureServersObject(root!, out error);
        if (servers is null)
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var keyName = ManagedServerKeys.Name(key);
        var desired = BuildDesiredEntry(key, commandPath);
        if (JsonNode.DeepEquals(servers[keyName], desired))
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Unchanged);
        }

        var outcome = servers[keyName] is null ? ConfigUpsertOutcome.Added : ConfigUpsertOutcome.Updated;
        if (dryRun)
        {
            return ConfigUpsertResult.Success(outcome);
        }

        servers[keyName] = desired;
        var rendered = JsonConfigWriter.Write(root!, DetectIndentSize(content), DetectNewline(content));
        await AtomicConfigFileWriter.WriteAsync(filePath, rendered, _backupTracker, cancellationToken);
        return ConfigUpsertResult.Success(outcome);
    }

    public async Task<ConfigUpsertResult> RemoveAsync(
        string filePath,
        bool dryRun,
        CancellationToken cancellationToken,
        ManagedServerKey key = ManagedServerKey.Overmind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await ReadAsync(filePath, cancellationToken);
        if (content is null)
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
        }

        if (!TryParseRoot(content, out var root, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var keyName = ManagedServerKeys.Name(key);
        if (root![ServersPropertyName] is not JsonObject servers || servers[keyName] is null)
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
        }

        if (dryRun)
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Removed);
        }

        servers.Remove(keyName);
        var rendered = JsonConfigWriter.Write(root, DetectIndentSize(content), DetectNewline(content));
        await AtomicConfigFileWriter.WriteAsync(filePath, rendered, _backupTracker, cancellationToken);
        return ConfigUpsertResult.Success(ConfigUpsertOutcome.Removed);
    }

    private static JsonObject BuildDesiredEntry(ManagedServerKey key, string commandPath)
    {
        if (key == ManagedServerKey.Overmind)
        {
            return new JsonObject { [CommandPropertyName] = commandPath };
        }

        var args = new JsonArray();
        foreach (var arg in ManagedServerKeys.GrimoireArgs)
        {
            args.Add(arg);
        }

        return new JsonObject
        {
            [CommandPropertyName] = ManagedServerKeys.GrimoireCommand,
            [ArgsPropertyName] = args,
        };
    }

    private static async Task<string?> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static bool TryParseRoot(string? content, out JsonObject? root, out string? error)
    {
        root = null;
        error = null;

        if (content is null)
        {
            root = [];
            return true;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(content);
        }
        catch (JsonException exception)
        {
            error = $"Invalid JSON: {exception.Message}";
            return false;
        }

        if (node is not JsonObject jsonObject)
        {
            error = "Config root is not a JSON object.";
            return false;
        }

        root = jsonObject;
        return true;
    }

    private static JsonObject? EnsureServersObject(JsonObject root, out string? error)
    {
        error = null;

        if (root[ServersPropertyName] is null)
        {
            var created = new JsonObject();
            root[ServersPropertyName] = created;
            return created;
        }

        if (root[ServersPropertyName] is not JsonObject existing)
        {
            error = $"Property '{ServersPropertyName}' is not a JSON object.";
            return null;
        }

        return existing;
    }

    private static int DetectIndentSize(string? content)
    {
        if (content is null)
        {
            return DefaultIndentSize;
        }

        foreach (var line in content.Split('\n'))
        {
            if (line.Length >= 3 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ')
            {
                return 2;
            }
        }

        foreach (var line in content.Split('\n'))
        {
            if (line.Length >= 5 && line[4] != ' ' && line.StartsWith("    ", StringComparison.Ordinal))
            {
                return 4;
            }
        }

        return DefaultIndentSize;
    }

    private static string DetectNewline(string? content)
    {
        return content is not null && content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }
}
