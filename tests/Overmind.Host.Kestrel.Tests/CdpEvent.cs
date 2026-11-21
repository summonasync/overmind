using System.Text.Json;

namespace Overmind.Host.Kestrel.Tests;

public sealed record CdpEvent(long Sequence, string Method, JsonElement Payload);
