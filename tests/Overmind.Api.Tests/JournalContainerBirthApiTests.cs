using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Spec R2 in one sentence: the reserved container is born by the owner's first thought, and by nothing else.
// The write-path link resolver used to disagree — it ensured the container on every entry write, so an agent note
// whose body carried a bare [[Title]] minted the human's Journal project (empty, but visible in the project list)
// before the owner ever wrote a line. The ensure bought resolution nothing: with no container there are no journal
// rows, so the title map was empty either way. These tests measure the container across the writes that used to
// birth it, and keep the one write that legitimately must.
public sealed class JournalContainerBirthApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    public JournalContainerBirthApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
        _projectId = CreateProject("birth-project");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task AgentWriteWithABareWikiLink_LeavesTheReservedContainerUnborn()
    {
        ContainerExists().ShouldBeFalse("the probe starts on a database the owner has never journaled in");

        var response = await _client.PostAsJsonAsync("/api/agent/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = "Agent note with a marker",
            Content = "mentions [[Owner thought]] in passing",
        }, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Created, body);
        ContainerExists().ShouldBeFalse(
            "an agent write reached the human surface: the Journal project would now exist with no rows in it");

        // The marker is unresolved, not lost to an error: the write succeeded and the edge set is empty.
        var created = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
        (await DerivedEdgeCountAsync(created)).ShouldBe(0);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("append")]
    public async Task AgentWriteOnAnExistingEntry_WithABareWikiLink_LeavesTheContainerUnborn(string verb)
    {
        // The recompute runs on every write, not only on create, and this is the shape that produced the same
        // side effect one request later: a body edited into carrying a marker the owner has not written yet.
        var entryId = await CreateAgentNoteAsync("Birth probe note", "plain body");

        ContainerExists().ShouldBeFalse();

        var response = await SendAsync(verb, entryId, "now mentions [[Owner thought]]");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{verb}: {body}");
        ContainerExists().ShouldBeFalse($"the {verb} recompute birthed the reserved container");
        (await DerivedEdgeCountAsync(entryId)).ShouldBe(0);
    }

    [Fact]
    public async Task OwnerFirstThought_BirthsTheContainer_TheHumanJournalDoorStillDoes_It()
    {
        ContainerExists().ShouldBeFalse();

        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = "Owner thought",
            Content = "the first line the owner wrote",
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        ContainerExists().ShouldBeTrue("R2: the container is born by the owner's first thought");
    }

    [Fact]
    public async Task OnceTheContainerExists_AnAgentMarker_ResolvesAgainstTheOwnersRow()
    {
        // The other half of the decision: not birthing the container must not cost resolution once the owner has
        // one to resolve against. Same request body as the unborn case, different answer, no side effect needed.
        var owned = await _client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = "Owner thought",
            Content = "the first line the owner wrote",
        }, JsonOptions);
        owned.EnsureSuccessStatusCode();
        var ownerId = JsonDocument.Parse(await owned.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();

        var agentNote = await CreateAgentNoteAsync("Agent note reading the owner", "reads [[Owner thought]]");

        var edges = await DerivedEdgeTargetsAsync(agentNote);
        edges.ShouldContain(ownerId,
            "the agent edge into the owner's row is exactly what the write path is allowed to create");
        ContainerExists().ShouldBeTrue();
    }

    [Fact]
    public async Task HumanNoteWrite_WithABareWikiLink_LeavesTheContainerUnborn_Too()
    {
        // The rule is about the write path, not about the caller: a note at either door is not a thought, and R2
        // names one birth. If the owner decides a note should start the journal, the journal create is one call
        // away and says so explicitly.
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = "Human note with a marker",
            Content = "mentions [[Owner thought]]",
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        ContainerExists().ShouldBeFalse();
    }

    private async Task<Guid> CreateAgentNoteAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/agent/entries", new
        {
            ProjectId = _projectId,
            Type = "note",
            Title = title,
            Content = content,
        }, JsonOptions);

        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> SendAsync(string verb, Guid entryId, string content) => verb switch
    {
        "update" => _client.PutAsJsonAsync($"/api/agent/entries/{entryId}", new { title = "Birth probe note", content }, JsonOptions),
        "append" => _client.PostAsJsonAsync($"/api/agent/entries/{entryId}/append", new { text = content }, JsonOptions),
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "unknown verb"),
    };

    private bool ContainerExists()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .Projects.Any(p => p.Name == JournalProjectStore.ProjectName);
    }

    private async Task<int> DerivedEdgeCountAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .EntryLinks.AsNoTracking()
            .CountAsync(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Derived);
    }

    private async Task<Guid[]> DerivedEdgeTargetsAsync(Guid entryId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OvermindDbContext>()
            .EntryLinks.AsNoTracking()
            .Where(link => link.FromEntryId == entryId && link.Origin == EntryLinkOrigin.Derived)
            .Select(link => link.ToId)
            .ToArrayAsync();
    }

    private Guid CreateProject(string name)
    {
        var response = _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
        }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions).GetAwaiter().GetResult()!.Id;
    }

    public sealed record ProjectJson(Guid Id);
}
