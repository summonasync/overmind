using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record EntryAckOutcome(EntryAck Ack, bool AckedNow);
