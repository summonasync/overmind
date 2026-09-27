---
name: dotnet-architect
description: Make architectural decisions, design service boundaries, and work with project documentation. Use when the user asks about system design, microservices boundaries, Aspire orchestration, Wolverine messaging topology (MassTransit legacy), or large-scale structural decisions spanning multiple files.
type: prompt
whenToUse: When making architectural decisions, designing service boundaries, working with documentation files, or designing Wolverine/Aspire messaging topology (MassTransit legacy).
---

# .NET Architecture & Documentation Skill

**Ownership:** consulted BY `feature-orchestrator` (Phase 1) — never runs planning gates, never creates spec/plan/task artifacts.

## Documentation-First Approach
Before any architectural decision or large structural work, read documentation in strict order:
1. **`CONTEXT.md`** — System design: architecture decisions, service roles, communication rules, AI Framework details, development phases, MCP server design (Russian)
2. **Overmind board** — features, statuses, execution state via `planning_list_features` (ceremony rules: `overmind-ceremony`)
3. **`STRUCTURE.md`** — Project structure, code style guidelines, architecture rules, AI agent guidelines, Do/Don't (English)
4. **`AGENTS.md`** — Tech stack, build commands, security, conventions for AI coding agents (English)

If these files are outdated or missing, propose updates to them BEFORE proposing code changes.

## Architecture Principles
- **Clean Architecture** is the default: Domain -> Application -> Infrastructure -> API
- **Vertical Slice** for feature grouping when appropriate
- **Microservices**: Start new ones with **Aspire** orchestration (AppHost + ServiceDefaults)
- **Messaging**: Use **Wolverine** as the abstraction over transports (RabbitMQ, Azure ServiceBus, Kafka). MassTransit is legacy-only: keep it where it already exists, never in new services
- **No direct service-to-service HTTP calls** between microservices. Use async messaging or a gateway.
- **No shared databases** between microservices. One DB per service boundary.
- **Strongly-typed IDs** (e.g., `record struct UserId(Guid Value)`) to prevent primitive obsession

## Code Style & Constraints (Architecture Level)
- **NO comments in code.** Architecture is expressed through folder structure and class names.
- **All project artifacts in English only.** Documentation can be mixed, but code is strictly EN.
- **Avoid God-Services.** If a service has >5 public methods, split by domain subdomain.
- **File separation:** API, DTOs, Services, Handlers, Validators, Configurations — all separate files.
- **No `async void` anywhere.** Use `async Task` or `IAsyncEnumerable<T>`.

## Aspire & Cloud-Native
- Use Aspire for local orchestration of microservices
- Configure health checks, OpenTelemetry, and resilience in `ServiceDefaults`
- Use Aspire components for Redis, PostgreSQL, RabbitMQ, etc.
- Keep AppHost project free of business logic — only orchestration and connection strings

## Messaging Topology Design (Wolverine)
- One queue/topic per consumer type, not per message type; let Wolverine conventional routing auto-queue handler messages and declare external transports explicitly in `app.UseWolverine(opts => ...)`
- Use conventional `Handle()` handler methods for command processing, `IMessageBus.InvokeAsync<T>()` for RPC-style calls
- Configure retry policies (exponential backoff via cooldown) at the app level with Wolverine error policies, not per handler
- Use the durable outbox for critical messages (Wolverine inbox/outbox with EF Core or Marten storage)
- Sagas: use Wolverine stateful sagas (`Saga` base class, correlated by saga `Id`, persisted via Marten/EF Core) for long-running processes, not manual orchestration

Legacy (MassTransit): existing services keep their current MassTransit topology as-is — `IConsumer<T>` handlers, `IRequestClient<T>` RPC, state machine sagas — but all new topology design is Wolverine.

## Frontend Architecture (if applicable)
- **Next.js BFF pattern:** `client` components -> Next.js API routes -> .NET backend
- Never allow direct browser-to-.NET calls from client components
- Use Next.js Server Actions or API routes as the proxy layer

## Communication
- **Speak Russian with the user.** All architectural discussions, questions, and explanations in Russian.
- When proposing architecture changes, provide:
  1. Updated documentation files (CONTEXT.md, board entries if needed)
  2. Folder structure diagram (ASCII or markdown)
  3. Service interaction diagram (if microservices)
  4. Messaging topology description (Wolverine; for legacy services, the existing MassTransit topology)
- Be concise. No generic theory — concrete decisions for this specific project.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — `feature-orchestrator` intake/spec/plan gates for the designed feature.
3. **Alternatives** — ADR entry in `TECHNICAL-SOLUTIONS.md` (user approves); `architect-review` to validate against existing structure.
Suggest, never auto-proceed. The user picks the next step.
