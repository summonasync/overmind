using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

internal sealed record MirrorFilePlan(
    Guid? FeatureId,
    string RelativePath,
    string AbsolutePath,
    string Content,
    string Sha256)
{
    public static MirrorFilePlan Create(string root, Guid? featureId, string relativePath, string content) =>
        new(featureId, relativePath, PathGuard.ResolveUnderRoot(root, relativePath), content, ContentHash.Sha256(content));
}
