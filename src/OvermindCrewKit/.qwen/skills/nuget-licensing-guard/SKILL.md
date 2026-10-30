---
name: nuget-licensing-guard
description: |
  Version-pinned licensing rules and approved alternatives for third-party .NET libraries.
  Use before adding, upgrading, or recommending any NuGet dependency — especially MediatR,
  MassTransit, AutoMapper, FluentAssertions, or any commercial/dual-licensed package.
type: prompt
whenToUse: |
  When the task involves adding a new NuGet package, upgrading an existing one,
  choosing between library alternatives, or reviewing .csproj / Directory.Packages.props changes.
---

# NuGet Licensing Guard

## Universal Rule
Before adding any third-party NuGet dependency, verify its license is OSI-approved
(MIT / Apache-2.0 / BSD). Flag dual-licensed or commercial libraries explicitly to the user
BEFORE adding — never silently.

## Known Traps (commercial re-licensing)

### Mediator (in-process CQRS)
- DO NOT use MediatR v13+ (commercial license).
- Use `Mediator` (martinothamar, source-generator based, MIT, Native AOT-friendly) as the default.
- If the project also uses Wolverine for messaging, use Wolverine's built-in mediator instead —
  do not add a second mediator library.
- MediatR ≤12.x is allowed only when maintaining legacy code that already depends on it — never in new code.
- For simple cases, plain handler interfaces injected directly are preferred over any mediator abstraction.

### Messaging / message brokers
- DO NOT use MassTransit v9+ (commercial license).
- Use Wolverine (MIT, open-core) as the default — it covers message bus, sagas, outbox,
  scheduling, and in-process mediation in one library.
- Use Rebus (MIT) when a dedicated, transport-focused service bus is preferred
  (RabbitMQ, Azure Service Bus, SQS).
- MassTransit v8 (Apache-2.0) is allowed only in existing codebases; its support ends after 2026.
  Reassess migration options (Wolverine, or OpenTransit if it has matured) when planning upgrades —
  do not assume any specific successor is ready; verify current state before recommending.
- For trivial pub/sub needs, raw clients (`RabbitMQ.Client`, `Azure.Messaging.ServiceBus`) are acceptable.

### Object mapping
- DO NOT use AutoMapper v15+ (commercial license).
- Prefer manual mapping (extension methods, record constructors) for simple cases — it is explicit,
  refactor-safe, and needs no dependency.
- Use Mapperly (source-generator based, MIT, compile-time errors, Native AOT-friendly) when
  mapping boilerplate is heavy.
- AutoMapper ≤14.x (MIT) is allowed only in existing codebases — never in new projects,
  and never upgrade it past v14.

### Assertions (testing)
- DO NOT use FluentAssertions v8+ (commercial license).
- Use Shouldly (BSD, actively maintained) as the default for new projects.
- FluentAssertions ≤7.x (Apache-2.0) is allowed only in existing test suites — never in new projects.

## Procedure
1. On any `.csproj` / `Directory.Packages.props` change — check every added/bumped package against this file.
2. For packages NOT listed here — check the license on nuget.org before approving.
3. If a commercial/dual license is detected — STOP, report to user with alternatives table, wait for decision.
4. When an allowed-legacy package is found in an existing codebase — note the ceiling version
   (e.g., "AutoMapper frozen at ≤14.x") so future upgrades don't cross the license boundary.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return the verdict to the calling coder skill (approve / pin / replace).
3. **Alternatives** — propose an approved alternative from the rules table; escalate to the user when no compliant option exists.
Suggest, never auto-proceed. The user picks the next step.
