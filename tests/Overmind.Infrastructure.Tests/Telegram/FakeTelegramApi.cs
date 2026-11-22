using System.Net;
using System.Text;
using System.Text.Json;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class FakeTelegramApi : HttpMessageHandler
{
    private const string EmptyResult = """{"ok":true,"result":[]}""";

    public sealed record Captured(
        string Method,
        string Path,
        string Query,
        string Body,
        long? ResultMessageId);

    private readonly object _lock = new();
    private readonly Queue<string> _pendingResponses = new();
    private readonly Dictionary<string, string> _pathFailures = [];
    private readonly Queue<(int StatusCode, string Json)> _pendingFailures = new();
    private int _autoMessageId = 100;

    public List<Captured> Requests { get; } = [];
    public SemaphoreSlim RequestArrived { get; } = new(0);

    public void QueueUpdate(string updateJson) =>
        QueueResponse($$"""{"ok":true,"result":[{{updateJson}}]}""");

    public void FailPath(string pathSuffix, string json)
    {
        lock (_lock)
        {
            _pathFailures[pathSuffix] = json;
        }
    }

    // A real transport-level answer: non-200 statuses make the Telegram.Bot client throw its
    // typed exceptions (ApiRequestException with ErrorCode), unlike ok:false bodies wrapped in
    // HTTP 200 that v22 silently tolerates.
    public void QueueFailure(int statusCode, string json)
    {
        lock (_lock)
        {
            _pendingFailures.Enqueue((statusCode, json));
        }
    }

    public void QueueResponse(string json)
    {
        lock (_lock)
        {
            _pendingResponses.Enqueue(json);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        (int StatusCode, string Json) failure;
        string json;
        lock (_lock)
        {
            var pathFailure = _pathFailures.FirstOrDefault(pair =>
                request.RequestUri!.AbsolutePath.Contains(pair.Key, StringComparison.Ordinal));
            if (_pendingFailures.Count > 0)
            {
                failure = _pendingFailures.Dequeue();
                Requests.Add(new Captured(
                    request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, body,
                    MessageIdOf(failure.Json)));
                RequestArrived.Release();
                return new HttpResponseMessage((HttpStatusCode)failure.StatusCode)
                {
                    Content = new StringContent(failure.Json, Encoding.UTF8, "application/json"),
                };
            }

            json = pathFailure.Value
                ?? (_pendingResponses.Count > 0
                    ? _pendingResponses.Dequeue()
                    : Fallback(request.RequestUri!.AbsolutePath));
            Requests.Add(new Captured(
                request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, body,
                MessageIdOf(json)));
        }

        RequestArrived.Release();
        await Task.Delay(20, cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static long? MessageIdOf(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Object
                && result.TryGetProperty("message_id", out var messageId)
                    ? messageId.GetInt64()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A queued response is consumed by whatever request comes next, so tests queue one only where the
    // returned id matters; everything else falls back to a well-formed answer for its own method.
    private string Fallback(string path) =>
        path.Contains("/sendMessage", StringComparison.Ordinal)
            ? SentMessageResponse(Interlocked.Increment(ref _autoMessageId))
            : EmptyResult;

    public Captured Request(int index)
    {
        lock (_lock)
        {
            return Requests[index];
        }
    }

    public int RequestCount
    {
        get
        {
            lock (_lock)
            {
                return Requests.Count;
            }
        }
    }

    public int CountRequests(string pathPart)
    {
        lock (_lock)
        {
            return Requests.Count(request => request.Path.Contains(pathPart, StringComparison.Ordinal));
        }
    }

    public static string TextUpdate(long chatId, string text, int updateId, string? username = null) =>
        "{\"update_id\":" + updateId +
        ",\"message\":{\"message_id\":" + updateId +
        ",\"from\":{\"id\":" + chatId + ",\"is_bot\":false,\"first_name\":\"Owner\"" +
        (username is null ? string.Empty : ",\"username\":\"" + username + "\"") +
        "},\"chat\":{\"id\":" + chatId + ",\"type\":\"private\"}" +
        ",\"date\":1760000000,\"text\":\"" + text + "\"}}";

    public static string SentMessageResponse(long messageId = 5) =>
        "{\"ok\":true,\"result\":{\"message_id\":" + messageId +
        ",\"date\":1760000000,\"chat\":{\"id\":42,\"type\":\"private\"}}}";

    public static string ReplyTextUpdate(
        long chatId, string text, int updateId, int repliedToMessageId, int? messageId = null) =>
        "{\"update_id\":" + updateId +
        ",\"message\":{\"message_id\":" + (messageId ?? updateId) +
        ",\"from\":{\"id\":" + chatId + ",\"is_bot\":false,\"first_name\":\"Owner\"}" +
        ",\"chat\":{\"id\":" + chatId + ",\"type\":\"private\"}" +
        ",\"date\":1760000000" +
        ",\"text\":\"" + text + "\"" +
        ",\"reply_to_message\":{\"message_id\":" + repliedToMessageId +
        ",\"chat\":{\"id\":" + chatId + ",\"type\":\"private\"}" +
        ",\"date\":1760000000,\"text\":\"earlier bot message\"}}}";

    public static string TextlessUpdate(long chatId, int updateId) =>
        $$$"""
        {"update_id":{{{updateId}}},"message":{"message_id":{{{updateId}}},"from":{"id":{{{chatId}}},"is_bot":false,"first_name":"Owner"},"chat":{"id":{{{chatId}}},"type":"private"},"date":1760000000}}
        """
        .Trim();

    public static string CallbackQueryUpdate(
        long chatId,
        string data,
        int updateId,
        int messageId,
        string callbackQueryId,
        string? username = "owner",
        string firstName = "Owner") =>
        "{\"update_id\":" + updateId +
        ",\"callback_query\":{\"id\":\"" + callbackQueryId + "\"" +
        ",\"from\":{\"id\":" + chatId + ",\"is_bot\":false,\"first_name\":\"" + firstName + "\"" +
        (username is null ? string.Empty : ",\"username\":\"" + username + "\"") +
        "},\"message\":{\"message_id\":" + messageId +
        ",\"chat\":{\"id\":" + chatId + ",\"type\":\"private\"}" +
        ",\"date\":1760000000,\"text\":\"[approval] approve the plan\"}" +
        ",\"data\":\"" + data + "\"}}";
}
