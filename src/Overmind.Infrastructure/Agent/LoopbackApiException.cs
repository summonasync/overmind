namespace Overmind.Infrastructure.Agent;

public sealed class LoopbackApiException : Exception
{
    public const int UnreachableStatusCode = 0;

    public LoopbackApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public bool IsUnreachable => StatusCode == UnreachableStatusCode;
}
