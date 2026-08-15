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


## Start-of-Work Protocol (MANDATORY — execute before ANY task)

### Step 1: Read Project Documentation (strict order)
Before proposing ANY changes, read documentation in this exact order:
1. **`AGENTS.md`** — Tech stack, build commands, conventions, documentation file order
2. **`context.md`** — System design, architecture decisions, current phase, service boundaries (Russian)
3. **`Backlog.md`** — Sprint backlog, tasks per phase, execution order, blocked items (Mixed EN/RU)
4. **`STRUCTURE.md`** — Project structure, code style, Do/Don't (English)
5. **`TECHNICAL-SOLUTIONS.md`** — Architecture Decision Records, trade-offs, historical context (English)

If any of these files are missing, report which ones are absent and ask the user if they should be created.

### Step 2: Determine Scope Lock
After reading documentation, determine the working mode:

**IF `Backlog.md` EXISTS and is not empty:**
- We are **LOCKED to the backlog scope**. No new features, no scope changes, no "while we're at it" suggestions.
- Identify the **first unchecked task** (`[ ]`) or task in progress (`[~]`).
- If the task is marked `[?]` (Blocked / Needs Human) — **STOP**. Report to user: "Задача заблокирована: [описание блокера]. Требуется решение человека."
- If the task has an **Anti-Scope** section — treat it as hard constraints. Do NOT touch anything listed there.
- **Never rewrite Backlog.md without explicit user approval.** Only update statuses: `[ ]` → `[~]` → `[x]`.

**IF `Backlog.md` DOES NOT EXIST or is empty:**
- Ask the user: "Бэклог не найден или пуст. Создать задачу '[описание]' в новом бэклоге?"
- Do NOT start coding until scope is defined and written to Backlog.md.

**IF user asks for something OUTSIDE Backlog.md:**
- Respond: "Этот запрос выходит за рамки текущего бэклога. Добавить как новую задачу в бэклог или работать вне бэклога?"
- Do NOT silently expand scope.

### Step 3: Context Validation
After reading all 4 files, verify consistency:
- Does `context.md` current phase match the active tasks in `Backlog.md`?
- Are there completed tasks in Backlog.md that are not reflected in context.md?
- Report mismatches to user before starting work.

---

## Execution Pipeline
For ANY complex task (more than 3 files changed, or cross-layer changes, or new API + frontend integration), execute STRICT sequence:

### Phase 1: Architecture (if needed)
- If task involves new routes, API contracts, or service boundaries → consult `dotnet-architect` or `next-js-architect` first.
- Output: updated `context.md`, folder structure, API contract.

### Phase 2: Implementation
- Delegate coding to appropriate skill:
  - `.NET backend` → `dotnet-coder`
  - `Next.js frontend` → `next-js-coder`
- Coder MUST follow all rules from their skill file.
- Coder MUST output list of changed files.

### Phase 3: Mandatory Review (AUTO-TRIGGER)
After Phase 2 completion, run reviews in this order:

**ALWAYS run:**
- `.NET code changed` → apply `dotnet-code-reviewer` checklist
- `Next.js code changed` → apply `next-js-coder-reviewer` checklist

**CONDITIONALLY run additional reviews:**
- **Security-related changes?** (auth, new endpoints, config, secrets, PII handling) → run `dotnet-code-reviewer` Security section with elevated priority.
- **Large refactoring / Phase completion?** (>10 files, architectural shift, monolith split) → run `architect-review` (check global structure integrity, dependency direction, service boundaries).
- **Both stacks changed?** → run BOTH `dotnet-code-reviewer` and `next-js-coder-reviewer` sequentially.

Review output must include:
1. Severity-classified issues (BLOCKER / WARNING / SUGGESTION)
2. File:line references
3. Concrete fix diffs

### Phase 4: Fix Loop
- **If Security BLOCKER found:** STOP pipeline immediately. Do NOT auto-fix. Present to user with `[SECURITY]` tag and require human approval. Security vulnerabilities (injection, hardcoded secrets, broken auth) must be reviewed by a human before any code changes.
- **If regular BLOCKERS exist:** return to Phase 2 (coder) with review comments as input.
- **If only WARNINGS/SUGGESTIONS:** present them to user and ask if they want fixes now or later.

### Phase 5: Architecture Gate (End-of-Phase only)
- Run ONLY after completing a sprint phase or large refactoring.
- Verify: Clean Architecture layers intact, no dependency inversions, service boundaries respected.
- Output: `ARCHITECTURE_DECISIONS.md` update if structural changes occurred.

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
- Changes MassTransit topology or message contracts
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

---

## Communication Rules
- **Speak Russian.**
- **At start:** Report what was found in documentation: "Бэклог найден, текущая задача: [X]. Anti-Scope: [Y]. Скоуп зафиксирован."
- **During work:** Report progress after each phase: "Фаза 1 завершена...", "Фаза 2: изменены файлы...", "Фаза 3: ревью..."
- **If scope violation detected:** "Этот запрос выходит за рамки бэклога. Добавить задачу?"
- **If blocked task:** "Задача [N] заблокирована: [причина]. Требуется решение человека."
- **Never skip Phase 3.** Review is mandatory for complex tasks.
- **If Security BLOCKER:** explicitly say: "Найдена критическая уязвимость безопасности. Авто-исправление запрещено. Требуется ручное ревью."
- **At finish:** "Работа завершена. Изменены файлы: [список]. Хотите сделать коммит?"
