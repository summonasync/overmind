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
