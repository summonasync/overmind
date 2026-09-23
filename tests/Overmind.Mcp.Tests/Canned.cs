using System.Text.Json.Nodes;
using Shouldly;

namespace Overmind.Mcp.Tests;

public static class Canned
{
    public static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SecondProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid EntryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MissingEntryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid PlanningSpecArtifactId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid PlanningPlanArtifactId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid PlanningTaskOneId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid PlanningTaskTwoId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid NotificationId = Guid.Parse("13131313-1313-1313-1313-131313131313");

    public const string PlanningSlug = "demo-feature";
    public const string PlanningStopSlug = "gate-locked";
    public const string PlanningEmptySlug = "all-done";

    public const string HealthJson = """{"status":"ok","vectorSearch":"up","version":"test"}""";

    public const string ProjectsJson =
        """
        [
          {"id":"11111111-1111-1111-1111-111111111111","name":"alpha","diskPath":"E:\\work\\alpha","description":"first project","createdAt":"2026-09-01T10:00:00Z"},
          {"id":"22222222-2222-2222-2222-222222222222","name":"beta","diskPath":"E:\\work\\beta","description":"","createdAt":"2026-09-02T10:00:00Z"}
        ]
        """;

    public const string ProjectJson =
        """{"id":"11111111-1111-1111-1111-111111111111","name":"alpha","diskPath":"E:\\work\\alpha","description":"first project","createdAt":"2026-09-01T10:00:00Z"}""";

    public const string EntryJson =
        """{"id":"33333333-3333-3333-3333-333333333333","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Entry One","content":"Entry body here.","currentVersion":3,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-03T10:00:00Z","tags":["core"],"skillFiles":[]}""";

