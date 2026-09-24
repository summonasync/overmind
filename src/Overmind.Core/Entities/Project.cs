namespace Overmind.Core;

public sealed class Project
{
    private const int MaxMaterializationErrorLength = 512;
    private const int MaxMaterializationSkipHead = 5;

    private Project()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string DiskPath { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PlanningMaterialization PlanningMaterialization { get; private set; }
    public HumanValidationOverride RequireHumanValidation { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastMaterializedAt { get; private set; }
    public string? LastMaterializationError { get; private set; }
    public DateTime? LastMaterializationFailedAt { get; private set; }
    public int? LastMaterializationSkipCount { get; private set; }
    public string? LastMaterializationSkipHead { get; private set; }

    public static Project Create(string name, string diskPath, string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(diskPath);

        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            DiskPath = diskPath.Trim(),
            Description = description.Trim(),
            PlanningMaterialization = PlanningMaterialization.Inherit,
            RequireHumanValidation = HumanValidationOverride.Unset,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetPlanningWorkflow(PlanningMaterialization materialization, HumanValidationOverride validation)
    {
        PlanningMaterialization = materialization;
        RequireHumanValidation = validation;
    }

    public void MarkMaterialized(IReadOnlyList<PlanningMaterializationSkip> skips)
    {
        LastMaterializedAt = DateTime.UtcNow;
        LastMaterializationError = null;
        LastMaterializationFailedAt = null;
        LastMaterializationSkipCount = skips.Count == 0 ? null : skips.Count;
        LastMaterializationSkipHead = skips.Count == 0
            ? null
            : string.Join('\n', skips
                .Take(MaxMaterializationSkipHead)
                .Select(skip => $"{skip.RelativePath}: {skip.Reason}"));
    }

    public void MarkMaterializationFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastMaterializationError = error.Length <= MaxMaterializationErrorLength
            ? error
            : error[..MaxMaterializationErrorLength];
        LastMaterializationFailedAt = DateTime.UtcNow;
    }
}
