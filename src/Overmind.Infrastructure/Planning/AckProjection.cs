namespace Overmind.Infrastructure.Planning;

public sealed record AckProjection(string? ByAlias, string? Vendor, DateTime? At)
{
    public bool Acked => At is not null;

    public static AckProjection Open { get; } = new(null, null, null);

    public static AckProjection From(string? byAlias, string? vendor, DateTime? at) =>
        at is null ? Open : new AckProjection(byAlias, vendor, at);
}
