using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;
using Overmind.Host;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// The startup backfill is the leg the migration cannot do: SQLite's lower() would leave Cyrillic titles
// unfolded in the stored column. These tests hand-break a row the way the migration's empty default
// leaves it, run the service directly, and hold both the repair and the idempotent second pass.
public sealed class GraphSearchBackfillTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Backfill_RefoldsHandBrokenRows_AndSecondPassIsStable()
    {
        var factory = new ApiFactory();
        var client = factory.CreateClient();
        Guid entryId;
        Guid featureId;
        Guid projectId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var project = Project.Create("backfill-probe", "/tmp/graph/backfill-probe");
            db.Projects.Add(project);

            var entry = ContextEntry.Create(project.Id, EntryType.Journal, "Рецепт Борща", "body");
            db.ContextEntries.Add(entry);

            var feature = PlanningFeature.Create(project.Id, "backfill-feature", "Фича О Заголовке", "body");
            db.PlanningFeatures.Add(feature);

            await db.SaveChangesAsync();

            entryId = entry.Id;
            featureId = feature.Id;
            projectId = project.Id;

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE ContextEntries SET TitleNormalized = 'BROKEN' WHERE Id = {0}", entry.Id);
        }

        await RunBackfillAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var entry = await db.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.TitleNormalized.ShouldBe("рецепт борща");
        }

        var response = await client.GetAsync($"/api/graph?search={Uri.EscapeDataString("борща")}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var payload = await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        payload!.Nodes.ShouldContain(node => node.Id == entryId,
            customMessage: "the repaired entry answers its lowercase Cyrillic term");

        await RunBackfillAsync(factory);

        using (var secondScope = factory.Services.CreateScope())
        {
            var db = secondScope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var entry = await db.ContextEntries.SingleAsync(e => e.Id == entryId);
            entry.TitleNormalized.ShouldBe("рецепт борща",
                customMessage: "the second pass is a no-op");
        }

        using (var renameScope = factory.Services.CreateScope())
        {
            var db = renameScope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            var feature = await db.PlanningFeatures.SingleAsync(f => f.Id == featureId);
            feature.Update("Переименованная Фича", null);

            await db.SaveChangesAsync();
        }

        var renamed = await client.GetAsync($"/api/graph?search={Uri.EscapeDataString("ПЕРЕИМЕНОВАННАЯ")}");
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renamedBody = await renamed.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        renamedBody!.Nodes.ShouldContain(node => node.Id == featureId,
            customMessage: "a renamed feature is findable by its new title in any casing");

        using (var projectScope = factory.Services.CreateScope())
        {
            var db = projectScope.ServiceProvider.GetRequiredService<OvermindDbContext>();
            (await db.Projects.SingleAsync(p => p.Id == projectId)).NameNormalized.ShouldBe("backfill-probe");
        }

        client.Dispose();
        await factory.DisposeAsync();
    }

    private static async Task RunBackfillAsync(WebApplicationFactory<Program> factory)
    {
        var service = new EntryTitleBackfillService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EntryTitleBackfillService>.Instance);
        await service.StartAsync(CancellationToken.None);
    }

    private sealed record GraphNodeDto(string Kind, Guid Id);

    private sealed record GraphEnvelope(List<GraphNodeDto> Nodes);
}
