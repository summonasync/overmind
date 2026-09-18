using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace Overmind.Mcp.Registration;

public sealed class TomlConfigUpsertEngine : IConfigUpsertEngine
{
    private const string ServersTableName = "mcp_servers";
    private const string OvermindKey = "overmind";
    private const string CommandKey = "command";

    private readonly ConfigBackupTracker _backupTracker;

    public TomlConfigUpsertEngine(ConfigBackupTracker backupTracker)
    {
        _backupTracker = backupTracker;
    }

    public AgentConfigFormat Format => AgentConfigFormat.Toml;

    public async Task<ConfigUpsertResult> UpsertAsync(string filePath, string commandPath, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandPath);

        var content = await ReadAsync(filePath, cancellationToken);
        if (!TryLoadModel(content, out var model, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var result = ApplyUpsert(model!, commandPath);
        if (result.Outcome is ConfigUpsertOutcome.Unchanged or ConfigUpsertOutcome.Failed || dryRun)
        {
            return result;
        }

        return await SaveAsync(filePath, model!, result, cancellationToken);
    }

    public async Task<ConfigUpsertResult> RemoveAsync(string filePath, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await ReadAsync(filePath, cancellationToken);
        if (!TryLoadModel(content, out var model, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var result = ApplyRemove(model!);
        if (result.Outcome is ConfigUpsertOutcome.NotPresent or ConfigUpsertOutcome.Failed || dryRun)
        {
            return result;
        }

        return await SaveAsync(filePath, model!, result, cancellationToken);
    }

    private static async Task<string?> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static bool TryLoadModel(string? content, out TomlTable? model, out string? error)
    {
        model = null;
        error = null;

        if (content is null)
        {
            model = new TomlTable();
            return true;
        }

        var document = Toml.Parse(content);
        if (document.HasErrors)
        {
            var diagnostic = document.Diagnostics.FirstOrDefault(message => message.Kind == DiagnosticMessageKind.Error);
            error = $"Invalid TOML: {diagnostic?.Message ?? "unknown parse error"}";
            return false;
        }

        model = Toml.ToModel(document);
        return true;
    }

    private static ConfigUpsertResult ApplyUpsert(TomlTable model, string commandPath)
    {
        TomlTable servers;
        if (model.TryGetValue(ServersTableName, out var serversValue))
        {
            if (serversValue is not TomlTable existingServers)
            {
                return ConfigUpsertResult.Failure($"Table '{ServersTableName}' is not a TOML table.");
            }

            servers = existingServers;
        }
        else
        {
            servers = new TomlTable();
            model[ServersTableName] = servers;
        }

        if (servers.TryGetValue(OvermindKey, out var existingOvermind)
            && existingOvermind is TomlTable existingTable
            && existingTable.Count == 1
            && existingTable.TryGetValue(CommandKey, out var existingCommand)
            && existingCommand is string existingCommandText
            && string.Equals(existingCommandText, commandPath, StringComparison.Ordinal))
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Unchanged);
        }

        var outcome = servers.ContainsKey(OvermindKey) ? ConfigUpsertOutcome.Updated : ConfigUpsertOutcome.Added;
        servers[OvermindKey] = new TomlTable { [CommandKey] = commandPath };
        return ConfigUpsertResult.Success(outcome);
    }

    private static ConfigUpsertResult ApplyRemove(TomlTable model)
    {
        if (model.TryGetValue(ServersTableName, out var serversValue) && serversValue is TomlTable servers)
        {
            if (!servers.ContainsKey(OvermindKey))
            {
                return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
            }

            servers.Remove(OvermindKey);
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Removed);
        }

        return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
    }

    private async Task<ConfigUpsertResult> SaveAsync(string filePath, TomlTable model, ConfigUpsertResult result, CancellationToken cancellationToken)
    {
        string rendered;
        try
        {
            rendered = Toml.FromModel(model);
        }
        catch (TomlException exception)
        {
            return ConfigUpsertResult.Failure($"Could not serialize TOML: {exception.Message}");
        }

        await AtomicConfigFileWriter.WriteAsync(filePath, rendered, _backupTracker, cancellationToken);
        return result;
    }
}
