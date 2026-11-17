using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

internal static class DescriptionBudgets
{
    public const int ToolDescriptionMax = 400;
    public const int ParameterDescriptionMax = 120;
    public const int PerToolTotalMax = 800;
    public const int RepoTotalMax = 24_000;
}

public class DescriptionBudgetLintTests
{
    internal const string DietPendingCategory = "description-diet-expected-red";

    [Fact]
    public void Scan_SeesTheWholeToolSurfaceOnce()
    {
        var snapshot = ToolDescriptionScanner.Scan();

        snapshot.ToolTypes.Count
            .ShouldBe(ToolDescriptionScanner.ExpectedToolTypeCount,
                $"the scan walked {snapshot.ToolTypes.Count} [McpServerToolType] types: {string.Join(", ", snapshot.ToolTypes)}");

        var frozenInventory = ToolDescriptionScanner.ExpectedToolNames
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        snapshot.Tools.Select(t => t.ToolName)
            .ToList()
            .ShouldBe(frozenInventory,
                "the tool surface moved — budgets and the diet baseline are counted over this exact inventory " +
                "(membership is guarded by Scan_ReportsMissingAndDuplicatedToolsByName, not by position)");
    }

    [Fact]
    public void Scan_CoversEveryRegisteredToolType()
    {
        var registrations = ToolRegistrationScan.RegisteredToolTypeNames();
        var scannedTypes = ToolDescriptionScanner.Scan().ToolTypes;

        registrations.Count
            .ShouldBe(ToolDescriptionScanner.ExpectedToolTypeCount,
                $"the adapter registers {registrations.Count} tool types through WithTools<T>() " +
                $"while the lint expects {ToolDescriptionScanner.ExpectedToolTypeCount} — " +
                $"the parse itself is suspect: [{string.Join(", ", registrations)}]");

        registrations.Except(scannedTypes, StringComparer.Ordinal).ToList()
            .ShouldBeEmpty(
                "registered via WithTools<T>() but never scanned — the SDK issues these tools and the lint " +
                "would silently under-count their descriptions on the wire");
    }

    [Fact]
    public void Scan_HasNoNestedSchemaDescriptions()
    {
        var surface = SchemaDescriptionScan.Measure(typeof(WaveRoleTools).Assembly);

        surface.PropertiesScanned.ShouldBeGreaterThan(0,
            "the nested-description guard walked no properties, so its verdict says nothing about the assembly");

        surface.ParametersScanned.ShouldBeGreaterThan(0,
            "the nested-description guard walked no tool parameters, so its verdict says nothing about the schema");

        var nested = surface.PropertyDescriptions.Concat(surface.SchemaTypedParameters).ToList();
        nested.ShouldBeEmpty(
            "the lint counts tool and parameter descriptions while scripts/tool-desc-diet/measure_wire.py drains " +
            "every description anywhere in inputSchema — these instruments are NO LONGER EQUIVALENT, extend the " +
            "counter before the repo budget means anything. Nested sources: " + string.Join("; ", nested));
    }

    [Fact]
    public void Scan_ReportsMissingAndDuplicatedToolsByName()
    {
        var scanned = ToolDescriptionScanner.Scan().Tools.Select(t => t.ToolName).ToList();
        var frozen = ToolDescriptionScanner.ExpectedToolNames;

        scanned
            .GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} declared {g.Count()} times")
            .ToList()
            .ShouldBeEmpty("a tool name can only be issued once per adapter");

        scanned.Except(frozen, StringComparer.Ordinal).ToList()
            .ShouldBeEmpty($"not in the frozen inventory of {frozen.Length} tools");

        frozen.Except(scanned, StringComparer.Ordinal).ToList()
            .ShouldBeEmpty("the scan never saw these");
    }

    [Fact]
    [Trait("Category", DietPendingCategory)]
    public void Budgets_Descriptions_FitAllFourCeilings()
    {
        var snapshot = ToolDescriptionScanner.Scan();

        snapshot.Tools.Count
            .ShouldBe(ToolDescriptionScanner.ExpectedToolNames.Length,
                "the budget report would be vacuous — the scan did not see the full tool surface; run Scan_SeesTheWholeToolSurfaceOnce first");

        DescriptionDietReport.ViolatedBudgets(snapshot)
            .ShouldBeEmpty(DescriptionDietReport.Violations(snapshot));
    }
}
