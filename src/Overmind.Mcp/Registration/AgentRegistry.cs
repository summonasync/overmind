namespace Overmind.Mcp.Registration;

public static class AgentRegistry
{
    public static IReadOnlyList<AgentDefinition> All { get; } =
    [
        new AgentDefinition
        {
            Name = "kimi-code",
            ConfigFormat = AgentConfigFormat.Json,
            DefaultUserConfigTemplate = "~/.kimi-code/mcp.json",
            UserConfigEnvironmentVariable = "KIMI_CODE_HOME",
            UserConfigEnvironmentFileName = "mcp.json",
            ProjectRelativePath = ".mcp.json",
            ConfigDirCandidates = ["$KIMI_CODE_HOME", "~/.kimi-code"],
            BinaryNames = ["kimi"],
            AgentFileCandidates = ["AGENTS.md"],
        },
        new AgentDefinition
        {
            Name = "claude",
            ConfigFormat = AgentConfigFormat.Json,
            DefaultUserConfigTemplate = "~/.claude.json",
            ProjectRelativePath = ".mcp.json",
            ConfigDirCandidates = ["~/.claude", "~/.claude.json"],
            BinaryNames = ["claude"],
            AgentFileCandidates = ["CLAUDE.md"],
        },
        new AgentDefinition
        {
            Name = "codex",
            ConfigFormat = AgentConfigFormat.Toml,
            DefaultUserConfigTemplate = "~/.codex/config.toml",
            UserConfigEnvironmentVariable = "CODEX_HOME",
            UserConfigEnvironmentFileName = "config.toml",
            ProjectRelativePath = ".codex/config.toml",
            ConfigDirCandidates = ["$CODEX_HOME", "~/.codex"],
            BinaryNames = ["codex"],
            AgentFileCandidates = ["AGENTS.md"],
        },
        new AgentDefinition
        {
            Name = "gemini",
            ConfigFormat = AgentConfigFormat.Json,
            DefaultUserConfigTemplate = "~/.gemini/settings.json",
            ProjectRelativePath = ".gemini/settings.json",
            ConfigDirCandidates = ["~/.gemini"],
            BinaryNames = ["gemini"],
            AgentFileCandidates = ["GEMINI.md"],
        },
        new AgentDefinition
        {
            Name = "qwen",
            ConfigFormat = AgentConfigFormat.Json,
            DefaultUserConfigTemplate = "~/.qwen/settings.json",
            ProjectRelativePath = ".qwen/settings.json",
            ConfigDirCandidates = ["~/.qwen"],
            BinaryNames = ["qwen"],
            AgentFileCandidates = ["QWEN.md"],
        },
        new AgentDefinition
        {
            Name = "antigravity",
            ConfigFormat = AgentConfigFormat.Json,
            DefaultUserConfigTemplate = "~/.gemini/config/mcp_config.json",
            ProjectRelativePath = ".agents/mcp_config.json",
            ConfigDirCandidates = ["~/.gemini/config"],
            BinaryNames = ["antigravity"],
            AgentFileCandidates = ["AGENTS.md"],
        },
    ];

    public static AgentDefinition? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return All.FirstOrDefault(agent => string.Equals(agent.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
