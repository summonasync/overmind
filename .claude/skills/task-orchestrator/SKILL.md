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

## Scope Boundary (DO NOT CROSS)
This skill handles **task-based development workflows** only: planning → coding → review for a specific feature/bug/fix.

For **system-wide audits** (sprint retrospectives, phase-end validation, security/compliance checks, comprehensive health checks) — use `audit-orchestrator`. Do NOT attempt to perform full-project audits within this skill.

For **coding challenges / тестовые задания** (any task marked as a test task, where the deliverable must show zero AI-agent traces) — hand over to `coding-challenge-orchestrator` immediately. Its fixed pipeline (coding → review → obfuscation via Opus → obfuscation review → validation) replaces this skill's pipeline entirely. Detect the marker at intake: task tagged as тестовое/challenge, or the user states the work is a test assignment.


## Start-of-Work Protocol (MANDATORY — execute before ANY task)

### Step 1: Read Project Documentation (strict order)
Before proposing ANY changes, read documentation in this exact order:
1. **`AGENTS.md`** — Tech stack, build commands, conventions, documentation file order
2. **`CONTEXT.md`** — System design, architecture decisions, current phase, service boundaries (Russian)
3. **`BACKLOG.md`** — Sprint backlog, tasks per phase, execution order, blocked items (Mixed EN/RU)
4. **`STRUCTURE.md`** — Project structure, code style, Do/Don't (English)
5. **`TECHNICAL-SOLUTIONS.md`** — Architecture Decision Records, trade-offs, historical context (English)
6. **`TASKS/`** — after identifying the current feature in BACKLOG.md (Step 2), open its folder `TASKS/nnn-slug/`. Orchestrator reads `feature.md` and `plan.md` (if present); the executing sub-agent reads ONLY its `tasks/NN-foo.md` IN FULL. Task files are the authoritative execution contracts; BACKLOG.md is only the index.

**Naming rule:** root filenames are exact and case-sensitive — always ALL CAPS as listed above. Inside feature folders, files are lowercase: `feature.md`, `plan.md`, `tasks/NN-foo.md`.

If any of these files are missing, report which ones are absent and ask the user if they should be created.

### Step 2: Determine Scope Lock
After reading documentation, determine the working mode:

**IF `BACKLOG.md` EXISTS and is not empty:**
- We are **LOCKED to the backlog scope**. No new features, no scope changes, no "while we're at it" suggestions.
- Identify the **first unchecked task** (`[ ]`) or task in progress (`[~]`).
- If the task is marked `[?]` (Blocked / Needs Human) — **STOP**. Report to user: "Задача заблокирована: [описание блокера]. Требуется решение человека."
- Open the feature folder `TASKS/nnn-slug/`, then load the current task file `tasks/NN-foo.md`. Its `<anti-scope>` section is hard constraints — do NOT touch anything listed there. `<acceptance-criteria>` + spec-compliance becomes the Phase 3 review checklist.
- **Never rewrite BACKLOG.md, feature.md, plan.md or task files without explicit user approval.** Only update statuses in the BACKLOG.md index (`[ ]` → `[~]` → `[x]`), task-file frontmatter `status:`, and append to `<log>` / `<obstacles>`. Approved feature.md/plan.md are frozen — changes require re-validation with the user.

**IF `BACKLOG.md` DOES NOT EXIST or is empty:**
- Ask the user: "Бэклог не найден или пуст. Создать задачу '[описание]'?"
- On approval for a Complex task: create the feature folder `TASKS/nnn-slug/` from `TASKS/_TEMPLATE/` AND add an index row to BACKLOG.md. Simple tasks stay one-liners in BACKLOG.md.
- Do NOT start coding until scope is defined and written down.

**IF user asks for something OUTSIDE BACKLOG.md:**
- Respond: "Этот запрос выходит за рамки текущего бэклога. Добавить как новую задачу в бэклог или работать вне бэклога?"
- Do NOT silently expand scope.

### Step 3: Context Validation
After reading all 5 files, verify consistency:
- Does `CONTEXT.md` current phase match the active tasks in `BACKLOG.md`?
- Are there completed tasks in BACKLOG.md that are not reflected in CONTEXT.md?
- Report mismatches to user before starting work.

