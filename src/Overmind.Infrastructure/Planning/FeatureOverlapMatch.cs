using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record FeatureOverlapMatch(
    string Pattern,
    string PartnerPattern,
    FeatureOverlapMatchKind Kind);
