---
name: task-orchestrator
description: |
  Orchestrate complex development tasks through multiple phases: planning -> coding -> review.
  Use when the user asks to implement a feature, fix a complex bug, or create a multi-file component.
  Automatically manages handoff between architect, coder, and reviewer skills.
type: prompt
whenToUse: |
  When the user asks to implement a feature, create a complex component,
  or perform multi-file changes that require review after completion.
---

# Task Orchestrator Skill

All planning state lives on the Overmind board. This skill owns the PIPELINE (phases, review
gates, delegation); ceremony mechanics (which MCP call, which status, which gate, sub-agent
briefing contract) are owned by `overmind-ceremony` — read it before your first board interaction.

## Scope Boundary (DO NOT CROSS)
This skill handles **task-based development workflows** only: planning → coding → review for a specific feature/bug/fix.

For **system-wide audits** (sprint retrospectives, phase-end validation, security/compliance checks, comprehensive health checks) — use `audit-orchestrator`. Do NOT attempt to perform full-project audits within this skill.

For **coding challenges / тестовые задания** (any task marked as a test task, where the deliverable must show zero AI-agent traces) — hand over to `coding-challenge-orchestrator` immediately. Its fixed pipeline (coding → review → obfuscation via Opus → obfuscation review → validation) replaces this skill's pipeline entirely. Detect the marker at intake: task tagged as тестовое/challenge, or the user states the work is a test assignment.


## Start-of-Work Protocol (MANDATORY — execute before ANY task)

### Step 1: Read Project Documentation (strict order)
Before proposing ANY changes, read documentation in this exact order:
1. **`AGENTS.md`** — Tech stack, build commands, conventions
2. **`CONTEXT.md`** — System design, architecture decisions, current phase, service boundaries (Russian)
3. **Overmind board** — `overmind_onboard` (projectId, materialization + validation blocks) → `planning_list_features` (prefer the pinned current feature). The board is the source of truth for what's done, planned, blocked.
4. **`STRUCTURE.md`** — Project structure, code style, Do/Don't (English)
5. **`TECHNICAL-SOLUTIONS.md`** — Architecture Decision Records, trade-offs, historical context (English)
6. **`SKILLFLOW.md`** — workflow graph: valid transitions and alternative routes; used for every «next step» suggestion.
7. **Feature artifacts** — after identifying the current feature on the board (Step 2), read it via `planning_read_feature` (orchestrator view: body + spec + plan) and the current task via `planning_current_task`. The executing sub-agent reads ONLY its task artifact via `planning_read_artifact` IN FULL. Task artifacts are the authoritative execution contracts.

If CONTEXT.md/STRUCTURE.md are missing, report which ones are absent and ask the user if they should be created. If overmind MCP is unavailable — STOP (hard requirement, see `overmind-ceremony`).

### Step 2: Determine Scope Lock
After reading documentation, determine the working mode:

**IF the board has features:**
- We are **LOCKED to the board scope**. No new features, no scope changes, no "while we're at it" suggestions.
- Work the pinned current feature; if none — ask the user which feature to pin (`planning_set_current`).
- `planning_current_task` returns the next task or a stop/empty reason. A **STOP line is authoritative** — report to the user verbatim, never work around it: "Задача заблокирована: [причина]. Требуется решение человека."
- The task artifact's `<anti-scope>` section is hard constraints — do NOT touch anything listed there. `<acceptance-criteria>` + spec-compliance becomes the Phase 3 review checklist.
- **Never rewrite validated spec/plan artifacts without explicit user re-validation.** Routine writes are only: `planning_set_status` (with logNote), `planning_append_log`, `planning_patch_artifact` (sections, AC ticks). Never hand-edit materialized files under `BACKLOG/` — they belong to the materializer.

**IF the board is empty:**
- Ask the user: "Борда пуста. Создать фичу '[описание]'?"
- On approval: `planning_create_feature` (durable what/why in the body). Complex features then go through Phase 0 gates; a Simple task = feature with a single task artifact.
- Do NOT start coding until scope is defined on the board.

**IF user asks for something OUTSIDE the board scope:**
- Respond: "Этот запрос выходит за рамки текущей фичи. Добавить как новую фичу на борду или работать вне борды?"
- Do NOT silently expand scope.

### Step 3: Context Validation
After reading, verify consistency:
- Does `CONTEXT.md` current phase match the active feature on the board?
- Are there done features on the board not reflected in CONTEXT.md?
- Report mismatches to user before starting work.

---

## Execution Pipeline
For ANY complex task (more than 3 files changed, or cross-layer changes, or new API + frontend integration), execute STRICT sequence.

**Exception:** pure documentation tasks (`documentation-writer`) and learning-material tasks (`tutor-master`) skip Phase 3 code review — apply the target skill's own verification checklist instead.

