namespace Overmind.Host.WaveChat;

public sealed record WaveStreamOptions(TimeSpan HeartbeatInterval)
{
    public static readonly WaveStreamOptions Default = new(TimeSpan.FromSeconds(25));
}
