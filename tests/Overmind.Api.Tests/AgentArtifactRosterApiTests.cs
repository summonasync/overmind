using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentArtifactRosterApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentArtifactRosterApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Roster_MixedFeature_DocsFirstThenTasksWithoutBodyFields()
    {
        var projectId = await SeedMixedFeatureAsync();

        var response = await _client.GetAsync(RosterUrl(projectId, "roster-feat"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain("SECRET-ROSTER-BODY");
        raw.ShouldNotContain("\"body\"");

        var roster = JsonNode.Parse(raw)!;
        var items = roster["artifacts"]!.AsArray();
        items.Count.ShouldBe(5);
        items.Select(i => i!["kind"]!.GetValue<string>()).ShouldBe(["spec", "plan", "result", "task", "task"]);
        items.Select(i => i!["order"]!.GetValue<int>()).ShouldBe([0, 0, 0, 2, 5]);

        var spec = items[0]!;
        spec["artifactId"].ShouldNotBeNull();
        spec["version"].ShouldNotBeNull();
        spec["updatedAt"].ShouldNotBeNull();
        spec["review"].ShouldBeNull();
        spec["acTicked"].ShouldBeNull();
        spec["acTotal"].ShouldBeNull();
        spec["brief"].ShouldBeNull();
        spec["pointers"]!.AsArray().Count.ShouldBe(1);
        spec["pointers"]![0]!["path"]!.GetValue<string>().ShouldBe("docs/roster-spec.md");
        spec["pointers"]![0]!["anchor"]!.GetValue<string>().ShouldBe("non-goals");

        var plan = items[1]!;
        plan["pointers"]!.ToJsonString().ShouldBe("[]");

        var firstTask = items[3]!;
        firstTask["review"]!.GetValue<string>().ShouldBe("light");
        firstTask["acTicked"]!.GetValue<int>().ShouldBe(2);
        firstTask["acTotal"]!.GetValue<int>().ShouldBe(2);
        firstTask["brief"]!.GetValue<string>().ShouldBe("Read the roster");

        var secondTask = items[4]!;
        secondTask["review"]!.GetValue<string>().ShouldBe("hard");
        secondTask["acTicked"]!.GetValue<int>().ShouldBe(1);
        secondTask["acTotal"]!.GetValue<int>().ShouldBe(3);
        secondTask["brief"]!.GetValue<string>().ShouldBe("Wire the roster");
    }

    [Fact]
    public async Task Roster_KindFilter_NarrowsCaseInsensitively()
    {
        var projectId = await SeedMixedFeatureAsync();

        var tasks = await _client.GetFromJsonAsync<RosterJson>(
            RosterUrl(projectId, "roster-feat") + "?kind=task", JsonOptions);
        tasks!.Artifacts.Select(a => a.Kind).ShouldBe(["task", "task"]);
        tasks.Artifacts.Select(a => a.Order).ShouldBe([2, 5]);

        var specs = await _client.GetFromJsonAsync<RosterJson>(
            RosterUrl(projectId, "roster-feat") + "?kind=SPEC", JsonOptions);
        specs!.Artifacts.Select(a => a.Kind).ShouldBe(["spec"]);
        specs.Artifacts[0].Review.ShouldBeNull();
    }

    [Fact]
    public async Task Roster_UnknownKind_Returns400ListingAllowedValues()
    {
        var projectId = await SeedMixedFeatureAsync();

        var response = await _client.GetAsync(RosterUrl(projectId, "roster-feat") + "?kind=bogus");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("spec");
        problem.Detail.ShouldContain("plan");
        problem.Detail.ShouldContain("task");
        problem.Detail.ShouldContain("result");
    }

    [Fact]
    public async Task Roster_UnknownSlug_Returns404SameShapeAsGetFeature()
    {
        var projectId = await SeedMixedFeatureAsync();

        var roster = await _client.GetAsync(RosterUrl(projectId, "missing-feat"));
        var feature = await _client.GetAsync($"/api/agent/projects/{projectId}/planning/features/missing-feat");

        roster.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        feature.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var rosterProblem = await roster.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        var featureProblem = await feature.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        rosterProblem!.Title.ShouldBe(featureProblem!.Title);
        rosterProblem.Detail.ShouldBe(featureProblem.Detail);
        rosterProblem.Detail!.ShouldContain("missing-feat");
    }

    private async Task<Guid> SeedMixedFeatureAsync()
    {
        var name = $"agent-roster-{Guid.NewGuid():N}";
        var projectResponse = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });
        projectResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);

        var featureResponse = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project!.Id}/planning/features",
            new { Slug = "roster-feat", Title = "Roster feature", Body = "" });
        featureResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        await CreateArtifactAsync(project.Id, new
        {
            Kind = "spec",
            Body = """
                # Spec

                SECRET-ROSTER-BODY

                pointer: docs/roster-spec.md#non-goals

                <acceptance-criteria>
                - [x] spec level item
                </acceptance-criteria>
                """,
        });
        await CreateArtifactAsync(project.Id, new { Kind = "plan", Body = "# Plan\n\n<goal>Ship it.</goal>" });
        await CreateArtifactAsync(project.Id, new { Kind = "result", Body = "# Result\n\nPending." });

        var tasksResponse = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/roster-feat/artifacts/tasks",
            new
            {
                Tasks = new object[]
                {
                    new
                    {
                        Order = 5,
                        Review = "hard",
                        Brief = "Wire the roster",
                        Body = """
                            <goal>Wire the roster endpoint.</goal>

                            <acceptance-criteria>
                            - [x] route registered
                            - [ ] handler sorts docs first
                            - [ ] tests cover the shape
                            </acceptance-criteria>
                            """,
                    },
                    new
                    {
                        Order = 2,
                        Brief = "Read the roster",
                        Body = """
                            <goal>Read the roster from tests.</goal>

                            <acceptance-criteria>
                            - [x] shape asserted
                            - [x] order asserted
                            </acceptance-criteria>
                            """,
                    },
                },
            });
        tasksResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        return project.Id;
    }

    private async Task CreateArtifactAsync(Guid projectId, object body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/roster-feat/artifacts", body);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    private static string RosterUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts";

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record RosterJson(RosterItemJson[] Artifacts);

    private sealed record RosterItemJson(
        Guid ArtifactId, string Kind, string Status, int Version, int Order, DateTime UpdatedAt,
        string? Review = null, int? AcTicked = null, int? AcTotal = null, string? Brief = null);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
