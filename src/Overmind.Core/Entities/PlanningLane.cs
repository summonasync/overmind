namespace Overmind.Core;

public sealed class PlanningLane
{
    public const int MaxNameLength = 64;
    public const int MaxContourLength = 2000;
    public const int MaxBoundAliasLength = 32;
    public const int MaxPriority = 3;

    private PlanningLane()
    {
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Contour { get; private set; }
    public int? SerialOrder { get; private set; }
    public int? Priority { get; private set; }
    public string? BoundAlias { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static PlanningLane Create(
        Guid projectId, string name, string? contour, int? serialOrder, int? priority = null,
        string? boundAlias = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new PlanningLane
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = NormalizeName(name),
            Contour = NormalizeContour(contour),
            SerialOrder = serialOrder,
            Priority = priority,
            BoundAlias = NormalizeBoundAlias(boundAlias),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string name, string? contour, int? serialOrder, int? priority, string? boundAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = NormalizeName(name);
        Contour = NormalizeContour(contour);
        SerialOrder = serialOrder;
        Priority = priority;
        BoundAlias = NormalizeBoundAlias(boundAlias);
        UpdatedAt = DateTime.UtcNow;
    }

    public static bool IsAddressableName(string name)
    {
        if (name.Trim() is "." or "..")
        {
            return false;
        }

        foreach (var character in name)
        {
            if (character is '/' or '\\' || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeName(string name) => name.Trim();

    private static string? NormalizeContour(string? contour) =>
        string.IsNullOrWhiteSpace(contour) ? null : contour.Trim();

    private static string? NormalizeBoundAlias(string? boundAlias) =>
        string.IsNullOrWhiteSpace(boundAlias) ? null : boundAlias.Trim();
}
