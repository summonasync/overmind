using System.Text.Json;
using Overmind.Mcp.ToolBinding;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class ToolBindingValidatorTests
{
    private static readonly JsonElement Schema = Parse(
        """
        {
          "type": "object",
          "properties": {
            "kind": {"type": "string", "enum": ["issue", "request"]},
            "count": {"type": "integer"},
            "tags": {"type": "array", "items": {"type": "string"}},
            "payload": {"type": "object"},
            "ratio": {"type": ["number", "null"]},
            "id": {"type": "string", "format": "uuid"},
            "note": {"type": "string"}
          },
          "required": ["id", "kind"]
        }
        """);

    [Fact]
    public void UnknownKey_IsReportedWithTheOffendingName()
    {
        var violation = ToolBindingValidator.Find(
            Args("kinds", "issue"), ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("kinds");
    }

    [Fact]
    public void MissingRequired_IsReportedBeforeOptionalGaps()
    {
        var violation = ToolBindingValidator.Find(Args("note", "hi"), ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("id");
    }

    [Fact]
    public void WrongJsonKind_IsReportedWithExpectedType()
    {
        var violation = ToolBindingValidator.Find(
            Args("id", Uuid, "kind", "issue", "payload", "not-an-object"),
            ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("payload");
        violation.Problem.ShouldContain("expects object");
        violation.Problem.ShouldContain("received string");
    }

    [Fact]
    public void NonUuidString_IsReportedAsUuidMismatch()
    {
        var violation = ToolBindingValidator.Find(
            Args("id", "nope", "kind", "issue"), ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("id");
        violation.Problem.ShouldBe("expects a UUID string");
    }

    [Fact]
    public void FractionalNumber_IsReportedAsIntegerMismatch()
    {
        var violation = ToolBindingValidator.Find(
            Args("id", Uuid, "kind", "issue", "count", 3.5), ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("count");
        violation.Problem.ShouldBe("expects an integer");
    }

    [Fact]
    public void CleanArguments_ProduceNoViolation()
    {
        var violation = ToolBindingValidator.Find(
            Args("id", Uuid, "kind", "issue", "ratio", null, "count", 2),
            ToolContractParser.Parse(Schema));

        violation.ShouldBeNull();
    }

    [Fact]
    public void ExplicitNull_OptionalParameter_IsAccepted()
    {
        var violation = ToolBindingValidator.Find(
            Args("id", Uuid, "kind", "issue", "note", null), ToolContractParser.Parse(Schema));

        violation.ShouldBeNull();
    }

    [Fact]
    public void EmptyArguments_MissingRequiredIsReported()
    {
        var violation = ToolBindingValidator.Find(null, ToolContractParser.Parse(Schema));

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("id");
    }

    [Fact]
    public void SchemaWithoutProperties_AcceptsAnything()
    {
        var open = Parse("""{"type": "object"}""");

        ToolBindingValidator.Find(Args("anything", "goes"), ToolContractParser.Parse(open)).ShouldBeNull();
    }

    // 266/T3 row 1 — the near-miss halves of the same door: a name that is close to one the tool
    // declares, and a required key whose value arrived under a near name.
    [Theory]
    [InlineData("room", "rooms")]
    [InlineData("count2", "count")]
    [InlineData("project_id", "projectId")]
    [InlineData("entryIdd", "entryId")]
    public void UnknownName_WithOneNearCandidate_NamesIt(string sent, string expected)
    {
        var contract = ToolContractParser.Parse(NearSchema(expected));

        var violation = ToolBindingValidator.Find(Args(sent, "x"), contract);

        violation.ShouldNotBeNull();
        violation.Problem.ShouldBe($"is not a parameter of this tool; did you mean '{expected}'?");
    }

    [Fact]
    public void UnknownName_WithTwoEquallyNear_IsRefusedWithoutAGuess()
    {
        var contract = ToolContractParser.Parse(Parse(
            """{"type":"object","properties":{"artifactId":{"type":"string"},"projectId":{"type":"string"}}}"""));

        var violation = ToolBindingValidator.Find(Args("id", "x"), contract);

        violation.ShouldNotBeNull();
        violation.Problem.ShouldBe("is not a parameter of this tool");
    }

    [Fact]
    public void MissingRequired_WithTheValueUnderANearName_PointsBothWays()
    {
        var contract = ToolContractParser.Parse(Parse("""
            {"type":"object",
             "properties":{"note":{"type":"string"},"logNote":{"type":"string"},"alias":{"type":"string"}},
             "required":["note"]}
            """));

        var violation = ToolBindingValidator.Find(
            Args("alias", "demo-feature/T1", "logNote", "mislabelled"), contract);

        violation.ShouldNotBeNull();
        violation.Parameter.ShouldBe("note");
        violation.Problem.ShouldBe(
            "is required but was not provided — the call carries 'logNote' instead; put the value under 'note'");
    }

    [Fact]
    public void MissingRequired_WithNothingNear_StaysPlain()
    {
        var contract = ToolContractParser.Parse(Parse("""
            {"type":"object","properties":{"note":{"type":"string"},"alias":{"type":"string"}},"required":["note"]}
            """));

        var violation = ToolBindingValidator.Find(Args("alias", "demo-feature/T1"), contract);

        violation.ShouldNotBeNull();
        violation.Problem.ShouldBe("is required but was not provided");
    }

    private static JsonElement NearSchema(string real) => Parse(
        """{"type":"object","properties":{"X":{"type":"string"},"payload":{"type":"string"}}}""".Replace("X", real));

    private const string Uuid = "11111111-1111-1111-1111-111111111111";

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    private static Dictionary<string, JsonElement> Args(params object?[] pairs)
    {
        var arguments = new Dictionary<string, JsonElement>();
        for (var i = 0; i < pairs.Length; i += 2)
        {
            arguments.Add((string)pairs[i]!, JsonSerializer.SerializeToElement(pairs[i + 1]));
        }

        return arguments;
    }
}
