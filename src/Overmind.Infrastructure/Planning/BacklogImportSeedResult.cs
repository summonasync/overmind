using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record BacklogImportSeedResult(int Tracked, PlanningMaterialization Strategy);
