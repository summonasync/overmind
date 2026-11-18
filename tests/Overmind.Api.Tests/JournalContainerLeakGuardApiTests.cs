using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The author boundary of spec R6 has two doors, not one. JournalWriteLockFilter closes the type door
// (an agent cannot create type=journal). These tests close the destination door: an agent must not be able to
// file a non-journal row into the human's reserved container either, and the way it tried to do it was
// projectId = the empty guid, which the widened Guid? let through the validator and the create handler then
// read as "no destination" — the bootstrap's own signal. Every refusal here is measured against a container
// that already exists, so "nothing landed" is an assertion about rows, not about status codes.
public sealed class JournalContainerLeakGuardApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public JournalContainerLeakGuardApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    public static IEnumerable<object[]> AgentWritableTypes() =>
        EntryTypeSurface.AgentWritable.Select(type => new object[] { type });

    // m1: the seal is a rule about types, so its coverage is measured off the enum and not off a list somebody
    // keeps. A member added tomorrow arrives here without anyone editing a case — BodyFor refuses to guess what an
    // unclassified member's request should look like and says which member is unhandled.
    public static IEnumerable<object[]> EveryNonJournalEntryType() =>
        Enum.GetValues<EntryType>()
            .Where(type => type != EntryType.Journal)
            .Select(type => new object[] { type });

    [Fact]
    public static void TheEnumScanIsTheWholeEnumMinusJournal_NotASubsetOfIt()
    {
        // The denominator: a member source narrowed by a hand-edit would leave the theory green on fewer cases.
        var all = Enum.GetValues<EntryType>();
        var scanned = all.Where(type => type != EntryType.Journal).ToArray();

        scanned.Length.ShouldBe(all.Length - 1,
            $"journal is the only type the empty guid is allowed to mean, got {scanned.Length} of {all.Length}");
        scanned.ShouldNotContain(EntryType.Journal);
        scanned.ShouldContain(EntryType.Skill,
            "the type the agent gate has opinions about must be measured at this door too");
        scanned.ShouldContain(EntryType.Tombstone);
    }

    [Theory]
    [MemberData(nameof(EveryNonJournalEntryType))]
    public async Task HumanDoor_EveryNonJournalTypeWithEmptyGuidProjectId_IsRefusedLikeBeforeTask14(EntryType type)
    {
        var containerId = await SeedTheReservedContainerAsync();
        var probe = $"Human zero-guid {type}";
        var before = await CountRowsAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/entries", BodyFor(type, Guid.Empty, probe), JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, Landing(response.StatusCode, body, containerId));
        body.Contains("ProjectId cannot be the empty guid").ShouldBeTrue(
            $"the refusal has to come from the destination rule and not from {type}'s own shape: {body}");
        (await TitlesInContainerAsync(containerId)).ShouldNotContain(probe);
        (await CountRowsAsync()).ShouldBe(before);
    }

    [Theory]
    [MemberData(nameof(AgentWritableTypes))]
    public async Task AgentDoor_NonJournalWithEmptyGuidProjectId_IsRefusedAndTheContainerStaysClean(EntryType type)
    {
        var containerId = await SeedTheReservedContainerAsync();
        var probe = $"Agent zero-guid {type}";
        var before = await CountRowsAsync();

        var response = await PostJsonAsync("/api/agent/entries", type, ZeroGuid(), probe);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, Landing(response.StatusCode, body, containerId));
        body.ShouldContain("ProjectId cannot be the empty guid");
        (await TitlesAnywhereAsync()).ShouldNotContain(probe, $"the refused {type} must not exist at all: {body}");
        (await TitlesInContainerAsync(containerId)).ShouldNotContain(probe);
        (await CountRowsAsync()).ShouldBe(before, "a refusal writes nothing");
    }

    [Theory]
    [MemberData(nameof(AgentWritableTypes))]
    public async Task AgentDoor_NonJournalWithNullProjectId_IsRefused(EntryType type)
    {
        // The other half of the same field, kept as its own case on purpose: JSON null and the empty guid are
        // different inputs and the guards that catch them are different rules.
        var containerId = await SeedTheReservedContainerAsync();
        var probe = $"Agent null {type}";

        var response = await PostJsonAsync("/api/agent/entries", type, null, probe);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await TitlesInContainerAsync(containerId)).ShouldBe(["Seed thought"]);
    }

    [Fact]
    public async Task HumanDoor_NoteWithNullProjectId_IsRefused()
    {
        await SeedTheReservedContainerAsync();

        var response = await PostJsonAsync("/api/entries", EntryType.Note, null, "Human null note");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AgentDoor_JournalWithEmptyGuidProjectId_IsRefusedByTheTypeGateFirst()
    {
        await SeedTheReservedContainerAsync();
        var before = await CountRowsAsync();

        var response = await PostJsonAsync("/api/agent/entries", EntryType.Journal, ZeroGuid(), "Agent journal zero");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe("JournalEntryWriteRefused");
        (await CountRowsAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task HumanDoor_JournalWithNullProjectId_LandsInTheReservedContainer()
    {
        // R2/R4: a thought never chooses a destination. Closing the leak must not cost the human this path.
        var response = await PostJsonAsync("/api/entries", EntryType.Journal, null, "Human null journal");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        created!.ProjectId.ShouldBe(container.Id);
        created.Type.ShouldBe("journal");
    }

    [Fact]
    public async Task HumanDoor_JournalWithEmptyGuidProjectId_IsReadAsNoDestinationAndLandsInTheContainer()
    {
        // Pinned decision, not an accident: for a journal create at the human door the empty guid means the same
        // thing as null — the destination is the human surface either way, so there is nothing to leak. The
        // coercion is only ever reachable for this type.
        var containerId = await SeedTheReservedContainerAsync();

        var response = await PostJsonAsync("/api/entries", EntryType.Journal, ZeroGuid(), "Human zero journal");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        created!.ProjectId.ShouldBe(containerId);
        (await TitlesInContainerAsync(containerId)).ShouldContain("Human zero journal");
    }

    [Fact]
    public async Task CreateHandler_WithTheValidatorStripped_StillRefusesAnEmptyGuidForANote()
    {
        // The second half of the seal, proven on its own merits rather than as a comment. The validator is
        // swapped for a rule-less one, so the request that reaches the create handler is the exact one task 14
        // leaked: type=note with the empty guid. If the handler's type gate is real, this answer comes from the
        // handler and not from the rule above it — which is what the message below pins.
        using var stripped = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IValidator<CreateEntryRequest>>();
            services.AddSingleton<IValidator<CreateEntryRequest>, RuleLessValidator>();
        }));
        using var client = stripped.CreateClient();

        var response = await client.PostAsJsonAsync("/api/agent/entries", new
        {
            ProjectId = Guid.Empty,
            Type = "note",
            Title = "Past the validator on purpose",
            Content = "only the handler stands between this row and the human's container",
        }, JsonOptions);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
        body.ShouldContain(
            "ProjectId is required for every type but journal",
            customMessage: "the refusal must be the handler's own gate, not the validator's");

        using var scope = stripped.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.Projects.AnyAsync(p => p.Name == JournalProjectStore.ProjectName)).ShouldBeFalse(
            "the bootstrap that mints the reserved container must not have run");
    }

    [Fact]
    public async Task CreateHandler_WithTheValidatorStripped_StillFilesAnOwnerlessJournalInTheContainer()
    {
        // Same stripped door, the type that is allowed to arrive ownerless: the gate is shaped by type, not a
        // blanket refusal, so the journal coercion survives with its validator held out of the picture.
        using var stripped = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IValidator<CreateEntryRequest>>();
            services.AddSingleton<IValidator<CreateEntryRequest>, RuleLessValidator>();
        }));
        using var client = stripped.CreateClient();

        var response = await client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = Guid.Empty,
            Type = "journal",
            Title = "Ownerless past the validator",
            Content = "the reserved container is still its destination",
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);

        using var scope = stripped.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var container = await db.Projects.SingleAsync(p => p.Name == JournalProjectStore.ProjectName);
        created!.ProjectId.ShouldBe(container.Id);
    }

    [Fact]
    public async Task AgentDoor_NoteWithARealProject_LandsThereAndTheContainerNeverGrows()
    {
        // The count assertions above only mean something if a legitimate write is able to move them.
        var containerId = await SeedTheReservedContainerAsync();
        var projectId = CreateProject("leak-guard-named");

        var response = await PostJsonAsync("/api/agent/entries", EntryType.Note, projectId, "Named destination note");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        created!.ProjectId.ShouldBe(projectId);
        (await TitlesInContainerAsync(containerId)).ShouldBe(["Seed thought"]);
    }

    private static Guid ZeroGuid() => Guid.Empty;

    // The reviewer's stand reported the leak as "201 + landed in the reserved container", so a red run of these
    // tests has to read the same way: status, the project the row actually landed in, and whether that is the
    // human's container. A bare status-code mismatch would not name the damage.
    private static string Landing(HttpStatusCode status, string body, Guid containerId)
    {
        Guid? landed = null;
        try
        {
            landed = JsonSerializer.Deserialize<EntryJson>(body, JsonOptions)?.ProjectId;
        }
        catch (JsonException)
        {
            landed = null;
        }

        return $"status={status} landedInProject={landed?.ToString() ?? "<no row>"} reservedContainer={containerId} " +
               $"landedInReservedContainer={landed == containerId}";
    }

    private Task<HttpResponseMessage> PostJsonAsync(
        string path,
        EntryType type,
        Guid? projectId,
        string title) =>
        _client.PostAsJsonAsync(path, BodyFor(type, projectId, title), JsonOptions);

    // One body per declared type, shaped so that the empty guid is the only thing wrong with it. An unclassified
    // member says so by name instead of being posted in a default shape that might pass for the wrong reason.
    private static object BodyFor(EntryType type, Guid? projectId, string title) => type switch
    {
        EntryType.Skill => new
        {
            ProjectId = projectId,
            Type = EntryTypeSurface.Normalize(type),
            Title = title,
            Content = "probe body",
            SkillFiles = new[] { new { Platform = (string?)null, RelativePath = "SKILL.md", Content = "body" } },
        },
        EntryType.Journal => new
        {
            ProjectId = projectId,
            Type = EntryTypeSurface.Normalize(type),
            Title = title,
            Content = "probe body",
        },
        EntryType.Note or EntryType.Decision or EntryType.Gate or EntryType.Guard
            or EntryType.Idea or EntryType.Audit or EntryType.Tombstone => new
        {
            ProjectId = projectId,
            Type = EntryTypeSurface.Normalize(type),
            Title = title,
            Content = "probe body",
        },
        _ => throw new InvalidOperationException(
            $"EntryType.{type} has no case in this guard's payload shape — classify it here on purpose rather " +
            "than letting the theory post a body that proves nothing about it."),
    };

    // Spec R2 through the human door: the container exists before any probe, so every refusal below is judged
    // against a real inbox that could have received a stray row.
    private async Task<Guid> SeedTheReservedContainerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            Type = "journal",
            Title = "Seed thought",
            Content = "the human's own first line",
        }, JsonOptions);

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        created!.ProjectId.ShouldNotBe(Guid.Empty);
        return created.ProjectId;
    }

    private async Task<int> CountRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        return await db.ContextEntries.CountAsync();
    }

    private async Task<string[]> TitlesInContainerAsync(Guid containerId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        return await db.ContextEntries
            .Where(entry => entry.ProjectId == containerId)
            .Select(entry => entry.Title)
            .ToArrayAsync();
    }

    private async Task<string[]> TitlesAnywhereAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        return await db.ContextEntries.Select(entry => entry.Title).ToArrayAsync();
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

    // Stands in for the validator/DTO drift the handler gate exists to survive: it accepts whatever the wire
    // sent, so a request reaching the create handler has passed no rule at all.
    private sealed class RuleLessValidator : AbstractValidator<CreateEntryRequest>;

    private sealed record EntryJson(Guid Id, Guid ProjectId, string Type, string Title);

    private sealed record ProjectJson(Guid Id);

    private sealed record ProblemJson(string? Title, string? Detail);
}
