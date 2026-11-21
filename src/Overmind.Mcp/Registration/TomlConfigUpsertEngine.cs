using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public sealed class TomlConfigUpsertEngine : IConfigUpsertEngine
{
    private const string ServersTableName = "mcp_servers";
    private const string CommandKey = "command";
    private const string ArgsKey = "args";

    private readonly ConfigBackupTracker _backupTracker;

    public TomlConfigUpsertEngine(ConfigBackupTracker backupTracker)
    {
        _backupTracker = backupTracker;
    }

    public AgentConfigFormat Format => AgentConfigFormat.Toml;

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
        if (!TryLoadModel(content, out var model, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var result = ApplyUpsert(model!, commandPath, key);
        if (result.Outcome is ConfigUpsertOutcome.Unchanged or ConfigUpsertOutcome.Failed || dryRun)
        {
            return result;
        }

        return await SaveAsync(filePath, model!, result, cancellationToken);
    }

    public async Task<ConfigUpsertResult> RemoveAsync(
        string filePath,
        bool dryRun,
        CancellationToken cancellationToken,
        ManagedServerKey key = ManagedServerKey.Overmind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var content = await ReadAsync(filePath, cancellationToken);
        if (!TryLoadModel(content, out var model, out var error))
        {
            return ConfigUpsertResult.Failure(error!);
        }

        var result = ApplyRemove(model!, key);
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

    private static ConfigUpsertResult ApplyUpsert(TomlTable model, string commandPath, ManagedServerKey key)
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

        var keyName = ManagedServerKeys.Name(key);
        if (servers.TryGetValue(keyName, out var existingEntry)
            && existingEntry is TomlTable existingTable
            && MatchesDesiredEntry(existingTable, key, commandPath))
        {
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Unchanged);
        }

        var outcome = servers.ContainsKey(keyName) ? ConfigUpsertOutcome.Updated : ConfigUpsertOutcome.Added;
        servers[keyName] = BuildDesiredEntry(key, commandPath);
        return ConfigUpsertResult.Success(outcome);
    }

    private static ConfigUpsertResult ApplyRemove(TomlTable model, ManagedServerKey key)
    {
        if (model.TryGetValue(ServersTableName, out var serversValue) && serversValue is TomlTable servers)
        {
            var keyName = ManagedServerKeys.Name(key);
            if (!servers.ContainsKey(keyName))
            {
                return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
            }

            servers.Remove(keyName);
            return ConfigUpsertResult.Success(ConfigUpsertOutcome.Removed);
        }

        return ConfigUpsertResult.Success(ConfigUpsertOutcome.NotPresent);
    }

    private static TomlTable BuildDesiredEntry(ManagedServerKey key, string commandPath)
    {
        if (key == ManagedServerKey.Overmind)
        {
            return new TomlTable { [CommandKey] = commandPath };
        }

        var args = new TomlArray();
        foreach (var arg in ManagedServerKeys.GrimoireArgs)
        {
            args.Add(arg);
        }

        return new TomlTable
        {
            [CommandKey] = ManagedServerKeys.GrimoireCommand,
            [ArgsKey] = args,
        };
    }

    private static bool MatchesDesiredEntry(TomlTable existing, ManagedServerKey key, string commandPath)
    {
        var desired = BuildDesiredEntry(key, commandPath);
        if (existing.Count != desired.Count)
        {
            return false;
        }

        foreach (var pair in desired)
        {
            if (!existing.TryGetValue(pair.Key, out var value))
            {
                return false;
            }

            if (pair.Value is TomlArray desiredArgs)
            {
                if (value is not TomlArray args || args.Count != desiredArgs.Count)
                {
                    return false;
                }

                for (var index = 0; index < desiredArgs.Count; index++)
                {
                    if (!string.Equals(args[index] as string, desiredArgs[index] as string, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }
            else if (!string.Equals(value as string, pair.Value as string, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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
