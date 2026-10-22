using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class PlanningOperationRecordTests
{
    [Fact]
    public void Create_MapsIdentityHashAndStoredResponse()
    {
        var projectId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        var record = PlanningOperationRecord.Create(
            projectId, operationId, "abc123", 201, "{\"slug\":\"001-x\"}", createdAt);

        record.Id.ShouldNotBe(Guid.Empty);
        record.ProjectId.ShouldBe(projectId);
        record.OperationId.ShouldBe(operationId);
        record.RequestHash.ShouldBe("abc123");
        record.StatusCode.ShouldBe(201);
        record.ResponseBody.ShouldBe("{\"slug\":\"001-x\"}");
        record.CreatedAt.ShouldBe(createdAt);
    }

    [Fact]
    public void Retention_IsPinnedTo24Hours()
    {
        PlanningOperationRecord.RetentionHours.ShouldBe(24);
    }
}
