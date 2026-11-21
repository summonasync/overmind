using Overmind.Core.Agents;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class AgentDetectorTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), $"overmind-detect-{Guid.NewGuid():N}");
    private readonly string _pathDir = Path.Combine(Path.GetTempPath(), $"overmind-detect-path-{Guid.NewGuid():N}");

    public AgentDetectorTests()
    {
        Directory.CreateDirectory(_home);
        Directory.CreateDirectory(_pathDir);
    }

    public void Dispose()
    {
        Directory.Delete(_home, recursive: true);
        Directory.Delete(_pathDir, recursive: true);
    }

    private AgentPathEnvironment Env(IReadOnlyDictionary<string, string>? extra = null)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["PATH"] = _pathDir };
        if (extra is not null)
        {
            foreach (var pair in extra)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        return new AgentPathEnvironment { HomeDirectory = _home, Variables = variables };
    }

    [Fact]
    public void NoCandidatesPresent_AgentNotDetected()
    {
        AgentDetector.IsDetected(AgentRegistry.Find("claude")!, Env()).ShouldBeFalse();
    }

    [Fact]
    public void ConfigDirExists_AgentDetected()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".claude"));

        AgentDetector.IsDetected(AgentRegistry.Find("claude")!, Env()).ShouldBeTrue();
    }

    [Fact]
    public void ConfigFileExists_AgentDetected()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".kimi-code"));
        File.WriteAllText(Path.Combine(_home, ".kimi-code", "mcp.json"), "{}");

        AgentDetector.IsDetected(AgentRegistry.Find("kimi-code")!, Env()).ShouldBeTrue();
    }

    [Fact]
    public void EnvOverrideDirExists_AgentDetectedWithoutDefaultDir()
    {
        var overrideDir = Path.Combine(Path.GetTempPath(), $"overmind-kimi-override-{Guid.NewGuid():N}");
        Directory.CreateDirectory(overrideDir);

        try
        {
            var detected = AgentDetector.IsDetected(
                AgentRegistry.Find("kimi-code")!,
                Env(new Dictionary<string, string> { ["KIMI_CODE_HOME"] = overrideDir }));

            detected.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(overrideDir, recursive: true);
        }
    }

    [Fact]
    public void EnvOverrideMissing_DefaultDirCandidateStillApplies()
    {
        Directory.CreateDirectory(Path.Combine(_home, ".codex"));

        var detected = AgentDetector.IsDetected(
            AgentRegistry.Find("codex")!,
            Env(new Dictionary<string, string> { ["CODEX_HOME"] = Path.Combine(_home, "does-not-exist") }));

        detected.ShouldBeTrue();
    }

    [Fact]
    public void BinaryOnPath_AgentDetectedWithoutAnyConfig()
    {
        File.WriteAllText(Path.Combine(_pathDir, "gemini"), "#!/bin/sh\n");

        AgentDetector.IsDetected(AgentRegistry.Find("gemini")!, Env()).ShouldBeTrue();
    }

    [Fact]
    public void OtherBinaryOnPath_DoesNotDetectAgent()
    {
        File.WriteAllText(Path.Combine(_pathDir, "unrelated-tool"), "#!/bin/sh\n");

        AgentDetector.IsDetected(AgentRegistry.Find("qwen")!, Env()).ShouldBeFalse();
    }

    [Fact]
    public void GeminiSettingsOnly_GeminiDetectedAndAntigravityNot()
    {
        var geminiDir = Path.Combine(_home, ".gemini");
        Directory.CreateDirectory(geminiDir);
        File.WriteAllText(Path.Combine(geminiDir, "settings.json"), "{}");

        AgentDetector.IsDetected(AgentRegistry.Find("gemini")!, Env()).ShouldBeTrue();
        AgentDetector.IsDetected(AgentRegistry.Find("antigravity")!, Env()).ShouldBeFalse();
    }

    [Fact]
    public void AntigravityConfigOnly_AntigravityDetected()
    {
        var antigravityDir = Path.Combine(_home, ".gemini", "config");
        Directory.CreateDirectory(antigravityDir);
        File.WriteAllText(Path.Combine(antigravityDir, "mcp_config.json"), "{}");

        AgentDetector.IsDetected(AgentRegistry.Find("antigravity")!, Env()).ShouldBeTrue();
    }

    [Fact]
    public void SharedGeminiDir_DoesNotConfuseAgents()
    {
        var geminiDir = Path.Combine(_home, ".gemini");
        Directory.CreateDirectory(Path.Combine(geminiDir, "config"));
        File.WriteAllText(Path.Combine(geminiDir, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(geminiDir, "config", "mcp_config.json"), "{}");

        AgentDetector.IsDetected(AgentRegistry.Find("gemini")!, Env()).ShouldBeTrue();
        AgentDetector.IsDetected(AgentRegistry.Find("antigravity")!, Env()).ShouldBeTrue();
    }
}