### Phase 0: Spec & Plan Gates (Complex features — MANDATORY, sequential)
0. **Requirements intake (OPTIONAL, trigger-based).** ONLY when requirements arrive in arbitrary/messy form (ticket, letter, chat dump, CSV, design files, conflicting sources): parse, deduplicate, decompose into actionable REQ-items, mark contradictions and gaps — record as entries (`write_entry`, type=note) or in the feature body. **STOP. User confirms requirements are complete — this is the intake gate.** Skip when the user provides already-structured requirements or the feature body already covers the scope.
1. **Spec gate.** Build the spec artifact (`planning_upsert_artifact`, kind=spec) — questioning technique per `overmind-ceremony` (Socratic, ONE question at a time) until requirements are unambiguous. The feature must already exist on the board; if missing and the work is Complex, `planning_create_feature` FIRST and confirm with the user. **STOP. Present the spec to the user. On explicit approval → `planning_validate`. No planning before this.**
2. **Plan gate.** Build the plan artifact — decomposition per `overmind-ceremony` techniques (small tasks, exact file paths, verification step per task). **STOP. Present to the user. On explicit approval → `planning_validate`. No task breakdown before this.**
3. **Task breakdown.** After plan validation: `planning_upsert_artifact(kind=task, order=N, brief=..., review=light|hard)` per task, each with XML sections and `<refs>` linking back to spec/plan.
4. Validated spec/plan are **frozen**. Any change mid-flight → `planning_patch_artifact` with `logNote` + user re-validation, never silent edits.
5. Medium features (no validation ceremony needed) MAY skip spec/plan — tasks only. Simple tasks = single task artifact.

### Phase 1: Architecture (if needed)
- If task involves new routes, API contracts, or service boundaries → consult `dotnet-architect` or `next-js-architect` first.
- Output: updated `CONTEXT.md`, folder structure, API contract.
- **Requirements unclear or feature under-specified?** → return to Phase 0 spec gate: Socratic questioning per `overmind-ceremony`. Output lands in the spec artifact / `CONTEXT.md` — never in side files.

### Phase 2: Implementation
- Delegate work to the appropriate skill:
  - `.NET backend` → `dotnet-coder`
  - `Next.js frontend` → `next-js-coder`
  - `.NET MAUI app` (pages, view models, XAML, platform code) → `maui-coder`
  - `Documentation` (README, ADR, runbooks, doc restructure) → `documentation-writer`
  - `Learning materials` (study plans, concept explanations, assessments) → `tutor-master`
- Coder MUST follow all rules from their skill file.
- Coder MUST output list of changed files.
- Coder receives ONLY its task artifact reference (projectId + featureSlug + artifactId) as primary input — reads it via `planning_read_artifact`, NEVER from disk when MCP is available (spec/plan stay with orchestrator and reviewer; the feature body is readable by all). Coder MUST patch discovered workarounds, quirks, and environment issues into the task artifact's `<obstacles>` section (`planning_patch_artifact`), append a one-line entry to `<log>` (`planning_append_log`), and move statuses (`planning_set_status` with logNote). Full contract: `overmind-ceremony`.
- **Parallel execution** → independent task artifacts with disjoint file sets may be dispatched concurrently per `overmind-ceremony` (Parallel dispatch): one spawn batch, one briefing per coder, all results collected before the next ceremony move.
- **Before adding/upgrading any third-party NuGet dependency** → consult `nuget-licensing-guard`.
- **`.NET` coders load `dotnet-best-practices` with the task artifact** — BP-rules are constraints; reviewers cite violations by BP-ID.
- **Decomposition style** → `overmind-ceremony` plan decomposition (small tasks, exact file paths, verification steps per task).
- **TDD** → strict red-green-refactor ONLY for domain/business logic per `overmind-ceremony` TDD scope. No commit steps anywhere — see Git & Commit Control below.
- **Stuck on a bug?** → root-cause-first debugging (`overmind-ceremony` techniques) before any fix attempt.

### Phase 3: Mandatory Review (AUTO-TRIGGER)
After Phase 2 completion, run reviews in this order:

**ALWAYS run:**
- `.NET code changed` → apply `dotnet-code-reviewer` checklist + `dotnet-best-practices` BP-rules (cite violations as `BP-NN [severity]`)
- `Next.js code changed` → apply `next-js-code-reviewer` checklist
- `MAUI code changed` → apply `dotnet-code-reviewer` checklist + `maui-coder` rules (MVVM, code-behind purity, platform isolation)

**Review depth from the task artifact:** read the `review:` flag of the executed task (`light` default / `hard`).
- `light` → matching code reviewer checklist only.
- `hard` → code reviewer + `architect-review` (Light checklist, both stacks) in the same Phase 3 pass.

**CONDITIONALLY run additional reviews:**
- **Security-related changes?** (auth, new endpoints, config, secrets, PII handling) → run `dotnet-code-reviewer` Security section with elevated priority.
- **Large refactoring / Phase completion?** (>10 files, architectural shift, monolith split) → run `architect-review` Hard checklist (global structure integrity, dependency direction, service boundaries, ADR coverage).
- **Both stacks changed?** → run BOTH `dotnet-code-reviewer` and `next-js-code-reviewer` sequentially.

