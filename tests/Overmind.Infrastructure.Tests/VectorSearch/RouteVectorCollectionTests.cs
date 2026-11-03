using Overmind.Core;
using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.VectorSearch;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.VectorSearch;

public sealed class RouteVectorCollectionTests
{
    [Fact]
    public void ToPoint_CarriesTheRoutePayload()
    {
        var routeId = Guid.NewGuid();
        var payload = new RouteVectorPayload("planning", "worker", "confirmed", 4);

        var point = RouteVectorCollection.ToPoint(routeId, "sort board", payload, [0.1f, 0.2f]);

        point.Payload[RouteVectorCollection.Domain].StringValue.ShouldBe("planning");
        point.Payload[RouteVectorCollection.Target].StringValue.ShouldBe("worker");
        point.Payload[RouteVectorCollection.Status].StringValue.ShouldBe("confirmed");
        point.Payload[RouteVectorCollection.HitCount].IntegerValue.ShouldBe(4);
        Guid.Parse(point.Payload[RouteVectorCollection.RouteId].StringValue).ShouldBe(routeId);
    }

    [Fact]
    public void ToPoint_NullTarget_IsStoredAsEmptyString()
    {
        var point = RouteVectorCollection.ToPoint(
            Guid.NewGuid(), "sort board", new RouteVectorPayload("planning", null, "provisional", 1), [0.1f]);

        point.Payload[RouteVectorCollection.Target].StringValue.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToMatch_RestoresTheMatchFromThePayload()
    {
        var routeId = Guid.NewGuid();
        var point = RouteVectorCollection.ToPoint(
            routeId, "sort board", new RouteVectorPayload("planning", "worker", "provisional", 2), [0.1f]);
        var scored = new ScoredPoint { Id = point.Id, Score = 0.9f };
        foreach (var pair in point.Payload)
        {
            scored.Payload.Add(pair.Key, pair.Value);
        }

        var match = RouteVectorCollection.ToMatch(scored);

        match.ShouldNotBeNull();
        match.RouteId.ShouldBe(routeId);
        match.Domain.ShouldBe("planning");
        match.Target.ShouldBe("worker");
        match.Score.ShouldBe(0.9f);
    }

    [Fact]
    public void ToMatch_MalformedPayload_ReturnsNullWithoutThrowing()
    {
        var point = new ScoredPoint { Score = 0.9f };
        point.Payload.Add(RouteVectorCollection.Domain, "planning");
        point.Payload.Add(RouteVectorCollection.RouteId, "not-a-guid");

        RouteVectorCollection.ToMatch(point).ShouldBeNull();
    }

    [Fact]
    public void ToMatch_BlankTarget_FallsBackToTheWorkerDefault()
    {
        var point = RouteVectorCollection.ToPoint(
            Guid.NewGuid(), "sort board", new RouteVectorPayload("planning", null, "provisional", 1), [0.1f]);
        var scored = new ScoredPoint { Id = point.Id, Score = 0.9f };
        foreach (var pair in point.Payload)
        {
            scored.Payload.Add(pair.Key, pair.Value);
        }

        var match = RouteVectorCollection.ToMatch(scored);

        match.ShouldNotBeNull();
        match.Target.ShouldBe(RoutingTargets.Worker);
    }

    [Fact]
    public void Threshold_GatesTheRoutingDecision()
    {
        new RouteVectorMatch(Guid.NewGuid(), "planning", "worker", RouteVectorMatch.Threshold)
            .IsRoutable.ShouldBeTrue();
        new RouteVectorMatch(Guid.NewGuid(), "planning", "worker", RouteVectorMatch.Threshold - 0.01f)
            .IsRoutable.ShouldBeFalse();
        new RouteVectorMatch(Guid.NewGuid(), "planning", "worker", 0.99f)
            .IsRoutable.ShouldBeTrue();
    }

    [Fact]
    public void CollectionName_IsDedicatedToRoutes() =>
        RouteVectorCollection.Name.ShouldBe("overmind_routes");
}
