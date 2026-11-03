namespace Overmind.Core;

public sealed record RoutingDecision(string Target, RoutingSource Source, RoutingMissContext Miss);
