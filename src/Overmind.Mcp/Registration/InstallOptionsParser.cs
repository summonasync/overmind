using System.Diagnostics.CodeAnalysis;

namespace Overmind.Mcp.Registration;

public static class InstallOptionsParser
{
    public const string UsageText = """
        usage: overmind-mcp install [--all] [--agents <csv>] [--project] [--onboard] [--remove] [--dry-run]

          --all           register at all six agents, ignoring detection
          --agents <csv>  comma-separated agent subset; wins over --all
          --project       project scope (git root from cwd) instead of user scope
          --onboard       write the managed onboard block to the repo's agent instruction file
          --remove        remove the overmind registration instead of adding it
          --dry-run       print the plan, write nothing

        exit codes: 0 ok or nothing to do, 1 any failure, 2 usage error
        """;

    public static bool TryParse(string[] args, [NotNullWhen(true)] out InstallOptions? options, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = null;
        error = null;

        var all = false;
        var project = false;
        var onboard = false;
        var remove = false;
        var dryRun = false;
        List<string>? agents = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--all":
                    all = true;
                    break;
                case "--project":
                    project = true;
                    break;
                case "--onboard":
                    onboard = true;
                    break;
                case "--remove":
                    remove = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--agents":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = "flag --agents requires a comma-separated value.";
                        return false;
                    }

                    index++;
                    if (!TryParseAgents(args[index], out agents, out error))
                    {
                        return false;
                    }

                    break;
                default:
                    error = $"unknown flag '{args[index]}'.";
                    return false;
            }
        }

        if (onboard && remove)
        {
            error = "flag --onboard is incompatible with --remove.";
            return false;
        }

        options = new InstallOptions
        {
            All = all,
            Agents = agents ?? [],
            Project = project,
            Onboard = onboard,
            Remove = remove,
            DryRun = dryRun,
        };
        return true;
    }

    private static bool TryParseAgents(string csv, [NotNullWhen(true)] out List<string>? agents, [NotNullWhen(false)] out string? error)
    {
        agents = null;
        error = null;

        var names = csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            error = "flag --agents requires a non-empty comma-separated value.";
            return false;
        }

        foreach (var name in names)
        {
            if (AgentRegistry.Find(name) is null)
            {
                error = $"unknown agent '{name}'.";
                return false;
            }
        }

        agents = names;
        return true;
    }
}
