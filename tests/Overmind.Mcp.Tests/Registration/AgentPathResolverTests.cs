using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class AgentPathResolverTests
{
    private const string Home = "/home/testuser";

    private static AgentPathEnvironment Env(IReadOnlyDictionary<string, string>? variables = null) =>
        new() { HomeDirectory = Home, Variables = variables ?? new Dictionary<string, string>() };

    [Theory]
    [InlineData("kimi-code", "mcp.json")]
    [InlineData("claude", ".claude.json")]
    [InlineData("codex", "config.toml")]
    [InlineData("gemini", "settings.json")]
    [InlineData("qwen", "settings.json")]
    [InlineData("antigravity", "mcp_config.json")]
    public void ResolveUserConfigPath_ExpandsHome(string name, string fileName)
    {
        var agent = AgentRegistry.Find(name)!;

        var path = AgentPathResolver.ResolveUserConfigPath(agent, Env());

        path.ShouldBe(Path.Combine(Home, agent.DefaultUserConfigTemplate[2..].Replace('/', Path.DirectorySeparatorChar)));
        Path.GetFileName(path).ShouldBe(fileName);
    }

    [Fact]
    public void ResolveUserConfigPath_KimiCodeEnvOverride_Honored()
    {
        var agent = AgentRegistry.Find("kimi-code")!;
        var env = Env(new Dictionary<string, string> { ["KIMI_CODE_HOME"] = "/custom/kimi" });

        var path = AgentPathResolver.ResolveUserConfigPath(agent, env);

        path.ShouldBe(Path.Combine("/custom/kimi", "mcp.json"));
    }

    [Fact]
    public void ResolveUserConfigPath_CodexEnvOverride_Honored()
    {
        var agent = AgentRegistry.Find("codex")!;
        var env = Env(new Dictionary<string, string> { ["CODEX_HOME"] = "/custom/codex" });

        var path = AgentPathResolver.ResolveUserConfigPath(agent, env);

        path.ShouldBe(Path.Combine("/custom/codex", "config.toml"));
    }

    [Theory]
    [InlineData("KIMI_CODE_HOME")]
    [InlineData("CODEX_HOME")]
    public void ResolveUserConfigPath_EnvOverrideEmptyOrUnset_FallsBackToDefault(string variable)
    {
        var agent = AgentRegistry.All.Single(a => a.UserConfigEnvironmentVariable == variable);
        var env = Env(new Dictionary<string, string> { [variable] = "  " });

        var path = AgentPathResolver.ResolveUserConfigPath(agent, env);

        path.ShouldBe(Path.Combine(Home, agent.DefaultUserConfigTemplate[2..].Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void ResolveUserConfigPath_AgentWithoutOverride_IgnoresEnv()
    {
        var agent = AgentRegistry.Find("claude")!;
        var env = Env(new Dictionary<string, string> { ["KIMI_CODE_HOME"] = "/custom/kimi" });

        var path = AgentPathResolver.ResolveUserConfigPath(agent, env);

        path.ShouldBe(Path.Combine(Home, ".claude.json"));
    }

    [Theory]
    [InlineData("kimi-code", ".mcp.json")]
    [InlineData("claude", ".mcp.json")]
    [InlineData("codex", ".codex/config.toml")]
    [InlineData("gemini", ".gemini/settings.json")]
    [InlineData("qwen", ".qwen/settings.json")]
    [InlineData("antigravity", ".agents/mcp_config.json")]
    public void ResolveProjectConfigPath_CombinesGitRootAndRelativePath(string name, string relativePath)
    {
        var agent = AgentRegistry.Find(name)!;
        var root = Path.Combine(Path.GetTempPath(), "repo");

        var path = AgentPathResolver.ResolveProjectConfigPath(agent, root);

        path.ShouldBe(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void ExpandPath_TildeOnly_ReturnsHome()
    {
        AgentPathResolver.ExpandPath("~", Env()).ShouldBe(Home);
    }

    [Fact]
    public void ExpandPath_UnknownTemplate_ReturnedAsIs()
    {
        AgentPathResolver.ExpandPath("/abs/path/file.json", Env()).ShouldBe("/abs/path/file.json");
    }

    [Fact]
    public void ExpandPath_DollarVariable_ExpandsWhenSet()
    {
        var env = Env(new Dictionary<string, string> { ["MY_VAR"] = "/var/root" });

        AgentPathResolver.ExpandPath("$MY_VAR/sub/dir", env).ShouldBe(Path.Combine("/var/root", "sub/dir"));
    }

    [Fact]
    public void ExpandPath_DollarVariableMissing_ReturnsNull()
    {
        AgentPathResolver.ExpandPath("$MISSING_VAR/sub", Env()).ShouldBeNull();
    }

    [Fact]
    public void ExpandPath_PercentVariable_ExpandsWhenSet()
    {
        var env = Env(new Dictionary<string, string> { ["USERPROFILE"] = @"C:\Users\test" });

        AgentPathResolver.ExpandPath(@"%USERPROFILE%\.claude.json", env).ShouldBe(Path.Combine(@"C:\Users\test", ".claude.json"));
    }

    [Fact]
    public void ExpandPath_PercentVariableMissing_ReturnsNull()
    {
        AgentPathResolver.ExpandPath(@"%NOPE%\file.json", Env()).ShouldBeNull();
    }
}
