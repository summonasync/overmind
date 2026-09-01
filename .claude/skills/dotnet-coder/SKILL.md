---
name: dotnet-coder
description: |
  Write, edit, review, or refactor C# code following senior .NET developer standards.
  Use for creating classes, services, handlers, DTOs, endpoints, tests, or any code modification.
  Strictly follows no-comments, English-only, Clean Architecture, MassTransit conventions,
  and mandatory decomposition.
type: prompt
whenToUse: |
  When writing, editing, reviewing, or refactoring C# code, creating classes, services,
  controllers, minimal API endpoints, DTOs, or tests in a .NET project.
---

# .NET Code Writing & Refactoring Skill

## Context & Documentation Order
Before writing or editing ANY code, read project documentation in this strict order:
1. `CONTEXT.md` — Architecture decisions, service roles, AI Framework details (Russian)
2. `BACKLOG.md` — Current sprint tasks and execution order (Mixed EN/RU)
3. `STRUCTURE.md` — Code style, architecture rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, build commands, conventions (English)
5. `~/.claude/skills/dotnet-best-practices/SKILL.md` — codified BP-IDs; enforceable during implementation. When executing a `BACKLOG/` task file, load this together with the task file.

## Tech Stack
- **Runtime**: .NET 9/10+ (latest LTS)
- **Language**: C# 13+ (primary constructors, collection expressions, inline arrays, pattern matching)
- **Architecture**: Clean Architecture. Domain -> Application -> Infrastructure -> API
- **CQRS**: MediatR for commands/queries. One handler per file.
- **Messaging**: MassTransit for async messaging (never raw RabbitMQ/ServiceBus clients)
- **API**: Minimal APIs or Carter. No heavy controllers.
- **ORM**: EF Core with strongly-typed IDs where applicable
- **Validation**: FluentValidation
- **Logging**: Serilog with structured logging. No string interpolation in log templates.
- **Testing**: xUnit + FluentAssertions + NSubstitute

## Code Style (ABSOLUTE RULES — ZERO TOLERANCE)
- **NO comments in code.** Self-documenting code via naming only. If you need a comment, your naming is wrong.
- **All strings/messages/errors/logs/variables in English only.** No Russian or other languages anywhere in code.
- **NEVER write everything in one file. ALWAYS decompose into minimally possible, reusable, single-responsibility units.** One file = one public type (class, record, interface, enum). Period.
- **Avoid God-Services / God-Classes.** Maximum 3-5 public methods per service. If more — split by responsibility into separate services.
- **Split files MANDATORY:** Endpoints, DTOs, Services/Handlers, Validators, Configurations, Mappings — all in separate files. No exceptions.
- **Clean Architecture on backend.** Zero business logic in API layer. Controllers/Endpoints are thin dispatchers only.
- **No `region`, no Hungarian notation, no `async void`.**
- **Use `CancellationToken` in ALL async methods.** No async method without it.
- **Prefer `IReadOnlyList<T>` or `IEnumerable<T>` in public APIs over `List<T>`.**
- **Use `Result<T>` pattern or exceptions consistently** — don't mix both randomly.
- **No primitive obsession.** Use strongly-typed IDs: `record struct UserId(Guid Value)`.

## Git & Commit Policy (STRICT)
- **NEVER run `git commit` automatically.** Do NOT commit any changes unless the user explicitly asks: "закоммить", "сделай коммит", "commit".
- **NEVER run `git push` automatically.** Push ONLY after explicit user request.
- **You MAY stage files** (`git add`) to show what changed, but do NOT commit.
- **After completing work, report the list of changed files** and suggest: "Изменены файлы: [список]. Хотите сделать коммит?"
- If the user says nothing about commit — leave changes unstaged/uncommitted.

## Architecture Rules
- **Domain layer**: Pure logic, no dependencies. Entities, Value Objects, Domain Events.
- **Application layer**: Use Cases, CQRS handlers, interfaces. No infrastructure imports.
- **Infrastructure layer**: EF, MassTransit, external APIs. Implements Application interfaces.
- **API layer**: Minimal endpoints, DTOs, middleware. No business logic.
- **No direct HTTP calls between microservices.** Use MassTransit messaging.
- **No shared databases** between service boundaries. One DB per bounded context.

## Writing Code
When generating or editing C# code:
1. **Decompose first.** Before writing implementation, define the minimal units needed. Never dump logic into existing "convenient" classes.
2. Use modern C# 13 features where appropriate (primary constructors for services, collection expressions `[]`, pattern matching)
3. Follow Clean Architecture layers strictly. If you import `Microsoft.EntityFrameworkCore` in Application layer — you are wrong.
4. One public class per file (except tiny nested DTOs if co-located, but prefer separate files).
5. Use records for DTOs and immutable value objects. Use `required init` for DTOs instead of nullable properties.
6. For MediatR: separate `IRequest<T>`, `IRequestHandler<T,TResult>`, and validator in different files.
7. For MassTransit: consumers must be in Application layer, configuration in Infrastructure.
8. For EF Core: configurations in `IEntityTypeConfiguration<T>`, separate files in `Configurations/`.
9. **File naming matches type name.** `CreateOrderCommand.cs` contains `CreateOrderCommand`. Not `Commands.cs` with 10 commands.

## Testing Rules
- One test class per production class. `OrderServiceTests` tests `OrderService`.
- Naming: `MethodName_Scenario_ExpectedResult`
- Use `Theory` for parameterized tests
- Mock external dependencies with NSubstitute
- Assert with FluentAssertions (e.g., `result.Should().BeEquivalentTo(expected)`)
- No business logic in test setup — only arrange data.

## Post-Task Review Protocol (MANDATORY)
After completing a COMPLEX task, the agent MUST trigger review before declaring success.

**Complex task criteria** (ANY of):
- Touches >3 files
- Spans multiple Clean Architecture layers (e.g., Domain + API)
- Introduces new API endpoints or MediatR handlers
- Modifies EF Core entities or migrations
- Changes MassTransit messages, consumers, or saga state
- Creates new project/service

**Review execution:**
1. Print: `--- POST-TASK REVIEW REQUIRED ---`
2. Apply `dotnet-code-reviewer` checklist to all changed files
3. Output review results grouped by severity:
   - `BLOCKER` — must fix immediately
   - `WARNING` — should fix
   - `SUGGESTION` — nice to have
4. If BLOCKERS exist → fix them immediately, then re-run review. Do not finish task with active BLOCKERS.
5. If only WARNINGS/SUGGESTIONS → present to user and ask if they want fixes now or in follow-up.

**If task is SIMPLE** (<3 files, single layer, no contracts changed) → review is optional but recommended.

## Verification After Changes
- **Always run `dotnet build` after significant code changes**
- **Run `dotnet test` if test files were modified**
- **Run `dotnet format` before finishing**
- **Ask before adding new NuGet packages** to production code

## Communication
- **Speak Russian with the user.** Code and project artifacts are English-only, but all dialogue is in Russian.
- Provide diffs or full file content when suggesting changes.
- Be concise. No boilerplate explanations. No "let me explain what I did" — show the code.
- When proposing file structure, use ASCII tree.
- **After finishing:** report changed files and ask: "Хотите сделать коммит?" Do NOT commit silently.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — `dotnet-code-reviewer` (mandatory Phase 3 for Complex tasks).
3. **Alternatives** — `nuget-licensing-guard` before any pending NuGet change; `task-orchestrator` for the next task in the feature folder.
Suggest, never auto-proceed. The user picks the next step.
