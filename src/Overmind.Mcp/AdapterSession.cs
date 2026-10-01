namespace Overmind.Mcp;

public static class AdapterSession
{
    public static Guid? Id { get; set; }

    public const string HeaderName = "X-Overmind-Session";
}
