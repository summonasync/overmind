---
name: coding-challenge-orchestrator
description: |
  Orchestrate coding-challenge (test task / тестовое задание) execution end-to-end:
  coding -> review -> AI-trace obfuscation (Opus model) -> obfuscation review -> final validation.
  Goal: production-quality solution with ZERO detectable AI-agent traces.
  Use whenever a task in the backlog is marked as a test task / coding challenge.
type: prompt
whenToUse: |
  When a task is marked as тестовое задание / coding challenge / test task,
  or when the user explicitly says the solution must show no signs of AI involvement.
---

# Coding Challenge Orchestrator Skill

## Mission
Deliver a solution to a test task (тестовое задание) that is production-quality **and** indistinguishable from human-written work. AI traces are a delivery defect on the same level as a failing test.

## Scope Boundary (DO NOT CROSS)
This skill handles **coding-challenge tasks only** — discrete, self-contained assignments where the deliverable will be reviewed by humans (typically hiring managers) who may be hostile to AI usage.

For regular project tasks — use `task-orchestrator`. For system-wide audits — use `audit-orchestrator`.

## Core Rule (ABSOLUTE)
- **Obfuscation is ALWAYS executed by the Opus model** via `ai-obfuscator`. Never by the model that wrote or reviewed the code.
- **Zero AI traces is the target.** Not "few", not "acceptable" — zero detectable signatures.
- **No step is skippable.** The pipeline below is fixed. A coding challenge without obfuscation review is not done.

## Pipeline (FIXED ORDER)

### Phase 0: Task Intake
1. Read the task statement verbatim. Extract: explicit requirements, implicit expectations (framework versions, project layout the reviewer expects), submission format, deadline.
2. **Set up the isolated workspace:** all challenge work lives in `coding-challenges/<task-slug>/`. This folder is the deliverable root. Nothing outside it is touched; nothing from the outer project (docs, configs, agent files) is copied into it.
3. Read project docs per standard order (`AGENTS.md` → `context.md` → `Backlog.md` → `STRUCTURE.md` → `TECHNICAL-SOLUTIONS.md`) **only if the challenge lives inside an existing repo**. For a standalone challenge — the task statement overrides project conventions. The "Coding Challenge Rules" section of `AGENTS.md` ALWAYS applies.
4. Confirm scope with the user: "Задача: [X]. Требования: [список]. Формат сдачи: [Y]. Workspace: `coding-challenges/<slug>/`. Скоуп зафиксирован, приступаю."

### Phase 1: Implementation
- Delegate to the matching coder skill: `dotnet-coder` / `next-js-coder` / `maui-coder`.
- **Constraint override for challenges:** match the *expected human solution style* for the seniority level the challenge targets, not the maximal-standards style. A middle-level challenge solved with gold-plated architecture is an AI trace in itself.
- Output: list of changed/created files.

### Phase 2: Code Review
- Apply the matching reviewer (`dotnet-code-reviewer` / `next-js-code-reviewer`).
- BLOCKERS → back to Phase 1 with comments. Repeat until no BLOCKERS.
- **Do not over-polish style** — reviewer checks correctness, security, and requirement coverage. Perfect style consistency is the obfuscator's problem to break later.

### Phase 3: Obfuscation (Opus MODEL ONLY)
- Hand the full changed-file list to `ai-obfuscator`.
- The obfuscator follows its own workflow (analyze → plan → transform → verify → checklist).
- Output: per-file report of techniques applied.

### Phase 4: Obfuscation Review
- Apply `ai-obfuscation-reviewer` to all transformed files.
- **BLOCKER (residual AI traces)** → back to Phase 3 with the review as input.
- **BLOCKER (integrity broken)** → back to Phase 1 coder for the broken logic, then re-run Phases 3-4 on the affected files.
- Repeat until `PASS`.

### Phase 5: Final Validation
- Run the full verification suite as the challenge reviewer would:
  - clean checkout build: `dotnet build` / `npm run build` from scratch
  - `dotnet test` / test runner — all green
  - lint / format checks **as configured in the challenge repo** (not our project defaults)
  - requirements checklist: every explicit requirement from Phase 0 mapped to where it's implemented
- **AI-leakage sweep of the deliverable folder** (mandatory, hostile review):
  - no `AGENTS.md`, `CLAUDE.md`, `.claude/`, `.cursor/`, `.ai/`, agent configs, or skill references anywhere in `coding-challenges/<task-slug>/`
  - no mentions of AI tooling in code comments, README, configs, or git history
  - no AI-perfect README (delegate README to `documentation-writer`, then obfuscate it too — AI-perfect READMEs are a common tell)
  - commit history (if any) looks human: message style, count, no co-author trailers
- Final report to user:

```markdown
## Coding Challenge — готово
- Задача: [X]
- Workspace: `coding-challenges/<slug>/`
- Файлы: [список]
- Требования: [N/N покрыто]
- Сборка/тесты: OK
- Обфускация: PASS (Opus, N итераций)
- Коммит: не сделан — сделать коммит?
```

## Git & Commit Control (STRICT)
- **NEVER run `git commit` or `git push` automatically** — even more critical here: commit messages and timestamps are also AI tells. Discuss commit strategy with the user explicitly (message style, one commit vs several, author config).
- After completion ask: "Сделать коммит? Каким сообщением и сколько коммитов — как писал бы человек?"

## Complexity Note
Coding challenges are **always complex by definition** — the full pipeline applies regardless of file count.

## Communication Rules
- **Speak Russian.**
- Report at each phase boundary: "Фаза N завершена: [итог]. Переход к фазе N+1."
- If the task statement is ambiguous — STOP and ask before Phase 1. Guessing wrong on a challenge wastes the whole pipeline.
- Never mention obfuscation phases in any artifact that ships with the deliverable (README, commit messages, code comments).
