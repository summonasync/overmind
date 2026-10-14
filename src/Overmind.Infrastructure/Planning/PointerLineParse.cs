namespace Overmind.Infrastructure.Planning;

public sealed record PointerLineParse(bool IsMarker, ArtifactPointer? Pointer, string? Error)
{
    public static PointerLineParse NotMarker { get; } = new(false, null, null);

    public static PointerLineParse Valid(ArtifactPointer pointer) => new(true, pointer, null);

    public static PointerLineParse Invalid(string error) => new(true, null, error);
}
