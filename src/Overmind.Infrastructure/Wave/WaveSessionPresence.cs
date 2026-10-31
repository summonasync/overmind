namespace Overmind.Infrastructure.Wave;

public sealed record WaveSessionPresence(
    string Alias,
    string Vendor,
    string Role,
    DateTime LastSeenAt);
