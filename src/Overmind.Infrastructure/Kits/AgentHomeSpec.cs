using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Kits;

public sealed record AgentHomeSpec(
    string AgentName,
    AgentPlatform Platform,
    string[] ConfigDirCandidates,
    string EntryFileName);

public static class AgentHomeResolution
{
    public sealed record ResolvedHome(
        AgentPlatform Platform,
        string ConfigDir,
        string SkillsDir,
        string EntryFile);

    // env-based candidates ("$CODEX_HOME") resolve against that root; the skills dir then
    // keeps only the tail segment of the project-relative path ("skills"), because the env
    // root already points at the agent's own home root. Literal candidates resolve the
    // platforms.json path against $HOME.
    public static ResolvedHome? Resolve(
        AgentHomeSpec spec, PlatformDirectoryMap platforms, string homeRoot, IReadOnlyDictionary<string, string> env)
    {
        foreach (var rawCandidate in spec.ConfigDirCandidates)
        {
            var candidate = rawCandidate.Trim();
            string configDir;
            string skillsDir;
            if (candidate.StartsWith('$'))
            {
                var name = candidate[1..];
                var envRoot = env.GetValueOrDefault(name);
                if (string.IsNullOrWhiteSpace(envRoot) || !Directory.Exists(envRoot))
                {
                    continue;
                }

                configDir = envRoot;
                var projectSkillsDir = platforms.GetSkillsDir(spec.Platform);
                var tail = Path.GetFileName(projectSkillsDir.TrimEnd('/'));
                skillsDir = Path.Combine(envRoot, tail);
            }
            else
            {
                var expanded = ExpandHome(candidate, homeRoot);
                if (!Directory.Exists(expanded))
                {
                    continue;
                }

                configDir = expanded;
                var projectSkillsDir = platforms.GetSkillsDir(spec.Platform);
                skillsDir = Path.Combine(homeRoot, projectSkillsDir.Replace('/', Path.DirectorySeparatorChar));
            }

            return new ResolvedHome(spec.Platform, configDir, skillsDir, Path.Combine(configDir, spec.EntryFileName));
        }

        return null;
    }

    public static string ExpandHome(string path, string homeRoot) =>
        path == "~" ? homeRoot
            : path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(homeRoot, path[2..])
            : path;
}
