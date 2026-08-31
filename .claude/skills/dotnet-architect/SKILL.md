---
name: dotnet-architect
description: Make architectural decisions, plan features, design service boundaries, and work with project documentation. Use when the user asks about system design, microservices boundaries, Aspire orchestration, MassTransit topology, or when planning large features across multiple files.
type: prompt
whenToUse: When making architectural decisions, designing service boundaries, planning features, working with documentation files, or designing MassTransit/Aspire topology.
---

# .NET Architecture & Documentation Skill

## Documentation-First Approach
Before any architectural decision or large feature planning, read documentation in strict order:
1. **`context.md`** — System design: architecture decisions, service roles, communication rules, AI Framework details, development phases, MCP server design (Russian)
2. **`Backlog.md`** — Sprint backlog: tasks per phase with checkboxes, deferred items, execution order (Mixed EN headers, RU descriptions)
3. **`STRUCTURE.md`** — Project structure, code style guidelines, architecture rules, AI agent guidelines, Do/Don't (English)
4. **`AGENTS.md`** — Tech stack, build commands, security, conventions for AI coding agents (English)

If these files are outdated or missing, propose updates to them BEFORE proposing code changes.

## Architecture Principles
- **Clean Architecture** is the default: Domain -> Application -> Infrastructure -> API
- **Vertical Slice** for feature grouping when appropriate
- **Microservices**: Start new ones with **Aspire** orchestration (AppHost + ServiceDefaults)
- **Messaging**: Use **MassTransit** as the abstraction over transports (RabbitMQ, Azure ServiceBus, Kafka)
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

## MassTransit Topology Design
- One queue/topic per consumer type, not per message type
- Use `IConsumer<T>` for command processing, `IRequestClient<T>` for RPC-style calls
- Configure retry policies (exponential backoff) at the bus level, not per consumer
- Use outbox pattern for critical messages (EF Core integration)
- Sagas: use MassTransit State Machine Sagas for long-running processes, not manual orchestration

## Frontend Architecture (if applicable)
- **Next.js BFF pattern:** `client` components -> Next.js API routes -> .NET backend
- Never allow direct browser-to-.NET calls from client components
- Use Next.js Server Actions or API routes as the proxy layer

## Communication
- **Speak Russian with the user.** All architectural discussions, questions, and explanations in Russian.
- When proposing architecture changes, provide:
  1. Updated documentation files (context.md, Backlog.md if needed)
  2. Folder structure diagram (ASCII or markdown)
  3. Service interaction diagram (if microservices)
  4. MassTransit topology description (if messaging involved)
- Be concise. No generic theory — concrete decisions for this specific project.
