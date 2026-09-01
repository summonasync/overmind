---
name: audit-orchestrator
description: |
  Conduct comprehensive system audits by analyzing project documentation,
  identifying required audit types, and generating a structured report as audits/audit-YYYY-MM-DD.md.
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
2. **`CONTEXT.md`** — System design, current phase, architecture decisions, service boundaries (Russian)
3. **`BACKLOG.md`** — Sprint state: completed tasks, in-progress items, blocked tasks, deferred tech debt (Mixed EN/RU)
4. **`STRUCTURE.md`** — Code style, architecture rules, Do/Don't (English)
5. **`TECHNICAL-SOLUTIONS.md`** (if exists) — Historical ADRs

**Naming rule:** filenames are exact and case-sensitive — always ALL CAPS as listed above.

### Step 2: Determine Audit Scope
Based on documentation analysis, determine which audit types are relevant:

**Always run (Base Audit):**
- Code Quality Audit — check against STRUCTURE.md and AGENTS.md rules

**Conditional audits (triggered by backlog/context):**

| Trigger in Backlog/Context | Audit Type | What to Check |
|---------------------------|------------|---------------|
| Auth/Identity tasks, new endpoints, secret handling | **Security Audit** | AuthZ, secrets, injection, deserialization, CORS |
| Refactoring tasks, new microservices, phase changes | **Architecture Audit** | Clean Architecture layers, coupling, service boundaries |
| EF migrations, performance tasks, API optimization | **Performance Audit** | N+1 queries, async misuse, caching, messaging topology |
| Deferred items, TODO/FIXME in code, legacy mentions | **Tech Debt Audit** | Deprecated APIs, uncompleted tasks, migration blockers |
| Messaging topology changes, saga additions | **Messaging Audit** | Queue design, retry policies, outbox, saga state machines |
| Frontend changes, Next.js upgrades, BFF modifications | **Frontend Audit** | RSC compliance, BFF pattern, component decomposition |
| MAUI project present, mobile/desktop tasks | **MAUI Audit** | MVVM compliance, code-behind purity, platform code isolation, main-thread violations |
| Doc tasks, doc drift suspected, onboarding complaints | **Documentation Audit** | Docs vs code accuracy, stale commands/paths, contradiction with AGENTS.md/STRUCTURE.md |
| Incoming vendor repo, AI-usage suspicion, pre/post-obfuscation check | **AI Trace Audit** | Delegate to `ai-code-detector` (strongest reasoning model): where AI code lives, volume, signatures |

**If no specific triggers** — run Base + Architecture + Security as default.

### Step 3: Scan Project Structure
- List all `.csproj` files and their dependencies (check for circular refs)
- List all projects in solution and their layer (Domain/Application/Infrastructure/API)
- Identify files changed since last audit (previous audit reports are found by glob `audits/audit-*.md`)
- Check for files without corresponding tests

---

## Audit Execution

**Messaging stack context (from AGENTS.md):** default messaging library is **Wolverine**; Rebus for transport-focused bus; MassTransit ONLY in legacy codebases (v8, support ends after 2026; reassess when OpenTransit matures). All messaging checks below apply to whichever of these is actually used — do NOT flag "not using MassTransit" as an issue. Using MassTransit v9+ IS an issue (commercial license).

### Base Audit — Code Quality
Check the entire codebase against project rules:
- [ ] **No comments in code.** Architecture expressed via naming/structure (comments only for non-obvious decisions).
- [ ] **English-only** strings, exceptions, logs, enums.
- [ ] **One file = one public type.** No God-Services (>5 public methods).
- [ ] **Clean Architecture respected.** No EF imports in Application layer. No business logic in API.
- [ ] **Split files:** Endpoints, DTOs, Handlers, Validators, Services — all separate.
- [ ] **CancellationToken** in all async methods.
- [ ] **No `async void`.**
- [ ] **Test coverage:** Every public service/handler has a test class.
- [ ] **Dependency licenses:** third-party packages comply with `nuget-licensing-guard` rules (no commercial MediatR/MassTransit/AutoMapper/FluentAssertions versions in new code).

