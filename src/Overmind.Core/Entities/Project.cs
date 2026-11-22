using System.Text.RegularExpressions;

namespace Overmind.Core;

public sealed partial class Project
{
    private const int MaxMaterializationErrorLength = 512;
    private const int MaxMaterializationSkipHead = 5;
    public const int DefaultClaimStaleThresholdMinutes = 15;
    public const int MaxManifestLength = 200_000;
    public const int MaxNamingRulesLength = 2000;
    public const int MaxRescuePolicyLength = 2000;
    public const int NumberPrefixMaxLength = 16;
    public const string DefaultRescuePolicy =
        "everything unpushed may be pushed, everything unmerged I allow merging";

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,15}$")]
    public static partial Regex NumberPrefixRegex { get; }

    private Project()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NameNormalized { get; private set; } = string.Empty;
    public string DiskPath { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PlanningMaterialization PlanningMaterialization { get; private set; }
    public HumanValidationOverride RequireHumanValidation { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastMaterializedAt { get; private set; }
    public DateTime? LastMaterializationAttemptAt { get; private set; }
    public string? LastMaterializationError { get; private set; }
    public DateTime? LastMaterializationFailedAt { get; private set; }
    public DateTime? MaterializationErrorSeenAt { get; private set; }
    public string? LastMaterializationRun { get; private set; }
    public int? LastMaterializationSkipCount { get; private set; }
    public string? LastMaterializationSkipHead { get; private set; }
    public int ClaimStaleThresholdMinutes { get; private set; } = DefaultClaimStaleThresholdMinutes;
    public string? AgentNamingRules { get; private set; }
    public string? RescuePolicy { get; private set; }
    public string? Manifest { get; private set; }
    public ProjectNumberStyle NumberStyle { get; private set; } = ProjectNumberStyle.Hash;
    public string? NumberPrefix { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool AutoActOnReplies { get; private set; }
    public bool HumanDigestEnabled { get; private set; }
    public DelegateApprovalsOverride DelegateApprovals { get; private set; }
    public List<PlanningLane> Lanes { get; private set; } = [];

    public static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    public static Project Create(string name, string diskPath, string description = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(diskPath);

        var trimmedName = name.Trim();
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            NameNormalized = NormalizeName(name),
            DiskPath = diskPath.Trim(),
            Description = description.Trim(),
            PlanningMaterialization = PlanningMaterialization.Inherit,
            RequireHumanValidation = HumanValidationOverride.Unset,
            DelegateApprovals = DelegateApprovalsOverride.Unset,
            NumberStyle = ProjectNumberStyle.Hash,
            IsFavorite = false,
            AutoActOnReplies = true,
            HumanDigestEnabled = false,
            RescuePolicy = DefaultRescuePolicy,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        NameNormalized = NormalizeName(Name);
    }

    public void SetPlanningWorkflow(PlanningMaterialization materialization, HumanValidationOverride validation)
    {
        PlanningMaterialization = materialization;
        RequireHumanValidation = validation;
    }

    public void SetClaimSettings(int staleThresholdMinutes, string? namingRules, string? rescuePolicy = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(staleThresholdMinutes);
        if (namingRules is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(namingRules.Length, MaxNamingRulesLength);
        }

        if (rescuePolicy is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(rescuePolicy.Length, MaxRescuePolicyLength);
        }

        ClaimStaleThresholdMinutes = staleThresholdMinutes;
        AgentNamingRules = string.IsNullOrWhiteSpace(namingRules) ? null : namingRules.Trim();
        if (rescuePolicy is not null)
        {
            RescuePolicy = string.IsNullOrWhiteSpace(rescuePolicy) ? null : rescuePolicy.Trim();
        }
    }

    public void SetManifest(string? manifest)
    {
        if (manifest is null)
        {
            Manifest = null;
            return;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(manifest.Length, MaxManifestLength);
        Manifest = string.IsNullOrWhiteSpace(manifest) ? null : manifest.Trim();
    }

    public void SetFavorite(bool isFavorite) => IsFavorite = isFavorite;

    public void SetAutoActOnReplies(bool enabled) => AutoActOnReplies = enabled;

    public void SetHumanDigest(bool enabled) => HumanDigestEnabled = enabled;

    public void SetDelegateApprovals(DelegateApprovalsOverride mode) => DelegateApprovals = mode;

    public void SetNumbering(ProjectNumberStyle style, string? numberPrefix)
    {
        if (style == ProjectNumberStyle.Key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(numberPrefix);
            var prefix = numberPrefix.Trim();
            if (!NumberPrefixRegex.IsMatch(prefix))
            {
                throw new ArgumentException(
                    $"Number prefix '{prefix}' must be {NumberPrefixMaxLength} characters or fewer: an uppercase letter followed by uppercase letters, digits or underscores.",
                    nameof(numberPrefix));
            }

            NumberPrefix = prefix;
        }
        else
        {
            NumberPrefix = null;
        }

        NumberStyle = style;
    }

    public void MarkMaterialized(IReadOnlyList<PlanningMaterializationSkip> skips)
    {
        LastMaterializedAt = DateTime.UtcNow;
        LastMaterializationError = null;
        LastMaterializationFailedAt = null;
        MaterializationErrorSeenAt = null;
        LastMaterializationSkipCount = skips.Count == 0 ? null : skips.Count;
        LastMaterializationSkipHead = skips.Count == 0
            ? null
            : string.Join('\n', skips
                .Take(MaxMaterializationSkipHead)
                .Select(skip => $"{skip.RelativePath}: {skip.Reason}"));
    }

    public void MarkMaterializationAttempt(DateTime startedAt) =>
        LastMaterializationAttemptAt = startedAt;

    public void RecordMaterializationRun(string run) =>
        LastMaterializationRun = run;

    public void MarkMaterializationFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastMaterializationError = error.Length <= MaxMaterializationErrorLength
            ? error
            : error[..MaxMaterializationErrorLength];
        LastMaterializationFailedAt = DateTime.UtcNow;
        MaterializationErrorSeenAt = null;
    }

    // The seen stamp carries the failure's own FailedAt, so "seen" is the explicit comparison
    // MaterializationErrorSeenAt >= LastMaterializationFailedAt: a newer failure resets the
    // stamp to null in MarkMaterializationFailed and turns unseen again, a clean pass clears
    // both. Onboard sets this once it has rendered the full block for the current failure.
    public void MarkMaterializationErrorSeen() =>
        MaterializationErrorSeenAt = LastMaterializationFailedAt;
}