---

## Execution Pipeline
For ANY complex task (more than 3 files changed, or cross-layer changes, or new API + frontend integration), execute STRICT sequence.

**Exception:** pure documentation tasks (`documentation-writer`) and learning-material tasks (`tutor-master`) skip Phase 3 code review — apply the target skill's own verification checklist instead.

### Phase 0: Spec & Plan Gates (Complex features — MANDATORY, sequential)
1. **Spec gate.** Build `TASKS/nnn-slug/feature.md` from `TASKS/_TEMPLATE/feature.md` — use `superpowers:brainstorming` as the questioning engine (Socratic, one question at a time) until requirements are unambiguous. **STOP. Present feature.md to the user. No planning until explicit approval.**
2. **Plan gate.** Build `TASKS/nnn-slug/plan.md` from `TASKS/_TEMPLATE/plan.md` — decomposition style per `superpowers:writing-plans` (small tasks, exact file paths, verification step per task). **STOP. Present plan.md to the user. No task breakdown until explicit approval.**
3. **Task breakdown.** After plan approval: create `TASKS/nnn-slug/tasks/NN-foo.md` per task from `TASKS/_TEMPLATE/task.md`, each linking back to feature.md/plan.md via `<refs>`. Add/update the BACKLOG.md index row.
4. Approved feature.md/plan.md become **frozen snapshots**. Any change mid-flight → re-validate with the user, bump `version:` in frontmatter, never edit silently.
5. Medium features (no validation ceremony needed) MAY skip spec/plan — folder with `tasks/` only. Simple tasks never get a folder.

### Phase 1: Architecture (if needed)
- If task involves new routes, API contracts, or service boundaries → consult `dotnet-architect` or `next-js-architect` first.
- Output: updated `CONTEXT.md`, folder structure, API contract.
- **Requirements unclear or feature under-specified?** → return to Phase 0 spec gate: `superpowers:brainstorming` as the questioning engine (Socratic, one question at a time). Output lands in `TASKS/nnn-slug/feature.md` / `CONTEXT.md` — NEVER in `docs/superpowers/`.

### Phase 2: Implementation
- Delegate work to the appropriate skill:
  - `.NET backend` → `dotnet-coder`
  - `Next.js frontend` → `next-js-coder`
  - `.NET MAUI app` (pages, view models, XAML, platform code) → `maui-coder`
  - `Documentation` (README, ADR, runbooks, doc restructure) → `documentation-writer`
  - `Learning materials` (study plans, concept explanations, assessments) → `tutor-master`
- Coder MUST follow all rules from their skill file.
- Coder MUST output list of changed files.
- Coder receives ONLY its task file `TASKS/nnn-slug/tasks/NN-foo.md` as primary input (feature.md/plan.md stay with orchestrator and reviewer). Coder MUST append discovered workarounds, quirks, and environment issues to the task file's `<obstacles>` section, and a one-line entry to `<log>`.
- **Before adding/upgrading any third-party NuGet dependency** → consult `nuget-licensing-guard`.
- **Decomposition style** → `superpowers:writing-plans` (small tasks, exact file paths, verification steps per task); the plan itself lives in `TASKS/nnn-slug/plan.md` (Phase 0 plan gate).
- **TDD** → `superpowers:test-driven-development` applies ONLY to domain/business logic per AGENTS.md «Superpowers Integration» rule 4. Its commit step is DISABLED — see Git & Commit Control below.
- **Stuck on a bug?** → `superpowers:systematic-debugging` (root cause first, no guess-fixes) before any fix attempt.

### Phase 3: Mandatory Review (AUTO-TRIGGER)
After Phase 2 completion, run reviews in this order:

**ALWAYS run:**
- `.NET code changed` → apply `dotnet-code-reviewer` checklist
- `Next.js code changed` → apply `next-js-coder-reviewer` checklist
- `MAUI code changed` → apply `dotnet-code-reviewer` checklist + `maui-coder` rules (MVVM, code-behind purity, platform isolation)

**Review depth from the task file:** read the `review:` frontmatter field of the executed task (`light` default / `hard`).
- `light` → matching code reviewer checklist only.
- `hard` → code reviewer + `architect-review` (Light checklist, both stacks) in the same Phase 3 pass.

