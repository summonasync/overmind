using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests;

public class JournalToolDocContractTests
{
    [Fact]
    public void Preamble_NamesTheProfilePinAndTheHumanDoorRule()
    {
        var preamble = ContractsDoc.Preamble();

        preamble.ShouldContain("OVERMIND_MCP_PROFILE=journal");
        preamble.ShouldContain("overmind-mcp install --profile journal");
        preamble.ShouldContain("overmind-journal");
        preamble.ShouldContain("cannot boot the planning surface");
        preamble.ShouldContain("human doors");
        preamble.ShouldContain("loopback-only host");
    }

    [Fact]
    public void Capture_Section_PinsTheHumanDoorTypeAndTitleCap()
    {
        var section = ContractsDoc.Section("journal_capture");

        section.ShouldContain("POST /api/entries");
        section.ShouldContain("type is forced to journal");
        section.ShouldContain("write_entry refuses journal on the agent door");
        section.ShouldContain("the first capture births it");
        section.ShouldContain("validated at 500 characters");
        section.ShouldContain("stored whole, no soft cap");
        section.ShouldContain("tags cannot contain commas");
        section.ShouldContain("bodyChars");
    }

    [Fact]
    public void Search_Section_PinsModeClampAndSemanticDownBehavior()
    {
        var section = ContractsDoc.Section("journal_search");

        section.ShouldContain("GET /api/search");
        section.ShouldContain("clamps to 1..100");
        section.ShouldContain("503 with a fix hint when the vector store is down");
        section.ShouldContain("fulltext keeps working");
        section.ShouldContain("hits");
    }

    [Fact]
    public void Read_Section_PinsTheLinkRails()
    {
        var section = ContractsDoc.Section("journal_read");

        section.ShouldContain("GET /api/entries/{id}");
        section.ShouldContain("outgoing and incoming wiki edges");
        section.ShouldContain("unresolved [[key]] markers");
        section.ShouldContain("Loopback-only host, no auth");
    }

    [Fact]
    public void List_Section_PinsFiltersAndDayBounds()
    {
        var section = ContractsDoc.Section("journal_list");

        section.ShouldContain("GET /api/projects/{id}/entries");
        section.ShouldContain("reserved Journal container");
        section.ShouldContain("YYYY-MM-DD");
        section.ShouldContain("since/until bounds");
        section.ShouldContain("caps the returned rows client-side");
        section.ShouldContain("newest-first");
    }

    [Fact]
    public void Append_Section_PinsTheSoftCapAndTheEcho()
    {
        var section = ContractsDoc.Section("journal_append");

        section.ShouldContain("POST /api/entries/{id}/append");
        section.ShouldContain("soft-caps at 2000");
        section.ShouldContain("grace band");
        section.ShouldContain("previews the lost tail");
        section.ShouldContain("currentVersion");
    }

    [Fact]
    public void ResolveLinks_Section_PinsReadOnlyAndTheRefusalCeiling()
    {
        var section = ContractsDoc.Section("journal_resolve_links");

        section.ShouldContain("POST /api/entries/resolve-links");
        section.ShouldContain("read-only dry preview");
        section.ShouldContain("Nothing is written");
        section.ShouldContain("2440");
        section.ShouldContain("nothing resolved");
        section.ShouldContain("Loopback-only host, no auth");
    }

    [Fact]
    public void EveryJournalSection_CarriesTheLoopbackNote()
    {
        foreach (var toolName in ToolDescriptionScanner.ExpectedJournalToolNames)
        {
            var section = ContractsDoc.Section(toolName);
            section.ShouldContain("Loopback-only host, no auth",
                customMessage: $"the {toolName} section lost the loopback-only note");
        }
    }
}
