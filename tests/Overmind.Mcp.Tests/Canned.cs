using System.Text.Json.Nodes;
using Shouldly;

namespace Overmind.Mcp.Tests;

public static class Canned
{
    public static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SecondProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid EntryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MissingEntryId = Guid.Parse("44444444-4444-4444-4444-444444444444");

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

    public static string EntryMarkdown(string title, string content) => $"# {title}\n\n{content}\n";

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