**CONDITIONALLY run additional reviews:**
- **Security-related changes?** (auth, new endpoints, config, secrets, PII handling) → run `dotnet-code-reviewer` Security section with elevated priority.
- **Large refactoring / Phase completion?** (>10 files, architectural shift, monolith split) → run `architect-review` Hard checklist (global structure integrity, dependency direction, service boundaries, ADR coverage).
- **Both stacks changed?** → run BOTH `dotnet-code-reviewer` and `next-js-coder-reviewer` sequentially.

Review output must include:
1. Severity-classified issues (BLOCKER / WARNING / SUGGESTION)
2. File:line references
3. Concrete fix diffs

**Two-stage review (per superpowers):** first stage — spec-compliance against `TASKS/nnn-slug/feature.md` + `<acceptance-criteria>`; second stage — code quality. Spec violation is a BLOCKER even if the code is clean.

**Before reporting ANY task as done** → apply `superpowers:verification-before-completion`: prove the fix/feature works (build + tests green, behavior demonstrated), never claim completion on "should work now".

### Phase 4: Fix Loop
- **If Security BLOCKER found:** STOP pipeline immediately. Do NOT auto-fix. Present to user with `[SECURITY]` tag and require human approval. Security vulnerabilities (injection, hardcoded secrets, broken auth) must be reviewed by a human before any code changes.
- **If regular BLOCKERS exist:** return to Phase 2 (coder) with review comments as input.
- **If only WARNINGS/SUGGESTIONS:** present them to user and ask if they want fixes now or later.

### Phase 5: Architecture Gate (End-of-Phase only)
- Run ONLY after completing a sprint phase or large refactoring.
- Executed by `architect-review` (Hard checklist): layers intact, no dependency inversions, service boundaries respected, ADR coverage.
- Output: `architect-review-YYYY-MM-DD.md` + `TECHNICAL-SOLUTIONS.md` update (new ADR entry) if structural changes occurred — user approves ADR writes.

---

## Git & Commit Control (STRICT — applies to ALL delegated skills)
- **NEVER run `git commit` from any skill automatically.** Do NOT commit any changes unless the user explicitly asks: "закоммить", "сделай коммит", "commit".
- **NEVER run `git push` automatically.** Push ONLY after explicit user request.
- **Skills MAY stage files** (`git add`) to show what changed, but do NOT commit.
- **After completing ALL phases, report the list of changed files** and ask: "Изменены файлы: [список]. Хотите сделать коммит?"
- If the user says nothing about commit — leave changes unstaged/uncommitted.
- **Orchestrator MUST enforce this rule:** if any sub-skill attempts to commit, stop it and report to user.
- **Superpowers skills contain built-in commit steps (spec commit, TDD cycle commit, branch finishing). These are DISABLED.** This file's no-auto-commits rule overrides any skill instruction, including `superpowers:*`.

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
| Frontend | Next.js files changed | `next-js-coder-reviewer` |
| Backend | C# files changed | `dotnet-code-reviewer` |
| MAUI | .NET MAUI files changed (XAML, view models, Platforms/) | `dotnet-code-reviewer` + `maui-coder` rules |
| Docs | Documentation files created/changed | `documentation-writer` verification checklist |

---

## Communication Rules
- **Speak Russian.**
- **At start:** Report what was found in documentation: "Бэклог найден, текущая фича: [X] (`TASKS/nnn-slug/`), задача: `tasks/NN-foo.md`. Anti-Scope: [Y]. Скоуп зафиксирован."
- **During work:** Report progress after each phase: "Фаза 1 завершена...", "Фаза 2: изменены файлы...", "Фаза 3: ревью..."
- **If scope violation detected:** "Этот запрос выходит за рамки бэклога. Добавить задачу?"
- **If blocked task:** "Задача [N] заблокирована: [причина]. Требуется решение человека."
- **Never skip Phase 3.** Review is mandatory for complex tasks.
- **If Security BLOCKER:** explicitly say: "Найдена критическая уязвимость безопасности. Авто-исправление запрещено. Требуется ручное ревью."
- **At finish:** "Работа завершена. Изменены файлы: [список]. Хотите сделать коммит?"
