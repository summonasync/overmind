using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Entries;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The two stream filters spec R5 promised /journal, answered by the list endpoint itself: no per-row detail
// read, and a value that cannot be resolved answers loudly instead of returning a page that reads as
// "nothing to triage".
public sealed class EntryStreamFilterApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _journalId;
    private readonly Guid _otherProjectId;

    public EntryStreamFilterApiTests()
    {
        _client = _factory.CreateClient();
        _journalId = ContainerId(_factory);
        _otherProjectId = CreateProject("stream-other");
        CreateFeature("777-stream-pin", "Stream pin");

        CreateNote("Unlinked Note", "no marker at all");
        CreateNote("Outgoing Only", "links [[feature:777]]");
        CreateNote("Incoming Only", "plain body");
        CreateNote("Incoming Source", "reads [[Incoming Only]]");
        CreateNote("Project Linker", "links [[project:stream-other]]");
        CreateNote("Buried Target", "plain body");
        CreateNote("Named By Lifted", "plain body");

        var liftedSource = CreateNote("Lifted Source", "reads [[Named By Lifted]]");

        // A tombstone-typed carrier is the only way a buried source still holds an edge: deleting an entry
        // cascades its own edges away, so the dead-counterparty rule needs a row written as a tombstone.
        CreateCarrier("Tombstone Carrier", "buries [[Buried Target]]");

        var lifted = _client.PostAsJsonAsync(
            $"/api/entries/{liftedSource}/lift", new { Reason = "replaced" }).GetAwaiter().GetResult();
        lifted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task UnlinkedFilter_KeepsThePileTheOwnerHasToRead()
    {
        var titles = await StreamTitlesAsync("link=unlinked");

        titles.ShouldBe(
        [
            "Unlinked Note",
            "Buried Target",
            "Named By Lifted"
        ], ignoreOrder: true, customMessage: "a tombstone's mention and a lifted note's mention link nothing");
    }

    [Fact]
    public async Task LinkedFilter_CountsBothDirections_ButNotDeadCounterparties()
    {
        (await StreamTitlesAsync("link=linked")).ShouldBe(
        [
            "Outgoing Only",
            "Incoming Source",
            "Incoming Only",
            "Project Linker"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task LinkedAndUnlinked_PartitionTheStream_TheDefinitionIsAnsweredOnce()
    {
        var linked = await StreamTitlesAsync("link=linked");
        var unlinked = await StreamTitlesAsync("link=unlinked");
        var every = await StreamTitlesAsync(null);

        linked.Intersect(unlinked).ShouldBeEmpty();
        linked.Concat(unlinked).OrderBy(title => title).ShouldBe(every.OrderBy(title => title));
    }

    [Theory]
    [InlineData("LINKED")]
    [InlineData("UnLinked")]
    [InlineData(" unlinked ")]
    public async Task LinkFilter_IsCaseAndWhitespaceTolerant(string value)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_journalId}/entries?type=journal&link={Uri.EscapeDataString(value)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkFilter_UnknownValue_IsATypedRefusalNotAnEmptyPage()
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_journalId}/entries?type=journal&link=sortme");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("UnknownLinkFilter");
    }

    [Fact]
    public async Task ProjectFilter_FindsTheNoteNamingTheProject_ByIdAndByName()
    {
        var byId = await StreamTitlesAsync($"project={_otherProjectId}");
        var byName = await StreamTitlesAsync("project=stream-other");

        byId.ShouldHaveSingleItem();
        byId[0].ShouldBe("Project Linker");
        byName.ShouldBe(byId);
    }

    [Fact]
    public async Task ProjectFilter_ComposesWithTheLinkFilter()
    {
        (await StreamTitlesAsync($"project={_otherProjectId}&link=linked")).ShouldHaveSingleItem();
        (await StreamTitlesAsync($"project={_otherProjectId}&link=unlinked")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("project=nowhere-project")]
    [InlineData("project=11111111-1111-1111-1111-111111111111")]
    public async Task ProjectFilter_UnresolvableValue_IsRefusedNotSilentlyEmpty(string query)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_journalId}/entries?type=journal&{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("UnresolvedProjectFilter");
    }

    [Fact]
    public async Task Filters_RideTheListEndpoint_NoExtraCommandAndNoPerRowRead()
    {
        var capture = new WriteCapture();
        using var measured = new ApiFactory(interceptor: capture);
        using var client = measured.CreateClient();
        var journal = ContainerId(measured);

        await CreateNoteAsync(client, "Measured Target", "plain body");
        await CreateNoteAsync(client, "Measured Source", "reads [[Measured Target]]");
        await CreateNoteAsync(client, "Measured Pile", "no marker");

        var plain = await ListCommandsAsync(client, journal, null, capture);
        var filtered = await ListCommandsAsync(client, journal, "link=unlinked", capture);

        filtered.ShouldBe(plain, "the filter costs no round trip: a page keeps the same command count it had unfiltered");

        string[] commands = [.. capture.Statements.Select(statement => statement.Sql)];
        commands.Count(sql => sql.Contains("EntryLinks", StringComparison.Ordinal))
            .ShouldBeGreaterThan(0, string.Join(" || ", commands));

        var main = commands.First(sql => sql.Contains("FROM \"ContextEntries\" AS \"c\"", StringComparison.Ordinal)
            && sql.Contains("EntryLinks", StringComparison.Ordinal));
        main.ShouldContain("NOT EXISTS", Case.Sensitive);
        main.ShouldContain("\"e\".\"FromEntryId\" = \"c\".\"Id\"", Case.Sensitive);
        main.ShouldContain("\"e0\".\"ToKind\" = 0 AND \"e0\".\"ToId\" = \"c\".\"Id\"", Case.Sensitive);
        main.ShouldNotContain("IN (SELECT", Case.Sensitive);
    }

    [Fact]
    public async Task Filters_AreAdditive_TheRowShapeIsUntouched()
    {
        var without = await ResponsePropertyNames($"/api/projects/{_journalId}/entries?type=journal");
        var with = await ResponsePropertyNames(
            $"/api/projects/{_journalId}/entries?type=journal&link=linked&project={_otherProjectId}");

        with.ShouldBe([.. without]);
    }

    private static async Task<int> ListCommandsAsync(
        HttpClient client, Guid journal, string? filter, WriteCapture capture)
    {
        capture.Clear();
        var rows = await client.GetFromJsonAsync<List<StreamRow>>(StreamPath(journal, filter), JsonOptions);
        rows.ShouldNotBeNull();
        rows.ShouldNotContain(row => row.Outgoing.Count > 0, "list rows stay rail-free by design");
        rows.ShouldNotContain(row => row.Incoming.Count > 0);

        return capture.Statements.Count(statement =>
            statement.Sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Guid> CreateNoteAsync(HttpClient client, string title, string content)
    {
        var response = await client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = title,
            Content = content,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<StreamRow>(JsonOptions))!.Id;
    }

    private static string StreamPath(Guid journal, string? filter)
    {
        var path = $"/api/projects/{journal}/entries?type=journal&state=active";
        return string.IsNullOrEmpty(filter) ? path : $"{path}&{filter}";
    }

    private async Task<List<string>> StreamTitlesAsync(string? filter)
    {
        var rows = await _client.GetFromJsonAsync<List<StreamRow>>(StreamPath(_journalId, filter), JsonOptions);
        rows.ShouldNotBeNull();
        return [.. rows!.Select(row => row.Title)];
    }

    private Guid CreateNote(string title, string content) =>
        CreateNoteAsync(_client, title, content).GetAwaiter().GetResult();

    private void CreateCarrier(string title, string content)
    {
        var response = _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _journalId,
            Type = "tombstone",
            Title = title,
            Content = content,
        }).GetAwaiter().GetResult();

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private void CreateFeature(string slug, string title)
    {
        var response = _client.PostAsJsonAsync(
            $"/api/projects/{_otherProjectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" }).GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
    }

    private static Guid ContainerId(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<JournalProjectStore>()
            .EnsureIdAsync()
            .GetAwaiter()
            .GetResult();
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

    private async Task<List<string>> ResponsePropertyNames(string path)
    {
        var payload = await _client.GetStringAsync(path);
        using var document = JsonDocument.Parse(payload);
        return [.. document.RootElement.EnumerateArray().First().EnumerateObject()
            .Select(property => property.Name)];
    }

    public sealed record ProjectJson(Guid Id);

    public sealed record ProblemJson(string? Title, string? Detail);

    public sealed record OutgoingJson(string Kind, Guid Id, string Label);

    public sealed record BacklinkJson(Guid EntryId, string Title, string Snippet);

    public sealed record StreamRow(
        Guid Id,
        string Title,
        string State,
        List<OutgoingJson> Outgoing,
        List<BacklinkJson> Incoming,
        List<string> Unresolved);
}
