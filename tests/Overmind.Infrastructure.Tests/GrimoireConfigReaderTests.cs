using Overmind.Infrastructure.Grimoire;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class GrimoireConfigReaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"grimoire-cfg-{Guid.NewGuid():N}");

    public GrimoireConfigReaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Read_MissingFile_ReturnsNullPort()
    {
        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    [Fact]
    public void Read_ServePortPresent_ReturnsPort()
    {
        Write("""{ "serve": { "port": 4770 } }""");

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBe(4770);
    }

    [Fact]
    public void Read_MalformedJson_ReturnsNullPort()
    {
        Write("{ not json");

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    [Fact]
    public void Read_EmptyFile_ReturnsNullPort()
    {
        Write(string.Empty);

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    [Fact]
    public void Read_NonObjectRoot_ReturnsNullPort()
    {
        Write("[1, 2, 3]");

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    [Fact]
    public void Read_ServeSectionMissing_ReturnsNullPort()
    {
        Write("""{ "defaultTome": "personal" }""");

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    [Fact]
    public void Read_PortAsString_ReturnsNullPort()
    {
        Write("""{ "serve": { "port": "4770" } }""");

        var config = new GrimoireConfigReader(_directory).Read();

        config.Port.ShouldBeNull();
    }

    private void Write(string content) =>
        File.WriteAllText(Path.Combine(_directory, "config.json"), content);

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
