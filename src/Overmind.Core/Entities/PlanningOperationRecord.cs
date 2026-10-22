namespace Overmind.Core;

public sealed class PlanningOperationRecord
{
    public const int RetentionHours = 24;

    private PlanningOperationRecord()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid OperationId { get; private set; }
    public string RequestHash { get; private set; } = null!;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public static PlanningOperationRecord Create(
        Guid projectId,
        Guid operationId,
        string requestHash,
        int statusCode,
        string responseBody,
        DateTime createdAt)
    {
        return new PlanningOperationRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OperationId = operationId,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = createdAt
        };
    }
}
