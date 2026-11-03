using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Overmind.Api.Tests;

public static class SseTestClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> PostStreamAsync(
        HttpClient client, Guid conversationId, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = text }, options: JsonOptions)
        };
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    public static async Task<List<SseFrame>> ReadAllFramesAsync(HttpContent content)
    {
        var frames = new List<SseFrame>();
        var buffer = new StringBuilder(await content.ReadAsStringAsync());
        CollectFrames(frames, buffer);
        return frames;
    }

    private static void CollectFrames(List<SseFrame> frames, StringBuilder buffer)
    {
        int boundary;
        while ((boundary = buffer.ToString().IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
        {
            var raw = buffer.ToString(0, boundary + 2);
            buffer.Remove(0, boundary + 2);
            var frame = ParseFrame(raw);
            if (frame is not null)
            {
                frames.Add(frame);
            }
        }
    }

    private static SseFrame? ParseFrame(string raw)
    {
        string? name = null;
        var data = new StringBuilder();
        foreach (var line in raw.Split('\n'))
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                name = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data.Append(line["data: ".Length..]);
            }
        }

        return name is null ? null : new SseFrame(name, data.ToString());
    }
}

public sealed record SseFrame(string Event, string DataText);
