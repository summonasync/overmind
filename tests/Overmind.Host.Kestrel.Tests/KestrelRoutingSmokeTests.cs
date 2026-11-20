using System.Net;
using Shouldly;
using Xunit;

namespace Overmind.Host.Kestrel.Tests;

[CollectionDefinition("overmind kestrel host")]
public sealed class KestrelHostCollection : ICollectionFixture<KestrelApiFactory>;

[Trait("Host", "Kestrel")]
[Collection("overmind kestrel host")]
public sealed class KestrelRoutingSmokeTests(KestrelApiFactory factory)
{
    [Fact]
    public async Task FullPipelineAnswersOverRealSocket()
    {
        using var client = factory.CreateWireClient();

        using var health = await client.GetAsync("/api/health");
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        using var missing = await client.GetAsync("/kestrel-smoke-missing-page");
        missing.StatusCode.ShouldBe(HttpStatusCode.OK);
        missing.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }
}
