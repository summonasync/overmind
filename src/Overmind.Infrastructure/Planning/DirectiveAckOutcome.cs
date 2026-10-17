using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record DirectiveAckOutcome(PlanningDirective Directive, bool AckedNow);
