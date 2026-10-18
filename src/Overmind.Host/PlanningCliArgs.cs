namespace Overmind.Host;

internal sealed class PlanningCliArgs
{
    private static readonly HashSet<string> BooleanFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--json",
        "--override-lane-check",
        "--untick",
    };

    private static readonly HashSet<string> SingleVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "queue",
        "current",
        "lanes",
        "claim",
        "unclaim",
        "materialization-status",
    };

    private PlanningCliArgs(
        string command,
        IReadOnlyList<string> positionals,
        IReadOnlyDictionary<string, string?> flags,
        List<string> problems)
    {
        Command = command;
        Positionals = positionals;
        Flags = flags;
        Problems = problems;
    }

    public string Command { get; }

    public IReadOnlyList<string> Positionals { get; }

    private IReadOnlyDictionary<string, string?> Flags { get; }

    public List<string> Problems { get; }

    public bool Json => Has("--json");

    public static bool IsRequested(string[] args) =>
        args.Length >= 1 && string.Equals(args[0], "planning", StringComparison.OrdinalIgnoreCase);

    public static PlanningCliArgs Parse(string[] args)
    {
        var tokens = new List<string>();
        var flags = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();

        for (var i = 1; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith('-'))
            {
                tokens.Add(token);
                continue;
            }

            if (BooleanFlags.Contains(token))
            {
                flags[token] = bool.TrueString;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                flags[token] = null;
                problems.Add($"{token} requires a value.");
                continue;
            }

            flags[token] = args[++i];
        }

        var verb = tokens.FirstOrDefault();
        if (verb is null)
        {
            return new PlanningCliArgs(string.Empty, [], flags, problems);
        }

        if (SingleVerbs.Contains(verb))
        {
            return new PlanningCliArgs(verb.ToLowerInvariant(), tokens.Skip(1).ToList(), flags, problems);
        }

        var sub = tokens.Skip(1).FirstOrDefault() ?? string.Empty;
        return new PlanningCliArgs($"{verb} {sub}".Trim().ToLowerInvariant(), tokens.Skip(2).ToList(), flags, problems);
    }

    public bool Has(string flag) => Flags.ContainsKey(flag);

    public string? Flag(string flag) =>
        Flags.TryGetValue(flag, out var value) ? value : null;

    public string? Require(string flag, string problem)
    {
        var value = Flag(flag);
        if (value is null)
        {
            Problems.Add(problem);
        }

        return value;
    }

    public string? RequirePositional(int index, string problem)
    {
        var value = Positionals.ElementAtOrDefault(index);
        if (string.IsNullOrWhiteSpace(value))
        {
            Problems.Add(problem);
            return null;
        }

        return value;
    }

    public Guid? ProjectId()
    {
        var raw = Flag("--project");
        if (raw is null)
        {
            Problems.Add("This command needs a project — pass --project <GUID> (see `planning projects list`).");
            return null;
        }

        if (Guid.TryParse(raw, out var id))
        {
            return id;
        }

        Problems.Add("--project expects a project GUID.");
        return null;
    }
}
