using Overmind.Infrastructure.Agent.Routes;
using Qdrant.Client.Grpc;
using static Qdrant.Client.Grpc.Conditions;

namespace Overmind.Infrastructure.VectorSearch;

public static class RouteVectorCollection
{
    public const string Name = "overmind_routes";
    public const int MatchTopK = 5;

    public const string RouteId = "routeId";
    public const string Domain = "domain";
    public const string Target = "target";
    public const string Status = "status";
    public const string HitCount = "hitCount";

    public static Qdrant.Client.Grpc.Filter AllRoutesFilter()
    {
        var filter = new Filter();
        filter.MustNot.Add(HasId(EmbeddingFingerprint.MarkerPointId));
        return filter;
    }

    public static PointStruct ToPoint(Guid routeId, string patternText, RouteVectorPayload payload, float[] vector) =>
        new()
        {
            Id = routeId,
            Vectors = vector,
            Payload =
            {
                [RouteId] = routeId.ToString(),
                [Domain] = payload.Domain,
                [Target] = payload.Target ?? string.Empty,
                [Status] = payload.Status,
                [HitCount] = payload.HitCount,
            },
        };

    public static RouteVectorMatch? ToMatch(ScoredPoint point)
    {
        var payload = point.Payload;
        if (!payload.TryGetValue(RouteId, out var routeId) ||
            !Guid.TryParse(routeId.StringValue, out var id) ||
            !payload.TryGetValue(Domain, out var domain))
        {
            return null;
        }

        var target = payload.TryGetValue(Target, out var targetValue) ? targetValue.StringValue : null;
        return new RouteVectorMatch(id, domain.StringValue, target, point.Score);
    }
}