Review output must include:
1. Severity-classified issues (BLOCKER / WARNING / SUGGESTION)
2. File:line references
3. Concrete fix diffs

**Two-stage review:** first stage — spec-compliance against the spec artifact + `<acceptance-criteria>`; second stage — code quality. Spec violation is a BLOCKER even if the code is clean.

**Before reporting ANY task as done** → verification-before-completion (`overmind-ceremony` techniques): prove the fix/feature works (build + tests green, behavior demonstrated), never claim completion on "should work now".

### Phase 4: Fix Loop
- **If Security BLOCKER found:** STOP pipeline immediately. Do NOT auto-fix. Present to user with `[SECURITY]` tag and require human approval. Security vulnerabilities (injection, hardcoded secrets, broken auth) must be reviewed by a human before any code changes.
- **If regular BLOCKERS exist:** return to Phase 2 (coder) with review comments as input.
- **Review comments are verified, not obeyed:** the coder reproduces every BLOCKER locally before fixing it. A comment that does not hold up is answered with evidence and bounced back to the reviewer — no blind fixes, no performative agreement.
- **If only WARNINGS/SUGGESTIONS:** present them to user and ask if they want fixes now or later.

### Phase 5: Architecture Gate (End-of-Phase only)
- Run ONLY after completing a sprint phase or large refactoring.
- Verify: Clean Architecture layers intact, no dependency inversions, service boundaries respected.
- Output: `TECHNICAL-SOLUTIONS.md` update (new ADR entry) if structural changes occurred.

---

## Git & Commit Control (STRICT — applies to ALL delegated skills)
- **NEVER run `git commit` from any skill automatically.** Do NOT commit any changes unless the user explicitly asks: "закоммить", "сделай коммит", "commit".
- **NEVER run `git push` automatically.** Push ONLY after explicit user request.
- **Skills MAY stage files** (`git add`) to show what changed, but do NOT commit.
- **After completing ALL phases, report the list of changed files** and ask: "Изменены файлы: [список]. Хотите сделать коммит?"
- If the user says nothing about commit — leave changes unstaged/uncommitted.
- **Orchestrator MUST enforce this rule:** if any sub-skill attempts to commit, stop it and report to user.

---

## Complexity Detection
Task is COMPLEX if:
- Touches >3 files
- Spans both frontend and backend
- Introduces new API endpoints or Server Actions
- Modifies database schema or EF migrations
- Changes messaging topology or message contracts (Wolverine/Rebus; MassTransit in legacy codebases)
- Creates new microservice or Aspire project
- Touches auth, authorization, or secrets management

---

## Conditional Review Triggers
| Trigger | Condition | Reviewer |
|---------|-----------|----------|
| Security | Auth, secrets, new endpoints, PII, config changes | `dotnet-code-reviewer` (Security section) |
| Architecture | >10 files, phase completion, structural refactoring | `architect-review` or `dotnet-architect` |
| Frontend | Next.js files changed | `next-js-code-reviewer` |
| Backend | C# files changed | `dotnet-code-reviewer` |
| MAUI | .NET MAUI files changed (XAML, view models, Platforms/) | `dotnet-code-reviewer` + `maui-coder` rules |
| Docs | Documentation files created/changed | `documentation-writer` verification checklist |

---

## Communication Rules
- **Speak Russian.**
- **At start:** Report what was found: "Борда прочитана, текущая фича: [slug], задача: [artifactId/order]. Anti-Scope: [Y]. Скоуп зафиксирован."
- **During work:** Report progress after each phase: "Фаза 1 завершена...", "Фаза 2: изменены файлы...", "Фаза 3: ревью..." Board moves compactly: "таска → inProgress", "spec validated".
- **If scope violation detected:** "Этот запрос выходит за рамки текущей фичи. Добавить на борду?"
- **If STOP line / blocked:** "Задача [N] заблокирована: [причина]. Требуется решение человека." — verbatim, never softened.
- **Never skip Phase 3.** Review is mandatory for complex tasks.
- **If Security BLOCKER:** explicitly say: "Найдена критическая уязвимость безопасности. Авто-исправление запрещено. Требуется ручное ревью."
- **At finish:** "Работа завершена. Изменены файлы: [список]. Хотите сделать коммит?"
- **Output contract (MANDATORY — this skill and every delegated skill):** end with a fixed block, then STOP:
  1. **Done** — summary + artifact identifiers (feature slug, artifact ids) and `audits/` paths where applicable.
  2. **Next step** — recommended follow-up per `SKILLFLOW.md` (exact skill/command name).
  3. **Alternatives** — 2–3 other valid follow-ups.
  Suggest, never auto-proceed. The user picks the next step.
