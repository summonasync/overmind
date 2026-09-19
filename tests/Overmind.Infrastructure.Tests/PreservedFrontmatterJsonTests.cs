using Overmind.Infrastructure.Planning;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class PreservedFrontmatterJsonTests
{
    [Fact]
    public void Serialize_UnorderedKeys_OrdinalSortedStableBytes()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["risk_level"] = "high",
            ["Zulu"] = "1",
            ["depends_on"] = "02-other"
        };

        PreservedFrontmatterJson.Serialize(map).ShouldBe(
            "{\"Zulu\":\"1\",\"depends_on\":\"02-other\",\"risk_level\":\"high\"}");
    }

    [Fact]
    public void Serialize_NullMap_ReturnsNull()
    {
        PreservedFrontmatterJson.Serialize(null).ShouldBeNull();
    }

    [Fact]
    public void Deserialize_RoundTripsVerbatimValues()
    {
        const string json = "{\"depends_on\":\"02-other\",\"note\":\"keep: this\"}";

        var map = PreservedFrontmatterJson.Deserialize(json)!;

        map["depends_on"].ShouldBe("02-other");
        map["note"].ShouldBe("keep: this");
    }

    [Fact]
    public void Deserialize_NullJson_ReturnsNull()
    {
        PreservedFrontmatterJson.Deserialize(null).ShouldBeNull();
    }

    [Fact]
    public void Deserialize_CorruptJson_ReturnsNull()
    {
        PreservedFrontmatterJson.Deserialize("{unclosed").ShouldBeNull();
        PreservedFrontmatterJson.Deserialize("[\"not-a-map\"]").ShouldBeNull();
    }
}
