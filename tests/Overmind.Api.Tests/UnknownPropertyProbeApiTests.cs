using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Overmind.Api.Tests;

// 266 T3 row 2 — the anti-pattern door: an unreferenced top-level JSON field used to be swallowed by
// the binder with no trace. Now the write is still accepted, the fields the endpoint reads are
// applied exactly as before, and the field nothing reads is named in the existing warnings[]
// channel. Rows 4 and 9 answer at the same HTTP doors, so their pins ride along here.
public sealed class UnknownPropertyProbeApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public UnknownPropertyProbeApiTests(ITestOutputHelper output)
    {
        _client = _factory.CreateClient();
        _output = output;
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---- row 2: the swallowed field (the marshal's update_feature case) ----

    [Fact]
    public async Task UpdateFeature_UnknownTopLevelField_IsNamedInWarnings_AndTheWriteStillLands()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(UpdateUrl(project.Id, "feat"), new
        {
            Title = "Renamed by the marshal",
            LogNote = "one stray field",
            laneHolder = "mark-wolff",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        var warning = payload["warnings"]!.AsArray().Single()!.AsObject();
        warning["property"]!.GetValue<string>().ShouldBe("laneHolder");
        warning["applied"]!.GetValue<bool>().ShouldBeFalse();
        warning["note"]!.GetValue<string>().ShouldContain("nothing was applied under this name");

        var feature = await GetFeatureAsync(project.Id, "feat");
        feature.Title.ShouldBe("Renamed by the marshal");
    }

    [Fact]
    public async Task UpdateFeature_OnlyKnownFields_KeepsThePreProbeShapeByteForByte()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(UpdateUrl(project.Id, "feat"), new
        {
            Title = "Renamed",
            LogNote = "no stray field",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();

        // The negative half the row demands: a known field is applied WITHOUT any warning — and the
        // echo carries exactly the keys it carried before the probe existed.
        JsonDocument.Parse(raw).RootElement.EnumerateObject().Select(p => p.Name).ToArray()
            .ShouldBe(["slug", "status", "version", "updatedAt", "bodyChars"]);
    }

    [Fact]
    public async Task UpdateFeature_KnownFieldInAnotherCasing_IsAppliedNotWarned()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(UpdateUrl(project.Id, "feat"), new JsonObject
        {
            ["TITLE"] = "Casing is not a new field",
            ["logNote"] = "uppercase key",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response))["warnings"].ShouldBeNull();
        (await GetFeatureAsync(project.Id, "feat")).Title.ShouldBe("Casing is not a new field");
    }

    [Fact]
    public async Task UpdateFeature_UnknownBesideACapWarning_BothChannelsSurvive()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(UpdateUrl(project.Id, "feat"), new
        {
            Title = "Cap and stray",
            LogNote = new string('n', 2_001),
            codeSurface = new[] { "src/x/**" },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var warnings = (await ReadAsync(response))["warnings"]!.AsArray();

        var cap = warnings.Single(node => node!.AsObject().ContainsKey("accepted_len"))!.AsObject();
        cap["limit"]!.GetValue<int>().ShouldBe(2_000);
        var stray = warnings.Single(node => node!.AsObject().ContainsKey("property"))!.AsObject();
        stray["property"]!.GetValue<string>().ShouldBe("codeSurface");

        // codeSurfaces (the real field) is untouched by the near-miss on codeSurface.
        var feature = await GetFeatureAsync(project.Id, "feat");
        feature.CodeSurfaces.ShouldBeEmpty();
    }

    [Fact]
    public async Task Notify_UnknownField_WarnsOnADoorOutsidePlanning()
    {
        var project = await CreateProjectAsync();

        var response = await _client.PostAsJsonAsync("/api/agent/notifications", new
        {
            Kind = "question",
            Text = "pick the persistence model",
            ProjectId = project.Id,
            titel = "the notify case from the survey",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var warning = (await ReadAsync(response))["warnings"]!.AsArray().Single()!.AsObject();
        warning["property"]!.GetValue<string>().ShouldBe("titel");
        warning["applied"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateFeature_UnknownFieldOnARejection_LeavesTheProblemBodyUntouched()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(UpdateUrl(project.Id, "feat"), new
        {
            stray = "no logNote here",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadAsync(response);
        problem["warnings"].ShouldBeNull();
        problem["errors"]!.AsObject()["LogNote"]!.AsArray().Single().GetValue<string>()
            .ShouldContain("must not be empty");
    }

    [Fact]
    public async Task BatchOps_UnknownFieldInsideAnItem_IsOutOfScope_TopLevelOnly()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var taskId = await CreateArtifactAsync(
            project.Id, "feat", "task", "<goal>\nwork\n</goal>\n<acceptance-criteria>\n- [ ] hold\n</acceptance-criteria>\n");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/batch",
            new
            {
                Ops = new object[]
                {
                    new
                    {
                        Op = "patch",
                        ArtifactId = taskId,
                        Patch = new { Op = "tick_ac", AcIndex = 1, strayInside = true },
                    },
                },
                strayOutside = true,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await ReadAsync(response);
        var stray = payload["warnings"]!.AsArray().Single(node => node!.AsObject().ContainsKey("property"))!
            .AsObject();
        stray["property"]!.GetValue<string>().ShouldBe("strayOutside");
        payload["warnings"]!.AsArray().Count(node => node!.AsObject().ContainsKey("property")).ShouldBe(1);

        var ticked = await ReadJsonAsync<ArtifactBody>(
            $"/api/agent/planning/artifacts/{taskId}");
        ticked.Body.ShouldContain("- [x] hold");
    }

    // ---- row 4: the frequency leader, wording upgrade at the real door ----

    [Fact]
    public async Task PatchArtifact_DuplicateHeadingLine_StatesTheByteStableRuleAndTheStripRemedy()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var specId = await CreateArtifactAsync(project.Id, "feat",
            "# Spec\n\n## Scope\n\nfirst body.\n\n## Scope\n\nsecond body.\n");

        var response = await _client.PostAsJsonAsync(ArtifactPatchUrl(specId), new
        {
            Op = "replace_section",
            Section = "## Scope",
            Content = "replacement body.\n",
            LogNote = "which scope did that rewrite?",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var detail = (await ReadAsync(response))["detail"]!.GetValue<string>();
        detail.ShouldContain("matches 2 H2 headings");
        detail.ShouldContain("the heading line itself stays byte-stable");
        detail.ShouldContain("resend with that line stripped from the content");
        detail.ShouldContain("merge or rename the duplicates");
    }

    // ---- row 9: the tombstone reason is one line, at every door that takes it ----

    [Fact]
    public async Task FeaturePatch_MultilineRemoveReason_IsRefusedAndNothingIsTombstoned()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/patch",
            new { Section = "goal", Remove = true, Reason = "two lines\nof reason", LogNote = "newline reason" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response))["detail"]!.GetValue<string>().ShouldContain("must be single-line text");
        (await GetFeatureAsync(project.Id, "feat")).Body.ShouldNotContain("<removed");
    }

    [Fact]
    public async Task ArtifactPatch_MultilineRemoveReason_IsRefusedBeforeTheCap()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var specId = await CreateArtifactAsync(project.Id, "feat",
            "# Spec\n\n## Scope\n\nfirst body.\n");

        var response = await _client.PostAsJsonAsync(ArtifactPatchUrl(specId), new
        {
            Op = "remove_section",
            Section = "## Scope",
            Reason = "two lines\nof reason",
            LogNote = "newline reason",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response))["detail"]!.GetValue<string>().ShouldContain("must be single-line text");
        (await ReadJsonAsync<ArtifactBody>($"/api/agent/planning/artifacts/{specId}")).Body
            .ShouldNotContain("<removed");
    }

    [Fact]
    public async Task BatchRemove_MultilineReason_IsRefusedAsThatOp()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");
        var specId = await CreateArtifactAsync(project.Id, "feat",
            "# Spec\n\n## Scope\n\nfirst body.\n");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/batch",
            new
            {
                Ops = new object[]
                {
                    new
                    {
                        Op = "patch",
                        ArtifactId = specId,
                        Patch = new { Op = "remove_section", Section = "## Scope", Reason = "a\nb" },
                    },
                },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response))["detail"]!.GetValue<string>().ShouldContain("single-line");
    }

    [Fact]
    public async Task FeaturePatch_MultilineReasonPastTheCap_IsNotLaunderedByTruncation()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/patch",
            new
            {
                Section = "goal",
                Remove = true,
                Reason = new string('r', 700) + "\nburied by the cut",
                LogNote = "long reason with a newline",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response))["detail"]!.GetValue<string>().ShouldContain("single-line");
    }

    [Fact]
    public async Task FeaturePatch_SingleLineRemoveReason_StillTombstones()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project.Id, "feat", "<goal>\nhold the line.\n</goal>\n");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/feat/patch",
            new { Section = "goal", Remove = true, Reason = "moved to the spec", LogNote = "tombstone" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetFeatureAsync(project.Id, "feat")).Body.ShouldMatch(@"<removed \d{4}-\d{2}-\d{2} — moved to the spec>");
    }

    // ---- row 8 measurement at the Host door: replies carry no window of their own ----

    [Fact]
    public async Task Replies_NoSinceFilter_ReturnsTheReply_TheHostHoldsNoWindow()
    {
        var project = await CreateProjectAsync();
        var notify = await _client.PostAsJsonAsync("/api/agent/notifications",
            new { Kind = "approval", Text = "approve the plan", ProjectId = project.Id });
        var notificationId = Guid.Parse((await ReadAsync(notify))["id"]!.GetValue<string>());

        var reply = await _client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/reply", new { Text = "ship it" }, JsonOptions);
        reply.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.GetAsync($"/api/agent/notifications/replies?projectId={project.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await ReadAsync(response))["items"]!.AsArray();

        // Measured, not assumed: with no since the door answers with every reply the project has —
        // there is no 7-day bound behind it. The adapter-side projection is what shrinks the bytes.
        items.Single()!["replyText"]!.GetValue<string>().ShouldBe("ship it");
        _output.WriteLine($"replies rows without since: {items.Count}");
    }

    private static string UpdateUrl(Guid projectId, string slug) =>
        $"/api/agent/projects/{projectId}/planning/features/{slug}/update";

    private static string ArtifactPatchUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/patch";

    private async Task<FeatureRow> GetFeatureAsync(Guid projectId, string slug) =>
        await ReadJsonAsync<FeatureRow>($"/api/agent/projects/{projectId}/planning/features/{slug}");

    private async Task<T> ReadJsonAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(body)!;
    }

    private async Task<ProjectRow> CreateProjectAsync()
    {
        var name = $"probe-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectRow>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string? body = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Title = $"Feature {slug}", Slug = slug, Body = body });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, string body) =>
        await CreateArtifactAsync(projectId, slug, "spec", body);

    private async Task<Guid> CreateArtifactAsync(Guid projectId, string slug, string kind, string body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts",
            new { Kind = kind, Body = body });
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        return Guid.Parse((await ReadAsync(response))["id"]!.GetValue<string>());
    }

    private sealed record ProjectRow(Guid Id, string Name);

    private sealed record FeatureRow(string Slug, string Title, string Body, string[] CodeSurfaces);

    private sealed record ArtifactBody(Guid Id, string Kind, string Body);
}
