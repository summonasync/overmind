using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Spec 249 R1/R2/R5: the activity pair binds UpdatedAt on both list branches, and the endpoint's
// default floor keeps the unbounded live-sync read bounded without turning old rows invisible past
// a cap. The fixtures lean on the CreatedAt/UpdatedAt split on purpose — a note created early and
// touched today is exactly the row the old since/until pair lost.
public sealed class EntryActivityWindowApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly Guid _projectId;

    public EntryActivityWindowApiTests()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var project = Project.Create("activity-window", "/tmp/activity-window");
        db.Projects.Add(project);
        db.SaveChanges();
        _projectId = project.Id;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreatedEarlier_UpdatedToday_VisibleUnderTodaysActivityWindow()
    {
        var now = DateTime.UtcNow;
        var touchedToday = SeedNote("rescued today", "created last week, edited today",
            createdAt: now.AddDays(-5), updatedAt: now);
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var activityWindow = await ListAsync(
            $"activitySince={Iso(todayStart)}&activityUntil={Iso(tomorrowStart)}");
        activityWindow.Select(row => row.Id).ShouldContain(touchedToday);

        var createdAtWindow = await ListAsync(
            $"since={Iso(todayStart)}&until={Iso(tomorrowStart)}");
        createdAtWindow.Select(row => row.Id).ShouldNotContain(touchedToday);
    }

    [Fact]
    public async Task UpdatedOutsideTheWindow_StaysHidden()
    {
        var now = DateTime.UtcNow;
        var touchedLastWeek = SeedNote("quiet week", "edited last week",
            createdAt: now.AddDays(-30), updatedAt: now.AddDays(-7));

        var activityWindow = await ListAsync(
            $"activitySince={Iso(now.Date)}&activityUntil={Iso(now.Date.AddDays(1))}");
        activityWindow.Select(row => row.Id).ShouldNotContain(touchedLastWeek);
    }

    [Fact]
    public async Task WithoutAPair_TheDefaultWindowBoundsTheStream()
    {
        var now = DateTime.UtcNow;
        var stale = SeedNote("stale note", "untouched for weeks",
            createdAt: now.AddDays(-60), updatedAt: now.AddDays(-31));
        var fresh = SeedNote("fresh note", "touched yesterday",
            createdAt: now.AddDays(-60), updatedAt: now.AddDays(-1));
        var oldButTouched = SeedNote("old but touched", "edited today",
            createdAt: now.AddDays(-90), updatedAt: now);
        var youngButQuiet = SeedNote("young but quiet", "created today, touched last week",
            createdAt: now, updatedAt: now.AddDays(-7));

        var unwindowed = await ListAsync(null);
        var ids = unwindowed.Select(row => row.Id);
        ids.ShouldNotContain(stale);
        ids.ShouldContain(fresh);
        ids.ShouldContain(oldButTouched, "the floor binds UpdatedAt, not CreatedAt");
        ids.ShouldContain(youngButQuiet);

        var widened = await ListAsync($"activitySince={Iso(now.AddDays(-40))}");
        widened.Select(row => row.Id).ShouldContain(stale);
    }

    [Fact]
    public async Task SearchPath_HonorsTheActivityWindow()
    {
        var now = DateTime.UtcNow;
        var fresh = await CreateNoteAsync("quokka fresh", "quokka touched today");
        var stale = await CreateNoteAsync("quokka stale", "quokka touched a month ago");
        Backdate(stale, createdAt: now.AddDays(-45), updatedAt: now.AddDays(-31));

        var unwindowed = await ListAsync("q=quokka");
        unwindowed.Select(row => row.Id).ShouldBe([fresh], ignoreOrder: true);

        var widened = await ListAsync($"q=quokka&activitySince={Iso(now.AddDays(-60))}");
        widened.Select(row => row.Id).ShouldBe([fresh, stale], ignoreOrder: true);
    }

    [Fact]
    public async Task LinkedCohortAccent_TheDefaultWindowBoundsIt()
    {
        var now = DateTime.UtcNow;
        var staleTarget = SeedNote("stale target", "plain body",
            createdAt: now.AddDays(-45), updatedAt: now.AddDays(-31));
        var freshPointer = SeedNote("fresh pointer", "plain body",
            createdAt: now.AddDays(-1), updatedAt: now);
        LinkDerived(freshPointer, staleTarget);

        var unwindowed = await ListAsync("link=linked");
        var ids = unwindowed.Select(row => row.Id);
        ids.ShouldContain(freshPointer);
        ids.ShouldNotContain(staleTarget);

        var widened = await ListAsync($"link=linked&activitySince={Iso(now.AddDays(-60))}");
        widened.Select(row => row.Id).ShouldBe([freshPointer, staleTarget], ignoreOrder: true);
    }

    [Theory]
    [InlineData("activitySince", "not-a-date")]
    [InlineData("activityUntil", "13/45/2020")]
    public async Task InvalidActivityBound_IsRefusedNotSilentlyDropped(string name, string value)
    {
        var response = await _client.GetAsync(
            $"/api/projects/{_projectId}/entries?{name}={Uri.EscapeDataString(value)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string Iso(DateTime at) => at.ToString("O");

    private Guid SeedNote(string title, string content, DateTime createdAt, DateTime updatedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var entry = ContextEntry.Create(_projectId, EntryType.Journal, title, content);
        db.ContextEntries.Add(entry);
        db.Entry(entry).Property(e => e.CreatedAt).CurrentValue = createdAt;
        db.Entry(entry).Property(e => e.UpdatedAt).CurrentValue = updatedAt;
        db.SaveChanges();
        return entry.Id;
    }

    private void LinkDerived(Guid from, Guid to)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        db.EntryLinks.Add(EntryLink.CreateDerived(from, EntryLinkKind.Entry, to));
        db.SaveChanges();
    }

    private async Task<Guid> CreateNoteAsync(string title, string content)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = _projectId,
            Type = "Note",
            Title = title,
            Content = content,
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RowJson>(JsonOptions))!.Id;
    }

    private void Backdate(Guid id, DateTime createdAt, DateTime updatedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var entry = db.ContextEntries.Single(e => e.Id == id);
        db.Entry(entry).Property(e => e.CreatedAt).CurrentValue = createdAt;
        db.Entry(entry).Property(e => e.UpdatedAt).CurrentValue = updatedAt;
        db.SaveChanges();
    }

    private async Task<List<RowJson>> ListAsync(string? query)
    {
        var path = $"/api/projects/{_projectId}/entries";
        var response = await _client.GetAsync(query is null ? path : $"{path}?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<RowJson>>(JsonOptions))!;
    }

    public sealed record RowJson(Guid Id, string Title, DateTime UpdatedAt);
}
