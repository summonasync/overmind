---
name: audit-orchestrator
description: |
  Conduct comprehensive system audits by analyzing project documentation,
  identifying required audit types, and generating structured reports in docs/audit/{date}/.
  Use for periodic reviews, sprint retrospectives, phase-end validation, or security/compliance checks.
type: prompt
whenToUse: |
  When the user asks for a code audit, sprint review, phase-end review, security audit,
  architecture audit, tech debt assessment, or comprehensive system health check.
---

# Audit Orchestrator Skill

## Purpose
This skill performs **system-wide audits** (not task-based reviews). It analyzes the entire project state against documentation, identifies gaps, and produces a structured audit report. Unlike `task-orchestrator` which reviews changes after a specific task, `audit-orchestrator` evaluates the whole system or a specific domain area on demand.

## Start-of-Audit Protocol (MANDATORY)

### Step 1: Read Project Documentation (strict order)
1. **`AGENTS.md`** — Tech stack, conventions, documentation order
2. **`context.md`** — System design, current phase, architecture decisions, service boundaries (Russian)
3. **`Backlog.md`** — Sprint state: completed tasks, in-progress items, blocked tasks, deferred tech debt (Mixed EN/RU)
4. **`STRUCTURE.md`** — Code style, architecture rules, Do/Don't (English)
5. **`ARCHITECTURE_DECISIONS.md`** (if exists) — Historical ADRs

### Step 2: Determine Audit Scope
Based on documentation analysis, determine which audit types are relevant:

**Always run (Base Audit):**
- Code Quality Audit — check against STRUCTURE.md and AGENTS.md rules

**Conditional audits (triggered by backlog/context):**

| Trigger in Backlog/Context | Audit Type | What to Check |
|---------------------------|------------|---------------|
| Auth/Identity tasks, new endpoints, secret handling | **Security Audit** | AuthZ, secrets, injection, deserialization, CORS |
| Refactoring tasks, new microservices, phase changes | **Architecture Audit** | Clean Architecture layers, coupling, service boundaries |
| EF migrations, performance tasks, API optimization | **Performance Audit** | N+1 queries, async misuse, caching, MassTransit topology |
| Deferred items, TODO/FIXME in code, legacy mentions | **Tech Debt Audit** | Deprecated APIs, uncompleted tasks, migration blockers |
| MassTransit topology changes, saga additions | **Messaging Audit** | Queue design, retry policies, outbox, saga state machines |
| Frontend changes, Next.js upgrades, BFF modifications | **Frontend Audit** | RSC compliance, BFF pattern, component decomposition |
| MAUI project present, mobile/desktop tasks | **MAUI Audit** | MVVM compliance, code-behind purity, platform code isolation, main-thread violations |
| Doc tasks, doc drift suspected, onboarding complaints | **Documentation Audit** | Docs vs code accuracy, stale commands/paths, contradiction with AGENTS.md/STRUCTURE.md |
| Incoming vendor repo, AI-usage suspicion, pre/post-obfuscation check | **AI Trace Audit** | Delegate to `ai-code-detector` (K3): where AI code lives, volume, signatures |

**If no specific triggers** — run Base + Architecture + Security as default.

### Step 3: Scan Project Structure
- List all `.csproj` files and their dependencies (check for circular refs)
- List all projects in solution and their layer (Domain/Application/Infrastructure/API)
- Identify files changed since last audit (if previous audit report exists in `docs/audit/`)
- Check for files without corresponding tests

---

## Audit Execution

### Base Audit — Code Quality
Check the entire codebase against project rules:
- [ ] **No comments in code.** Architecture expressed via naming/structure.
- [ ] **English-only** strings, exceptions, logs, enums.
- [ ] **One file = one public type.** No God-Services (>5 public methods).
- [ ] **Clean Architecture respected.** No EF imports in Application layer. No business logic in API.
- [ ] **Split files:** Endpoints, DTOs, Handlers, Validators, Services — all separate.
- [ ] **CancellationToken** in all async methods.
- [ ] **No `async void`.**
- [ ] **Test coverage:** Every public service/handler has a test class.

### Security Audit (if triggered)
- [ ] **SQL Injection:** No `FromSqlRaw` with interpolation. Use `FromSqlInterpolated`.
- [ ] **Hardcoded secrets:** Check `appsettings*.json`, `.cs`, `.env` for API keys, connection strings, passwords.
- [ ] **AuthZ:** Every endpoint has `.RequireAuthorization()` or explicit `[AllowAnonymous]` with justification.
- [ ] **Insecure deserialization:** No `BinaryFormatter`, no `TypeNameHandling.All` in Json.NET.
- [ ] **CORS:** Not `AllowAnyOrigin() + AllowCredentials()`.
- [ ] **MassTransit messages:** PII encrypted/tokenized, not plain text.
- [ ] **Mass assignment:** DTOs don't expose internal IDs that users can override.
- [ ] **Input validation:** FluentValidation on all public API inputs.
- [ ] **Exception leakage:** Generic messages to client, details only in logs.
- [ ] **EF client eval:** No `AsEnumerable()` before `Where()` on large tables.
- [ ] **Frontend:** No `dangerouslySetInnerHTML` with unsanitized input. No secrets in client bundle.

### Architecture Audit (if triggered)
- [ ] **Layer integrity:** Domain → Application → Infrastructure → API. No bypasses.
- [ ] **No shared databases** between microservices.
- [ ] **No direct HTTP** between services. MassTransit only.
- [ ] **Service boundaries:** Each service has <5 public methods or is split by subdomain.
- [ ] **Strongly-typed IDs:** Used for all domain entities (no primitive obsession).
- [ ] **Aspire orchestration:** AppHost has no business logic. ServiceDefaults have health checks, OTel, resilience.
- [ ] **MassTransit topology:** One queue per consumer type. Retry at bus level. Outbox for critical messages.
- [ ] **Frontend BFF:** No direct browser-to-.NET calls. All through Next.js backend.