### Security Audit (if triggered)
- [ ] **SQL Injection:** No `FromSqlRaw` with interpolation. Use `FromSqlInterpolated`.
- [ ] **Hardcoded secrets:** Check `appsettings*.json`, `.cs`, `.env` for API keys, connection strings, passwords.
- [ ] **AuthZ:** Every endpoint has `.RequireAuthorization()` or explicit `[AllowAnonymous]` with justification.
- [ ] **Insecure deserialization:** No `BinaryFormatter`, no `TypeNameHandling.All` in Json.NET.
- [ ] **CORS:** Not `AllowAnyOrigin() + AllowCredentials()`.
- [ ] **Messaging payloads:** PII encrypted/tokenized, not plain text (Wolverine/Rebus/MassTransit — whichever is in use).
- [ ] **Mass assignment:** DTOs don't expose internal IDs that users can override.
- [ ] **Input validation:** FluentValidation on all public API inputs.
- [ ] **Exception leakage:** Generic messages to client, details only in logs.
- [ ] **EF client eval:** No `AsEnumerable()` before `Where()` on large tables.
- [ ] **Frontend:** No `dangerouslySetInnerHTML` with unsanitized input. No secrets in client bundle.

### Architecture Audit (if triggered)
- [ ] **Layer integrity:** Domain → Application → Infrastructure → API. No bypasses.
- [ ] **No shared databases** between microservices.
- [ ] **No direct HTTP** between services. Messaging only (Wolverine default; Rebus for transport-focused; MassTransit legacy only).
- [ ] **Service boundaries:** Each service has <5 public methods or is split by subdomain.
- [ ] **Strongly-typed IDs:** Used for all domain entities (no primitive obsession).
- [ ] **Aspire orchestration:** AppHost has no business logic. ServiceDefaults have health checks, OTel, resilience.
- [ ] **Messaging topology:** One queue per consumer type. Retry at bus level. Outbox for critical messages.
- [ ] **Frontend BFF:** No direct browser-to-.NET calls. All through Next.js backend.

### Performance Audit (if triggered)
- [ ] **N+1 queries:** Check EF queries for `Include()` usage, projection with `Select()`.
- [ ] **Async/await:** No sync-over-async (`.Result`, `.Wait()`). No `async void`.
- [ ] **Caching:** Redis/IMemoryCache used for hot data. Cache invalidation strategy exists.
- [ ] **Messaging:** Sagas use a state machine (Wolverine saga / MassTransitStateMachine), not manual orchestration. Consumers are idempotent.
- [ ] **API:** Streaming endpoints use `IAsyncEnumerable<T>`. No large in-memory collections.
- [ ] **Frontend:** No `useEffect` for data fetching in RSC. Images use `next/image` with sizes.

### Tech Debt Audit (if triggered)
- [ ] **TODO/FIXME count** in codebase. Trend vs previous audit.
- [ ] **Deprecated APIs:** current .NET features not used where applicable (e.g., old `System.Text.Json` patterns).
- [ ] **Migration backlog:** Pending EF migrations not applied. Migration naming convention followed.
- [ ] **Package versions:** Outdated NuGet/npm packages with known vulnerabilities.
- [ ] **License time-bombs:** MassTransit v8 installations flagged with migration plan (support ends after 2026; evaluate OpenTransit readiness).
- [ ] **Documentation drift:** `CONTEXT.md` / `BACKLOG.md` / `STRUCTURE.md` out of sync with actual code. BACKLOG.md index statuses match feature folders in `BACKLOG/` (no content duplication between index and folders).

