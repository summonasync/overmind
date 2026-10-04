using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;

namespace Overmind.Infrastructure.Tests;

public static class TestPlanningDispatcher
{
    public static PlanningMaterializationDispatcher Create(
        OvermindDbContext db,
        IPlanningMaterializer materializer,
        PlanningMaterializationQueue? queue = null) =>
        new(
            queue ?? new PlanningMaterializationQueue(),
            new AppSettingsService(db),
            materializer,
            NullLogger<PlanningMaterializationDispatcher>.Instance);

    public static async Task EnableSyncModeAsync(OvermindDbContext db) =>
        await new AppSettingsService(db).SetAsync(
            SettingKeys.PlanningMaterializationSync, "on", CancellationToken.None);
}