### Performance Audit (if triggered)
- [ ] **N+1 queries:** Check EF queries for `Include()` usage, projection with `Select()`.
- [ ] **Async/await:** No sync-over-async (`.Result`, `.Wait()`). No `async void`.
- [ ] **Caching:** Redis/IMemoryCache used for hot data. Cache invalidation strategy exists.
- [ ] **MassTransit:** Sagas use state machine, not manual orchestration. Consumers are idempotent.
- [ ] **API:** Streaming endpoints use `IAsyncEnumerable<T>`. No large in-memory collections.
- [ ] **Frontend:** No `useEffect` for data fetching in RSC. Images use `next/image` with sizes.

### Tech Debt Audit (if triggered)
- [ ] **TODO/FIXME count** in codebase. Trend vs previous audit.
- [ ] **Deprecated APIs:** .NET 8+ features not used where applicable (e.g., still using old `System.Text.Json` patterns).
- [ ] **Migration backlog:** Pending EF migrations not applied. Migration naming convention followed.
- [ ] **Package versions:** Outdated NuGet/npm packages with known vulnerabilities.
- [ ] **Documentation drift:** `context.md` / `Backlog.md` / `STRUCTURE.md` out of sync with actual code.

### Messaging Audit (if triggered)
- [ ] **MassTransit transport:** RabbitMQ/ASB configured with retry, dead-letter, and outbox.
- [ ] **Message contracts:** Versioned, immutable, no breaking changes without versioning.
- [ ] **Saga state:** Sagas use `MassTransitStateMachine`, not custom orchestration.
- [ ] **Consumer idempotency:** All consumers handle duplicate messages gracefully.

### MAUI Audit (if triggered)
Run against `maui-coder` skill rules:
- [ ] **MVVM:** No business logic in code-behind. View models use CommunityToolkit.Mvvm generators.
- [ ] **Compiled bindings:** `x:DataType` set on pages/views.
- [ ] **Platform isolation:** Platform code in `Platforms/<Platform>/`, no scattered `#if` blocks.
- [ ] **Threading:** No `Task.Run` for UI work; main-thread marshalling where needed.
- [ ] **DI:** Pages/view models registered in `MauiProgram` via feature extension methods.

### Documentation Audit (if triggered)
Run against `documentation-writer` skill rules:
- [ ] **Accuracy:** Commands/paths in docs exist and run. Spot-check at least the quickstart.
- [ ] **Consistency:** No contradictions between `context.md`, `STRUCTURE.md`, `TECHNICAL-SOLUTIONS.md`, `AGENTS.md`.
- [ ] **Language:** Each doc follows the language convention from the `AGENTS.md` documentation table.
- [ ] **Drift:** Docs describing features changed since last audit were updated in the same commits.

---

## Report Generation

After completing all audit types, generate a structured report:

**Path:** `docs/audit/YYYY-MM-DD/audit-report.md`

**Structure:**
```markdown
# Audit Report — YYYY-MM-DD

## Executive Summary
- Audit scope: [Base + Security + Architecture + ...]
- Project phase: [from context.md]
- Overall health: [Green / Yellow / Red]
- Critical issues: [N] | Warnings: [N] | Suggestions: [N]

## Findings

### [Category] — Severity: [BLOCKER / WARNING / SUGGESTION]
- **Location:** `File.cs:line` or `app/feature/page.tsx:line`
- **Problem:** [Description]
- **Impact:** [Why this matters]
- **Recommendation:** [Concrete fix or architectural change]
- **Backlog link:** [If applicable, link to Backlog.md task #N]

## Action Items
| Priority | Task | Owner | Due | Linked Backlog Item |
|----------|------|-------|-----|---------------------|
| P0 | Fix SQL injection in X | TBD | +1d | #142 |
| P1 | Split OrderService | TBD | +3d | #155 |

## Trends (if previous audit exists)
- Tech debt delta: [+5 / -3 TODOs]
- Security posture: [Improved / Degraded / Stable]
- Test coverage: [X% → Y%]

## Appendix
- Full file list scanned
- Audit type triggers detected
- Documentation consistency check results
```

**Additional files (if findings are extensive):**
- `docs/audit/YYYY-MM-DD/security.md` — detailed security findings
- `docs/audit/YYYY-MM-DD/architecture.md` — architectural analysis with diagrams
- `docs/audit/YYYY-MM-DD/performance.md` — query analysis, profiling notes

---

## Post-Audit Actions
- If **BLOCKER found** → recommend immediate `task-orchestrator` session to fix
- If **architecture drift detected** → recommend updating `context.md` and `ARCHITECTURE_DECISIONS.md`
- If **tech debt > threshold** → recommend dedicated sprint for debt reduction
- If **no critical issues** → update `Backlog.md` with "Audit passed" status

---

## Communication
- **Speak Russian.**
- At start: "Аудит запущен. Анализирую документацию и определяю scope..."
- During audit: "Обнаружены триггеры: [Security, Architecture]. Запускаю соответствующие проверки..."
- At finish: "Аудит завершён. Отчёт: `docs/audit/YYYY-MM-DD/audit-report.md`. Критических проблем: [N]."
- Never auto-fix findings. Only report and recommend next steps.
- Never commit audit reports automatically. Ask user: "Сохранить отчёт и закоммитить?"
