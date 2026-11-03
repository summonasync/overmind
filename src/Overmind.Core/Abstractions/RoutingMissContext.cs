namespace Overmind.Core;

public sealed record RoutingMissContext(string Reason, int HotHeadSize)
{
    public string Describe() =>
        $"Routing context from the intent router: {Reason}; hot head rows consulted: {HotHeadSize}.";
}