    public const string GatesJson =
        """
        [
          {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","projectId":"11111111-1111-1111-1111-111111111111","type":"gate","title":"Gate One","content":"Always run the full test suite.","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]},
          {"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","projectId":"11111111-1111-1111-1111-111111111111","type":"gate","title":"Gate Two","content":"Never skip review.","currentVersion":2,"createdAt":"2026-09-01T11:00:00Z","updatedAt":"2026-09-02T11:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    public const string GuardsJson =
        """
        [
          {"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","projectId":"11111111-1111-1111-1111-111111111111","type":"guard","title":"Guard One","content":"Do not touch generated code.","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    public const string NotesJson =
        """
        [
          {"id":"33333333-3333-3333-3333-333333333333","projectId":"11111111-1111-1111-1111-111111111111","type":"note","title":"Note One","content":"Some note body.","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    public const string DecisionsJson =
        """
        [
          {"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","projectId":"11111111-1111-1111-1111-111111111111","type":"decision","title":"Decision One","content":"Use SQLite for storage.","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    public const string SkillsJson =
        """
        [
          {"id":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee","projectId":"11111111-1111-1111-1111-111111111111","type":"skill","title":"Skill One","content":"Skill instructions body.","currentVersion":1,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","tags":[],"skillFiles":[]}
        ]
        """;

    public const string SearchJson =
        """{"query":"hello","hits":[{"entryId":"33333333-3333-3333-3333-333333333333","title":"Entry One","type":"note","snippet":"hello world snippet","rank":0.95}]}""";

    public const string NotFoundJson =
        """{"title":"Not Found","detail":"Entry '44444444-4444-4444-4444-444444444444' was not found.","status":404}""";

    public const string SemanticUnavailableJson =
        """{"title":"Semantic search unavailable","detail":"Semantic search is unavailable because the vector store is down. Use mode=fulltext.","status":503}""";

    public const string PlanningBoardJson =
        """
        [
          {"slug":"demo-feature","title":"Demo feature","status":"inProgress","priority":"p1","version":4,"specStatus":"validated","planStatus":"draft","taskTotal":2,"taskDone":1,"updatedAt":"2026-09-10T08:00:00Z","isCurrent":true},
          {"slug":"draft-one","title":"Draft one","status":"draft","priority":"p2","version":1,"specStatus":null,"planStatus":null,"taskTotal":0,"taskDone":0,"updatedAt":"2026-09-09T09:00:00Z","isCurrent":false}
        ]
        """;

    public const string PlanningFeatureJson =
        """
        {
          "slug": "demo-feature",
          "title": "Demo feature",
          "body": "<goal>\nShip the demo.\n</goal>",
          "status": "inProgress",
          "priority": "p1",
          "version": 4,
          "createdAt": "2026-09-08T10:00:00Z",
          "updatedAt": "2026-09-10T08:00:00Z",
          "artifacts": [
            {"id":"66666666-6666-6666-6666-666666666666","kind":"spec","status":"validated","review":"light","order":0,"body":"# Demo spec\n\nShip the demo.","frozenAt":"2026-09-09T10:00:00Z","version":2,"createdAt":"2026-09-08T10:05:00Z","updatedAt":"2026-09-09T10:00:00Z","log":[{"date":"2026-09-09","text":"frozen by the user"}]},
            {"id":"77777777-7777-7777-7777-777777777777","kind":"plan","status":"draft","review":"light","order":0,"body":"# Demo plan\n\nTwo tasks.","frozenAt":null,"version":1,"createdAt":"2026-09-08T11:00:00Z","updatedAt":"2026-09-08T11:00:00Z","log":[]},
            {"id":"88888888-8888-8888-8888-888888888888","kind":"task","status":"done","review":"light","order":1,"body":"# Build the domain\n\n<goal>Model it.</goal>","frozenAt":null,"version":1,"createdAt":"2026-09-08T11:05:00Z","updatedAt":"2026-09-09T12:00:00Z","log":[]},
            {"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"inProgress","review":"hard","order":2,"body":"# Wire the API\n\n<goal>Expose the endpoints.</goal>","frozenAt":null,"version":1,"createdAt":"2026-09-08T11:10:00Z","updatedAt":"2026-09-10T07:30:00Z","log":[]}
          ]
        }
        """;

    public const string PlanningTaskTwoJson =
        """{"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"inProgress","review":"hard","order":2,"body":"# Wire the API\n\n<goal>Expose the endpoints.</goal>","frozenAt":null,"version":1,"createdAt":"2026-09-08T11:10:00Z","updatedAt":"2026-09-10T07:30:00Z","log":[]}""";

    public const string PlanningArtifactReadJson =
        """{"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"inProgress","version":1,"order":2,"updatedAt":"2026-09-10T07:30:00Z","brief":"Wire the endpoints","body":"# Wire the API\n\n<goal>\nExpose the endpoints.\n</goal>\n\n<obstacles>\nNone yet.\n</obstacles>\n"}""";

    public const string PlanningArtifactSectionedJson =
        """{"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"inProgress","version":1,"order":2,"updatedAt":"2026-09-10T07:30:00Z","brief":"Wire the endpoints","body":"<goal>\nExpose the endpoints.\n</goal>","missingSections":["obstacles"]}""";

    public const string PlanningArtifactNotFoundJson =
        """{"title":"Not Found","detail":"Artifact '99999999-9999-9999-9999-999999999999' was not found.","status":404}""";

    public const string PlanningCurrentTaskJson =
        """
        {"state":"task","feature":{"slug":"demo-feature","title":"Demo feature","status":"inProgress"},"artifact":{"id":"99999999-9999-9999-9999-999999999999","kind":"task","status":"inProgress","review":"hard","order":2,"body":"# Wire the API\n\n<goal>Expose the endpoints.</goal>","frozenAt":null,"version":1,"createdAt":"2026-09-08T11:10:00Z","updatedAt":"2026-09-10T07:30:00Z","log":[]},"reason":null}
        """;

    public const string PlanningCurrentStopJson =
        """
        {"state":"stop","feature":{"slug":"gate-locked","title":"Gate locked","status":"ready"},"artifact":null,"reason":"STOP: spec awaits human validation — it cannot be approved through MCP."}
        """;

    public const string PlanningCurrentEmptyJson =
        """
        {"state":"empty","feature":{"slug":"all-done","title":"All done","status":"inProgress"},"artifact":null,"reason":"All tasks are done — write the result artifact."}
        """;

    public const string PlanningValidateGateJson =
        """{"title":"HumanValidationRequired","detail":"STOP: the spec of feature 'demo-feature' awaits human validation — ask the user to approve it in the Overmind UI.","status":409}""";

    public const string PlanningFeatureNotFoundJson =
        """{"title":"Not Found","detail":"Feature 'demo-feature' was not found.","status":404}""";

    public const string OnboardQuickstartText = "Onboarding quickstart from the host — verbatim payload text.";

    public const string NotificationJson =
        """{"id":"13131313-1313-1313-1313-131313131313","projectId":"11111111-1111-1111-1111-111111111111","kind":"question","featureSlug":"demo-feature","tab":"spec","text":"Spec gate wants your call on the persistence model.","agent":"coder","createdAt":"2026-09-20T10:00:00Z","readAt":null,"replyText":null,"repliedAt":null}""";

    public const string RepliesJson =
        """{"items":[{"id":"13131313-1313-1313-1313-131313131313","projectId":"11111111-1111-1111-1111-111111111111","kind":"question","featureSlug":"demo-feature","tab":"spec","text":"Spec gate wants your call on the persistence model.","agent":"coder","createdAt":"2026-09-20T10:00:00Z","readAt":"2026-09-20T10:05:00Z","replyText":"Ship it as planned.","repliedAt":"2026-09-20T10:05:00Z"}]}""";

    public const string OnboardJson =
        """
        {"projectId":"11111111-1111-1111-1111-111111111111","projectName":"alpha","created":true,"board":{"countsByStatus":{"inProgress":1,"draft":1},"currentSlug":"demo-feature"},"materialization":{"strategy":"on","targetDir":"/repo/BACKLOG","trigger":"Event-driven: runs automatically after every planning mutation.","lastRun":"2026-09-23T10:15:00Z"},"validation":{"humanGate":false},"quickstart":"Onboarding quickstart from the host — verbatim payload text."}
        """;

    public const string OnboardOffStrategyJson =
        """
        {"projectId":"11111111-1111-1111-1111-111111111111","projectName":"alpha","created":false,"board":{"countsByStatus":{"draft":1},"currentSlug":null},"materialization":{"strategy":"off","targetDir":"/repo/BACKLOG","trigger":"Materialization is off for this project — the materializer will not run.","lastRun":null},"validation":{"humanGate":true},"quickstart":"Onboarding quickstart from the host — verbatim payload text."}
        """;

    public const string OnboardCyrillicJson =
        """
        {"projectId":"11111111-1111-1111-1111-111111111111","projectName":"кириллический проект","created":false,"board":{"countsByStatus":{"draft":1},"currentSlug":null},"materialization":{"strategy":"on","targetDir":"/repo/BACKLOG","trigger":"Event-driven: runs automatically after every planning mutation.","lastRun":null},"validation":{"humanGate":false},"quickstart":"Быстрый старт — кириллица едет литералом, включая тег <goal> и стрелку →."}
        """;

    public static string EntryMarkdown(string title, string content) => $"# {title}\n\n{content}\n";

    public static int StoredLength(string json, string field) =>
        JsonNode.Parse(json)![field]!.GetValue<string>().Length;

    public static string ToolResultText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        if (result!["structuredContent"] is JsonNode structured)
        {
            return structured.ToJsonString();
        }

        var content = result["content"]?.AsArray();
        content.ShouldNotBeNull("tool result has neither structuredContent nor content");
        return content!.First()!["text"]!.GetValue<string>();
    }

    public static string ResourceText(JsonNode response)
    {
        var result = response["result"];
        result.ShouldNotBeNull("expected a JSON-RPC result, got: " + response.ToJsonString());
        var contents = result!["contents"]?.AsArray();
        contents.ShouldNotBeNull("resources/read result has no contents");
        return contents!.First()!["text"]!.GetValue<string>();
    }
}
