using System.Net;
using System.Text;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class FakeTelegramApi : HttpMessageHandler
{
    private const string EmptyResult = """{"ok":true,"result":[]}""";

    public sealed record Captured(string Method, string Path, string Query, string Body);

    private readonly object _lock = new();
    private readonly Queue<string> _pendingResponses = new();
    private readonly Dictionary<string, string> _pathFailures = [];
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

        lock (_lock)
        {
            Requests.Add(new Captured(
                request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, body));
        }

        RequestArrived.Release();
        await Task.Delay(20, cancellationToken);

        string json;
        lock (_lock)
        {
            var failure = _pathFailures.FirstOrDefault(pair =>
                request.RequestUri!.AbsolutePath.Contains(pair.Key, StringComparison.Ordinal));
            json = failure.Value
                ?? (_pendingResponses.Count > 0
                    ? _pendingResponses.Dequeue()
                    : Fallback(request.RequestUri!.AbsolutePath));
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
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

    public static string ReplyTextUpdate(long chatId, string text, int updateId, int repliedToMessageId) =>
        "{\"update_id\":" + updateId +
        ",\"message\":{\"message_id\":" + updateId +
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
