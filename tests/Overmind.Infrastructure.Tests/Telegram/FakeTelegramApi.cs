using System.Net;
using System.Text;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class FakeTelegramApi : HttpMessageHandler
{
    public sealed record Captured(string Method, string Path, string Query, string Body);

    private readonly object _lock = new();
    private readonly Queue<string> _pendingResponses = new();

    public List<Captured> Requests { get; } = [];
    public SemaphoreSlim RequestArrived { get; } = new(0);

    public void QueueUpdate(string updateJson) =>
        QueueResponse($$"""{"ok":true,"result":[{{updateJson}}]}""");

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
            json = _pendingResponses.Count > 0
                ? _pendingResponses.Dequeue()
                : """{"ok":true,"result":[]}""";
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

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

    public static string TextUpdate(long chatId, string text, int updateId) =>
        $$$"""
        {"update_id":{{{updateId}}},"message":{"message_id":{{{updateId}}},"from":{"id":{{{chatId}}},"is_bot":false,"first_name":"Owner"},"chat":{"id":{{{chatId}}},"type":"private"},"date":1760000000,"text":"{{{text}}}"}}
        """
        .Trim();

    public static string SentMessageResponse() =>
        """{"ok":true,"result":{"message_id":5,"date":1760000000,"chat":{"id":42,"type":"private"}}}""";

    public static string TextlessUpdate(long chatId, int updateId) =>
        $$$"""
        {"update_id":{{{updateId}}},"message":{"message_id":{{{updateId}}},"from":{"id":{{{chatId}}},"is_bot":false,"first_name":"Owner"},"chat":{"id":{{{chatId}}},"type":"private"},"date":1760000000}}
        """
        .Trim();
}
