using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class AgentRegistryTests
{
    [Fact]
    public void All_ContainsExactlySixAgentsWithDistinctNames()
    {
        AgentRegistry.All.Count.ShouldBe(6);
        AgentRegistry.All.Select(agent => agent.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(6);
    }

    [Theory]
    [InlineData("kimi-code", AgentConfigFormat.Json, "~/.kimi-code/mcp.json", ".mcp.json")]
    [InlineData("claude", AgentConfigFormat.Json, "~/.claude.json", ".mcp.json")]
    [InlineData("codex", AgentConfigFormat.Toml, "~/.codex/config.toml", ".codex/config.toml")]
    [InlineData("gemini", AgentConfigFormat.Json, "~/.gemini/settings.json", ".gemini/settings.json")]
    [InlineData("qwen", AgentConfigFormat.Json, "~/.qwen/settings.json", ".qwen/settings.json")]
    [InlineData("antigravity", AgentConfigFormat.Json, "~/.gemini/config/mcp_config.json", ".agents/mcp_config.json")]
    public void Agent_HasSpecPathsAndFormat(string name, AgentConfigFormat format, string userTemplate, string projectRelativePath)
    {
        var agent = AgentRegistry.Find(name);

        agent.ShouldNotBeNull();
        agent.ConfigFormat.ShouldBe(format);
        agent.DefaultUserConfigTemplate.ShouldBe(userTemplate);
        agent.ProjectRelativePath.ShouldBe(projectRelativePath);
    }

    [Theory]
    [InlineData("kimi-code", "KIMI_CODE_HOME", "mcp.json")]
    [InlineData("codex", "CODEX_HOME", "config.toml")]
    public void Agent_HasEnvOverride(string name, string variable, string fileName)
    {
        var agent = AgentRegistry.Find(name)!;

        agent.UserConfigEnvironmentVariable.ShouldBe(variable);
        agent.UserConfigEnvironmentFileName.ShouldBe(fileName);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("gemini")]
    [InlineData("qwen")]
    [InlineData("antigravity")]
    public void Agent_WithoutEnvOverride_HasNullOverride(string name)
    {
        var agent = AgentRegistry.Find(name)!;

        agent.UserConfigEnvironmentVariable.ShouldBeNull();
        agent.UserConfigEnvironmentFileName.ShouldBeNull();
    }

    [Theory]
    [InlineData("kimi-code", "AGENTS.md")]
    [InlineData("claude", "CLAUDE.md")]
    [InlineData("codex", "AGENTS.md")]
    [InlineData("gemini", "GEMINI.md")]
    [InlineData("qwen", "QWEN.md")]
    [InlineData("antigravity", "AGENTS.md")]
    public void Agent_HasAgentFileCandidates(string name, string fileName)
    {
        var agent = AgentRegistry.Find(name)!;

        agent.AgentFileCandidates.ShouldBe([fileName]);
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        AgentRegistry.Find("KIMI-CODE").ShouldNotBeNull();
        AgentRegistry.Find("Claude").ShouldNotBeNull();
    }

    [Fact]
    public void Find_UnknownName_ReturnsNull()
    {
        AgentRegistry.Find("unknown-agent").ShouldBeNull();
    }
}
