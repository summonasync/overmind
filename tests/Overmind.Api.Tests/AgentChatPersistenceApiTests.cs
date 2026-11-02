using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentChatPersistenceApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Conversations_SurviveRestart_WithHistoryIntact()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-chat-restart-{Guid.NewGuid():N}.db");
        try
        {
            Guid conversationId;
            string assistantReply;
            using (var first = new ApiFactory(databasePath, preserveDatabase: true))
            {
                using var client = first.CreateClient();
                var created = await client.PostAsJsonAsync(
                    "/api/agent/conversations", new { Title = "restart chat" }, JsonOptions);
                created.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
                var conversation = await created.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions);
                conversationId = conversation!.Id;

                await StreamAsync(client, conversationId, "survive a host restart");

                var firstHistory = await HistoryAsync(client, conversationId);
                assistantReply = firstHistory.Items.Last().Content;
            }

            using (var second = new ApiFactory(databasePath, preserveDatabase: true))
            {
                using var client = second.CreateClient();
                var listed = await client.GetFromJsonAsync<ConversationsJson>("/api/agent/conversations", JsonOptions);
                listed!.Items.Single().Id.ShouldBe(conversationId);

                var history = await HistoryAsync(client, conversationId);
                history.Items.Length.ShouldBe(2);
                history.Items[0].Role.ShouldBe("user");
                history.Items[0].Content.ShouldBe("survive a host restart");
                history.Items[1].Role.ShouldBe("assistant");
                history.Items[1].Content.ShouldBe(assistantReply);
            }
        }
        finally
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                var file = databasePath + suffix;
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    private static async Task StreamAsync(HttpClient client, Guid conversationId, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = text }, options: JsonOptions)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("event: failed");
    }

    private static async Task<HistoryJson> HistoryAsync(HttpClient client, Guid conversationId)
    {
        var response = await client.GetAsync($"/api/agent/conversations/{conversationId}/messages");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HistoryJson>(JsonOptions))!;
    }

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string? Title);

    private sealed record ConversationsJson(ConversationJson[] Items);

    private sealed record MessageJson(Guid Id, string Role, string Content, DateTime CreatedAt);

    private sealed record HistoryJson(MessageJson[] Items);
}
