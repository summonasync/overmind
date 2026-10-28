namespace Overmind.Mcp;

public static class AdapterIdentityState
{
    public sealed record Identity(string Alias, string Vendor);

    private static readonly object Gate = new();
    private static Identity? _current;

    public static void Set(string alias, string vendor)
    {
        lock (Gate)
        {
            _current = new Identity(alias, vendor);
        }
    }

    public static Identity? TryGet()
    {
        lock (Gate)
        {
            return _current;
        }
    }

    internal static void Reset()
    {
        lock (Gate)
        {
            _current = null;
        }
    }
}
