---
name: dotnet-code-reviewer
description: |
  Review C# / .NET code for Clean Architecture compliance, Wolverine messaging conventions,
  code style, security, and senior backend standards. Use when the user asks for PR review,
  code audit, or quality check of .NET code.
type: prompt
whenToUse: |
  When reviewing C# code, checking Clean Architecture layers, Wolverine messaging usage,
  code style compliance, security vulnerabilities, or backend PR review.
---

# .NET Code Review Skill

## Context & Documentation Order
Before reviewing code, read project documentation in this strict order:
1. `CONTEXT.md` — Architecture decisions, service boundaries, messaging topology (Russian)
2. Overmind board — current feature/task context via `planning_list_features` (see `overmind-ceremony`)
3. `STRUCTURE.md` — Code style, architecture rules, Do/Don't (English)
4. `AGENTS.md` — Tech stack, build commands, conventions (English)

## Review Checklist (MUST CHECK)

**BP-IDs are the primary checklist.** Load `~/.codex/skills/dotnet-best-practices/SKILL.md` and check every applicable rule; cite violations as `BP-NN (severity)` in the review output. The checklist below complements, not replaces, BP-IDs.

### Architecture & Layers
- [ ] **Clean Architecture respected.** Domain has no infra dependencies. Application has no API dependencies.
- [ ] **No business logic in API layer.** Controllers/Minimal API endpoints are thin.
- [ ] **No direct HTTP between microservices.** Messaging (Wolverine) used for service communication.
- [ ] **No shared databases.** One DB per service boundary.
- [ ] **EF Core configurations** in separate `IEntityTypeConfiguration<T>` classes.

### Code Style
- [ ] **NO comments in code.** Self-documenting via naming.
- [ ] **English only.** No Russian in exceptions, logs, enums, or UI messages.
- [ ] **No God-Services.** >5 public methods per service → split by responsibility.
- [ ] **Split files.** Endpoints, DTOs, Handlers, Validators, Services — all separate.
- [ ] **No `region`, no Hungarian notation, no `async void`.**
- [ ] **CancellationToken** in ALL async methods.

### .NET Conventions
- [ ] **Primary constructors** used for services (C# 14+).
- [ ] **Records** for DTOs and immutable value objects.
- [ ] **Strongly-typed IDs** (`record struct UserId(Guid Value)`) where applicable.
- [ ] **Result<T> pattern OR exceptions** — not mixed randomly.
- [ ] **No `async void`.** Use `async Task` or `IAsyncEnumerable<T>`.
- [ ] **Prefer `IReadOnlyList<T>`** over `List<T>` in public APIs.

### Messaging (Wolverine)
- [ ] **Wolverine used for async messaging.** No raw RabbitMQ/Azure ServiceBus clients.
- [ ] **No MassTransit in new code** — BLOCKER. Allowed only in legacy codebases; note the legacy status in the review, never bless it into new modules.
- [ ] **Handlers live in Application layer**, bus configuration in Infrastructure.
- [ ] **Retry policies** at bus level, not per handler.
- [ ] **Durable/outbox messaging** for critical messages (if applicable).
- [ ] **Sagas** use Wolverine's durable sagas, not manual orchestration.

### Validation & Logging
- [ ] **FluentValidation** for all inputs.
- [ ] **Serilog structured logging.** No string interpolation in log templates (`Log.Information("User {UserId}", id)`).
- [ ] **No secrets in logs.**

### Third-Party Library Stack
- [ ] **Mediator (martinothamar) for CQRS, Wolverine for messaging, Mapperly or manual mapping, Shouldly for asserts.** Version-pinned rules: `nuget-licensing-guard`.
- [ ] **MediatR, MassTransit, AutoMapper, or FluentAssertions in NEW code = BLOCKER.** Only acceptable inside legacy codebases — flag it with an explicit legacy note in the review output, never propagate these libraries into new modules or migrations of existing ones.

### Testing
- [ ] **Tests exist** for new business logic.
- [ ] **Naming:** `MethodName_Scenario_ExpectedResult`.
- [ ] **NSubstitute** for mocks, **Shouldly** for asserts. **FluentAssertions in new code = BLOCKER** (legacy codebases only).
- [ ] **Theory** used for parameterized tests.

### Security Review (CRITICAL — check on EVERY PR touching auth, API, config, or data)
- [ ] **No SQL Injection.** `FromSqlRaw` with string interpolation = BLOCKER. Use `FromSqlInterpolated` or parameterized queries.
- [ ] **No hardcoded secrets.** API keys, connection strings, passwords must be in `IConfiguration` / Azure Key Vault / environment variables. Never in source code.
- [ ] **AuthZ on all endpoints.** Every Minimal API endpoint must have `.RequireAuthorization()` or explicit `[AllowAnonymous]` with justification.
- [ ] **No insecure deserialization.** `BinaryFormatter` = BLOCKER. `Newtonsoft.Json` with `TypeNameHandling.All` = BLOCKER. Use `System.Text.Json` with strict options.
- [ ] **CORS is restrictive.** `AllowAnyOrigin()` + `AllowCredentials()` = BLOCKER (broken combo). Explicit origins only.
- [ ] **Sensitive data in bus messages encrypted or tokenized.** PII must not travel in plain text across the bus.
- [ ] **No mass assignment vulnerabilities.** DTOs must not expose ID/created-by fields that user can override.
- [ ] **Input validation on boundary.** All public API inputs validated via FluentValidation before hitting business logic.
- [ ] **No exception details leaked to client.** Generic error messages in API responses; details only in logs.
- [ ] **EF Core: no client-side evaluation** that bypasses SQL generation (`AsEnumerable()` before `Where()` on large tables).

### Frontend Security (if Next.js in same PR)
- [ ] **No `dangerouslySetInnerHTML`** with unsanitized user input.
- [ ] **Server Actions validate origin.** CSRF protection via Next.js built-in mechanisms.
- [ ] **No secrets in client-side code.** `NEXT_PUBLIC_` vars are public-only; no backend keys in frontend bundle.

## Review Output Format
For each issue found, provide:
1. **Severity:** `BLOCKER` (architecture violation, security vulnerability, style breach) / `WARNING` (convention, performance) / `SUGGESTION` (refactoring)
2. **Location:** File and line (or method)
3. **Problem:** What violates project rules
4. **Fix:** Concrete code snippet or diff

**Security BLOCKER special rule:** If a security BLOCKER is found, add `[SECURITY]` tag to the issue and recommend immediate human review before auto-fix. Do NOT auto-suggest "quick fixes" for crypto, auth, or injection vulnerabilities.

## Communication
- **Speak Russian with the user.** Review summary and comments in Russian.
- Be direct. Group by severity: Security BLOCKERS first, then regular Blockers, then Warnings, then Suggestions.
- No generic praise. Focus on actionable fixes.

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — BLOCKERs → fix loop via `dotnet-coder`; clean review → task done.
3. **Alternatives** — `architect-review` (Light) when violations are structural; `nuget-licensing-guard` when dependencies changed.
Suggest, never auto-proceed. The user picks the next step.
