namespace Overmind.Core;

public sealed class KitActivationException : Exception
{
    private KitActivationException(KitActivationFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public KitActivationFailureKind Kind { get; }

    public static KitActivationException MandatoryGlobalKit(Guid kitId, string kitName) =>
        new(KitActivationFailureKind.MandatoryGlobal,
            $"Kit '{kitName}' ('{kitId}') is mandatory-global: it materializes into the agent home skill " +
            "directories and cannot be activated for a single project.");

    public static KitActivationException NotLinked(Guid projectId, Guid kitId) =>
        new(KitActivationFailureKind.NotLinked, $"Kit '{kitId}' is not linked to project '{projectId}'.");
}
