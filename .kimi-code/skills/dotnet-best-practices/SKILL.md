---
name: dotnet-best-practices
description: |
  Codified .NET engineering rules (BP-IDs) consumed by dotnet-coder during implementation
  and by dotnet-code-reviewer as the review checklist. Single source of enforceable code-quality rules.
type: prompt
whenToUse: |
  During any .NET implementation (coder loads it with the task file) and any .NET code review
  (reviewer cites violations by BP-ID).
---

# .NET Best Practices — Codified Rules

Each rule has a stable BP-ID. Reviewer cites violations as `BP-NN (severity)`.
Severity: **[B]** BLOCKER, **[W]** WARNING, **[S]** SUGGESTION.

## Async
- **BP-01 [B]** No `async void` except event handlers. No `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` — deadlock risk.
- **BP-02 [W]** `CancellationToken` parameter on every async method that does I/O; pass it through the whole call chain, never swallow it.
- **BP-03 [W]** No `async` wrapper around a single inner call without adding value — return the `Task` directly.
- **BP-04 [S]** `ConfigureAwait(false)` in library code only; not needed in ASP.NET Core app code.

## Dependency Injection
- **BP-10 [B]** Microsoft.Extensions.DependencyInjection only — no external containers, no service-locator patterns.
- **BP-11 [B]** No `new` for services registered in DI; no injecting `IServiceProvider` to resolve manually (except factory edge cases, justified in code-adjacent ADR).
- **BP-12 [W]** Correct lifetimes: DbContext and Unit-of-Work = Scoped; stateless/expensive-to-build = Singleton; per-operation state = Transient. Captive dependencies (singleton holding scoped) = BLOCKER.
- **BP-13 [W]** Constructor injection only; primary constructors allowed. No property injection.

## EF Core & Data
- **BP-20 [B]** No N+1: use `Include`/`ThenInclude` or projections. A loop issuing queries = automatic BLOCKER.
- **BP-21 [B]** Read paths use `AsNoTracking()` unless the entity is modified in the same scope.
- **BP-22 [W]** Migrations in a separate assembly for modular monoliths; never edit applied migrations — add a new one.
- **BP-23 [W]** No raw SQL without parameterization; `FromSqlRaw` with interpolated values = BLOCKER (SQL injection).
- **BP-24 [S]** Prefer compiled queries only on proven hot paths — measure first, don't gold-plate.

## API Surface
- **BP-30 [W]** Minimal APIs or Carter modules; heavy controllers are legacy-only. Thin handlers: HTTP in/out, logic in Application layer.
- **BP-31 [B]** DTOs at the boundary — domain entities never leave the API layer. Manual mapping or Mapperly (see `nuget-licensing-guard`).
- **BP-32 [W]** Consistent error shape: ProblemDetails for all error responses; no raw exception text leaking to clients.
- **BP-33 [W]** Pagination on every collection endpoint; no unbounded `ToListAsync()` exposed via API.

## Validation & Errors
- **BP-40 [W]** FluentValidation at the Application boundary; validators stay out of domain entities.
- **BP-41 [W]** Domain errors as results/explicit error types where the flow expects them; exceptions for truly exceptional paths. No empty `catch {}` — ever [B if found].
- **BP-42 [S]** Guard clauses at method entry for null/invalid args (`ArgumentNullException.ThrowIfNull`); don't nest validation deep.

## Logging & Observability
- **BP-50 [W]** Serilog structured logging with message templates (`Log.Information("Order {OrderId} processed", id)`) — no string interpolation in templates.
- **BP-51 [B]** No PII, credentials, tokens, or connection strings in logs.
- **BP-52 [S]** Log at the right level: Error = actionable failure, Warning = recovered anomaly, Information = business-meaningful event. No Debug-spam in committed code.

## Testing
- **BP-60 [W]** xUnit + Shouldly + NSubstitute (see AGENTS.md stack). Naming: `Method_Scenario_Expectation` or project convention — but consistent within the test project.
- **BP-61 [W]** TDD per `overmind-ceremony` TDD scope: domain/business logic gets red-green-refactor; migrations/DI/config/DTOs are exempt.
- **BP-62 [W]** Tests are independent: no shared mutable state, no order dependence, no hitting real DB/network in unit tests.
- **BP-63 [S]** One logical assertion per test; multiple Shouldly checks on one outcome are fine, multiple scenarios are not.

## Performance
- **BP-70 [W]** Hot paths: no LINQ allocations in tight loops without measurement; `Span<T>`/`ArrayPool` only where profiling justifies.
- **BP-71 [W]** `IAsyncEnumerable` for streaming large result sets instead of materializing lists.
- **BP-72 [S]** Response caching/output cache where the endpoint is read-heavy and invalidation is trivial.

## Security
- **BP-80 [B]** No secrets in code, config committed to git, or logs. User Secrets / env vars / vault only.
- **BP-81 [B]** Authorization explicit on every endpoint — no anonymous-by-default on new endpoints.
- **BP-82 [B]** All input validated; all SQL parameterized (BP-23); no `string.Format` into commands, URLs to internal services, or file paths from user input.

## Architecture & Style
- **BP-90 [B]** Clean Architecture dependency direction: Domain ← Application ← Infrastructure/API. An inward-to-outward reference = BLOCKER.
- **BP-91 [W]** No God-Services: a service exceeding ~300 lines or >5 dependencies is a split candidate — reviewer flags, human decides.
- **BP-92 [W]** Mediator (martinothamar) for CQRS dispatch; Wolverine for messaging — version-pinned rules in `nuget-licensing-guard`.
- **BP-93 [S]** API, DTO, Service in separate files. No `#region`. No comments restating code. Match surrounding human style (AI Coding Traces rules apply).
- **BP-94 [B] No unexplained magic.** Payment, integration, and infrastructure code must be explainable by the responsible developer. "It works but nobody understands how" = BLOCKER — undocumented side effects (temporary objects, invisible state, deleted-but-existing records) are exactly how AI-generated integrations blow up in production. If the reviewer can't trace what the code does against external systems, it doesn't pass — document the mechanism in an ADR or simplify until traceable.

## Usage
- **Coder:** load with the task file; rules are constraints, not aspirations.
- **Reviewer:** checklist = this file + task `<acceptance-criteria>` + spec-compliance. Cite as `BP-NN [B/W/S] — file:line — what's wrong — fix diff`.
- New recurring rule discovered during work → propose adding it here (reflect loop), don't patch it silently into one review.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — return control to the calling skill (`dotnet-coder` / `dotnet-code-reviewer`).
3. **Alternatives** — propose a new BP-ID to the user when a recurring rule is discovered; stop — reference skill, never runs standalone.
Suggest, never auto-proceed. The user picks the next step.
