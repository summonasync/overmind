using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class FrontmatterKeyApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public FrontmatterKeyApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Get_ReturnsSeededRegistrySortedByName()
    {
        using var response = await _client.GetAsync("/api/frontmatter-keys");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.EnumerateArray().ToList();

        var expected = new Dictionary<string, string>
        {
            ["created"] = "#38bdf8",
            ["created_during"] = "#e879f9",
            ["decision"] = "#f472b6",
            ["depends_on"] = "#fb923c",
            ["feature"] = "#a855f7",
            ["phase"] = "#22d3ee",
            ["review"] = "#f59e0b",
            ["slug"] = "#2dd4bf",
            ["status"] = "#22c55e",
            ["task"] = "#818cf8",
            ["validated"] = "#a3e635",
            ["version"] = "#60a5fa",
        };

        items.Count.ShouldBe(expected.Count);

        var names = items.Select(i => i.GetProperty("name").GetString()!).ToList();
        names.ShouldBe(names.OrderBy(n => n, StringComparer.Ordinal).ToList());

        foreach (var (name, color) in expected)
        {
            var item = items.Single(i => i.GetProperty("name").GetString() == name);
            item.GetProperty("color").GetString().ShouldBe(color);
        }
    }
}
