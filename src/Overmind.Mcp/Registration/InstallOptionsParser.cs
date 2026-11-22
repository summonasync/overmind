using System.Diagnostics.CodeAnalysis;

using Overmind.Core.Agents;

namespace Overmind.Mcp.Registration;

public static class InstallOptionsParser
{
    public const string UsageText = """
        usage: overmind-mcp install [--all] [--agents <csv>] [--project] [--onboard] [--profile <journal|planning>] [--remove] [--with-grimoire] [--remove-grimoire] [--dry-run]

          --all              register at all six agents, ignoring detection
          --agents <csv>     comma-separated agent subset; wins over --all
          --project          project scope (git root from cwd) instead of user scope
          --onboard          write the managed onboard block to the repo's agent instruction file
          --profile <name>   MCP profile: planning (default) or journal. journal registers the
                             overmind-journal entry whose env carries OVERMIND_MCP_PROFILE=journal,
                             so a client started from that entry cannot boot the planning surface
          --remove           remove the overmind registration instead of adding it
          --with-grimoire    also register the grimoire MCP entry (command `grimoire`, args ["mcp"])
          --remove-grimoire  remove only the grimoire MCP entry; overmind registrations stay untouched
          --dry-run          print the plan, write nothing

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
        var withGrimoire = false;
        var removeGrimoire = false;
        var dryRun = false;
        var profile = McpProfile.Planning;
        List<string>? agents = null;

        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith($"{McpProfileResolver.ArgumentName}=", StringComparison.Ordinal))
            {
                if (!TryParseProfile(
                        args[index][(McpProfileResolver.ArgumentName.Length + 1)..], out profile, out error))
                {
                    return false;
                }

                continue;
            }

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
                case "--with-grimoire":
                    withGrimoire = true;
                    break;
                case "--remove-grimoire":
                    removeGrimoire = true;
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
                case "--profile":
                    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"flag --profile requires a value ({McpProfileResolver.PlanningName} or {McpProfileResolver.JournalName}).";
                        return false;
                    }

                    index++;
                    if (!TryParseProfile(args[index], out profile, out error))
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

        if (withGrimoire && removeGrimoire)
        {
            error = "flag --with-grimoire is incompatible with --remove-grimoire.";
            return false;
        }

        if (withGrimoire && remove)
        {
            error = "flag --with-grimoire is incompatible with --remove.";
            return false;
        }

        if (remove && removeGrimoire)
        {
            error = "flag --remove-grimoire is incompatible with --remove.";
            return false;
        }

        options = new InstallOptions
        {
            All = all,
            Agents = agents ?? [],
            Project = project,
            Onboard = onboard,
            Remove = remove,
            Profile = profile,
            WithGrimoire = withGrimoire,
            RemoveGrimoire = removeGrimoire,
            DryRun = dryRun,
        };
        return true;
    }

    private static bool TryParseProfile(string value, out McpProfile profile, [NotNullWhen(false)] out string? error)
    {
        if (value.Equals(McpProfileResolver.PlanningName, StringComparison.OrdinalIgnoreCase))
        {
            profile = McpProfile.Planning;
            error = null;
            return true;
        }

        if (value.Equals(McpProfileResolver.JournalName, StringComparison.OrdinalIgnoreCase))
        {
            profile = McpProfile.Journal;
            error = null;
            return true;
        }

        profile = McpProfile.Planning;
        error = $"unknown profile '{value}'. valid profiles: {McpProfileResolver.PlanningName}, {McpProfileResolver.JournalName}.";
        return false;
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
