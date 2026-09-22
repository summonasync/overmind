using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class ShutdownApiTests
{
    [Fact]
    public async Task Shutdown_ReturnsAccepted_AndTriggersApplicationStopping()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var lifetime = factory.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.IsCancellationRequested.ShouldBeFalse();

        var response = await client.PostAsync("/api/shutdown", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        lifetime.ApplicationStopping.WaitHandle
            .WaitOne(TimeSpan.FromSeconds(10))
            .ShouldBeTrue("the host should begin stopping after the response completes");
    }
}
