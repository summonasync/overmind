using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class GrimoireApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public GrimoireApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Status_FreshDatabase_ReturnsCompositePayloadWithoutWiring()
    {
        using var response = await _client.GetAsync("/api/grimoire/status");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("wired").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("bookName").GetString().ShouldBeEmpty();
        document.RootElement.GetProperty("overridePath").GetString().ShouldBeEmpty();
        document.RootElement.GetProperty("installChannel").GetString().ShouldBeEmpty();
        document.RootElement.GetProperty("status").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Wire_MissingBookName_ReturnsValidationProblem()
    {
        using var response = await _client.PostAsJsonAsync("/api/grimoire/wire", new { bookName = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("errors").GetProperty("BookName").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AssistedInstall_AssistedIntentWithoutChannel_ReturnsDegradedInstructions()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/grimoire/assisted-install", new { intent = "assisted", channel = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("degraded").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("instructions").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AssistedInstall_ManualIntent_ReturnsDegradedInstructions()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/grimoire/assisted-install", new { intent = "manual", channel = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("degraded").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        document.RootElement.GetProperty("instructions").EnumerateArray().ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("telepathy")]
    public async Task AssistedInstall_MissingOrUnknownIntent_ReturnsBadRequest(string? intent)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/grimoire/assisted-install", new { intent, channel = (string?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("detail").GetString()!.ShouldContain("intent");
    }
}
