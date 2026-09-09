namespace Overmind.Core;

public sealed class Project
{
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
}