### Messaging Audit (if triggered)
- [ ] **Transport:** RabbitMQ/ASB configured with retry, dead-letter, and outbox (via Wolverine/Rebus; MassTransit only in legacy).
- [ ] **Message contracts:** Versioned, immutable, no breaking changes without versioning.
- [ ] **Saga state:** Sagas use the library's state machine support, not custom orchestration.
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
- [ ] **Consistency:** No contradictions between `CONTEXT.md`, `STRUCTURE.md`, `TECHNICAL-SOLUTIONS.md`, `AGENTS.md`.
- [ ] **Language:** Each doc follows the language convention from the `AGENTS.md` documentation table.
- [ ] **Drift:** Docs describing features changed since last audit were updated in the same commits.
- [ ] **Index/folders sync:** `BACKLOG.md` index rows match existing `BACKLOG/nnn-slug/` folders (no orphans either way).
- [ ] **Feature folder integrity:** no loose task files outside `BACKLOG/nnn-slug/tasks/`; no `spec.md` without at least one task file; no task file whose `<refs>` points to a missing spec/plan.
- [ ] **Frozen artifacts:** `spec.md`/`plan.md` with `status: approved` were not edited after approval without a `version:` bump (check git history) — silent edits of frozen artifacts are a WARNING.
- [ ] **Tombstoning:** removed features/requirements carry `<removed>` / `[-] removed` markers — silent deletions (git diff shows removals without tombstones in docs) are a WARNING: agents resurrect untombstoned deletions from context history.
- [ ] **Dependency materialization:** no prose cross-references between tasks/features ("done when X is ready") outside `<refs>` — every such reference must exist as a `<refs>` entry or a `[?]` BACKLOG row.
- [ ] **Cloud spend guard:** no `gcloud`/`az`/`aws` mutations in shell history or scripts without a corresponding user-approved task — flag any autonomous infra change as BLOCKER.
- [ ] **Superpowers containment:** no `docs/superpowers/` directory exists as a source of truth — if found, flag as WARNING: content must be migrated into `BACKLOG/nnn-slug/spec.md` / `plan.md` / `CONTEXT.md` / `TECHNICAL-SOLUTIONS.md` per AGENTS.md «Superpowers Integration».
- [ ] **Artifact provenance:** every generated report/doc outside `BACKLOG/` lives in `audits/` and carries a skill-prefix (`audits/audit-*`, `audits/review-*`, `audits/obfuscation-*`, `audits/intake-*`) per AGENTS.md provenance rule — anonymous or misplaced artifacts are a WARNING.
- [ ] **SKILLFLOW.md consistency:** routes and transitions described in `SKILLFLOW.md` still match reality (skills referenced there exist, gates listed there are actually enforced in task-orchestrator).

---

## Report Generation

After completing all audit types, generate a structured report:

**Path:** `audits/audit-YYYY-MM-DD.md` (skill-prefix provenance rule)

**Structure:**
```markdown
# Audit Report — YYYY-MM-DD

## Executive Summary
- Audit scope: [Base + Security + Architecture + ...]
- Project phase: [from CONTEXT.md]
- Overall health: [Green / Yellow / Red]
- Critical issues: [N] | Warnings: [N] | Suggestions: [N]

## Findings

### [Category] — Severity: [BLOCKER / WARNING / SUGGESTION]
- **Location:** `File.cs:line` or `app/feature/page.tsx:line`
- **Problem:** [Description]
- **Impact:** [Why this matters]
- **Recommendation:** [Concrete fix or architectural change]
- **Backlog link:** [If applicable, link to BACKLOG/nnn-slug/]

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
- `audits/audit-YYYY-MM-DD-security.md` — detailed security findings
- `audits/audit-YYYY-MM-DD-architecture.md` — architectural analysis with diagrams
- `audits/audit-YYYY-MM-DD-performance.md` — query analysis, profiling notes

---

## Post-Audit Actions
- If **BLOCKER found** → recommend immediate `task-orchestrator` session to fix
- If **architecture drift detected** → recommend updating `CONTEXT.md` and `TECHNICAL-SOLUTIONS.md`
- If **tech debt > threshold** → recommend dedicated sprint for debt reduction
- If **no critical issues** → update `BACKLOG.md` with "Audit passed" status

---

## Communication
- **Speak Russian.**
- At start: "Аудит запущен. Анализирую документацию и определяю scope..."
- During audit: "Обнаружены триггеры: [Security, Architecture]. Запускаю соответствующие проверки..."
- At finish: "Аудит завершён. Отчёт: `audits/audit-YYYY-MM-DD.md`. Критических проблем: [N]."
- Never auto-fix findings. Only report and recommend next steps.
- Never commit audit reports automatically. Ask user: "Сохранить отчёт и закоммитить?"

---

## Output Contract (MANDATORY)
End with the fixed closing block, then STOP:
1. **Done** — summary + artifact paths (skill-prefixed per AGENTS.md provenance rule).
2. **Next step** — user triages findings; P0 items become tasks via `task-orchestrator`.
3. **Alternatives** — `architect-review` (Hard) for structural findings; `documentation-writer` for doc-drift fixes.
Suggest, never auto-proceed. The user picks the next step.
