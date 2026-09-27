---
feature: 003
slug: crew-role-boundaries
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Spec 003 — Crew role boundaries: feature-orchestrator rename, sprint-planner, stack drift fix

Every change below lands on all four runtimes (.kimi-code / .claude / .gemini/antigravity / .qwen) in the same commit, per SYNC-STRUCTURE.md. Paths written once mean "the mapped file on every side".

## 1. Rename task-orchestrator → feature-orchestrator

- Move `skills/task-orchestrator/` → `skills/feature-orchestrator/`; `agents/task-orchestrator.md` → `agents/feature-orchestrator.md` (×4).
- Frontmatter: `name: feature-orchestrator`; description/whenToUse rewritten to name the real scope: "Owns the pipeline INSIDE one feature: intake → spec gate → plan gate → task breakdown → coder dispatch → review → fix loop → result. Trigger: user asks to implement a feature / complex bug on the board." Body content otherwise unchanged (it is already feature-centric).
- Update every cross-reference (×4): SKILLFLOW.md rows 0.0/0.1/0.2/Tasks; `overmind-ceremony` SKILL.md (whenToUse sentence + «Relationship to Other Skills»); next-step/routing lines in `dotnet-coder`, `dotnet-init`, `dotnet-architect`, `next-js-architect`, `next-js-coder`, `maui-coder`, `documentation-writer`, `fast-fixer-coder`, `audit-orchestrator`, `coding-challenge-orchestrator` SKILL.md; crew contract files (CLAUDE.md / AGENTS.md / GEMINI.md / QWEN.md, section «This file and task-orchestrator define the pipeline»); SYNC-STRUCTURE.md file-mapping rows + frontmatter-affected set; `manifest.json` deploy entry.
- Kimi-side `agents/task-orchestrator.md` carries a `subagents:` list — keep, no self-reference to fix; Antigravity twin mirrors it.
- No alias/compat shim: the old name disappears completely.

## 2. New skill: sprint-planner (board level)

- New `skills/sprint-planner/SKILL.md` + `agents/sprint-planner.md` (×4).
- Scope: board-level planning only — `planning_task_queue` triage, priority bands (`planning_move_feature`; band crossings only on explicit user approval via `planning_set_feature_priority`), feature dependencies (`planning_add/remove_feature_dependency`), feature creation ceremony (`planning_create_feature` + intake routing), sprint/phase slicing, phase-end handoff to `audit-orchestrator`.
- Anti-scope: never works a feature's inner pipeline (→ feature-orchestrator), never codes, never reviews, never touches task artifacts, ceremony mechanics defer to `overmind-ceremony`.
- Model per runtime convention: claude `opus`, qwen `qwen3.8-max`, kimi pool k3, Antigravity role-based wording.
- Registration: `manifest.json` entries (skill dir + agent), SYNC-STRUCTURE.md mapping rows, SKILLFLOW.md new alternative route «Board planning (sprint)».
- Count parity shifts: 22→23 agents, 25→26 skills per side; SYNC-STRUCTURE pre-commit checklist numbers updated.

## 3. Stack drift fix (C1)

- `dotnet-coder` SKILL.md (×4): CQRS → `Mediator` (martinothamar), one handler per file; Messaging → `Wolverine` (MassTransit only in legacy codebases); Testing → xUnit + `Shouldly` + NSubstitute; mapping → Mapperly or manual; Runtime → .NET 10+ (latest LTS), C# 14+; description line «MassTransit conventions» reworded; Architecture-rules lines referencing MassTransit get the legacy clause.
- Sweep and apply the same drift fix where present: `dotnet-code-reviewer` (review checklist must flag MediatR/MassTransit/AutoMapper/FluentAssertions in NEW code as violations; legacy = allowed with a note), `dotnet-init`, `maui-coder`, `dotnet-best-practices` (BP-IDs pinning the banned libs).
- `nuget-licensing-guard` is already correct — align wording with it, do not duplicate its tables.

## 4. Coder = single-task implementator (C4)

- Remove "review" from all coder triggers/descriptions (dotnet-coder, next-js-coder, maui-coder — frontmatter + whenToUse, ×4): "Write, edit, or refactor…".
- `dotnet-coder`: replace the self-review «Post-Task Review Protocol» section with a handoff rule — Complex task → reviewer as a SEPARATE agent dispatched by feature-orchestrator (Phase 3); coder never applies the reviewer checklist to its own output. Check next-js-coder / maui-coder for the same pattern and fix likewise.

## 5. Architect = Phase 1 consultant (C3)

- `dotnet-architect`, `next-js-architect` (×4): strip «plan features / planning large features» from description/whenToUse; add "consulted BY feature-orchestrator (Phase 1) — never runs planning gates itself"; update their next-step lines to the renamed orchestrator.

## 6. Explicit model pin for overmind-ceremony agent (C5)

- `.claude/agents/overmind-ceremony.md`: add `model: opus` (orchestration role). Other sides mirror the INTENT per their conventions (kimi pool pin, qwen `qwen3.8-max`, Antigravity role wording) — verify what exists per side and close the gap.

## 7. BACKLOG.md reconciliation (C6)

- `overmind-ceremony` SKILL.md (×4): replace «There is NO `BACKLOG.md` index anymore» with the true rule: `BACKLOG.md` at project root is a materialized index generated by Overmind — read-only, same status as the rest of `BACKLOG/`.
- Working tree: revert the hand edits in `BACKLOG.md` and `BACKLOG/002-superpowers-gap-closure/feature.md`, then run `planning_materialize` and confirm a clean pass (no refusals, no drift).

## Acceptance criteria

- [ ] `grep -rn "task-orchestrator"` across all four runtime dirs + root docs (CLAUDE/AGENTS/GEMINI/QWEN/SYNC-STRUCTURE/manifest) returns zero hits; historical hits allowed only under `audits/` and `BACKLOG/` (materialized history).
- [ ] `feature-orchestrator` exists as skill + agent on all four sides; sprint-planner likewise.
- [ ] Count parity: 23 agents + 26 skills on every side; SYNC-STRUCTURE checklist updated.
- [ ] All five SYNC-STRUCTURE contamination greps come back empty.
- [ ] `dotnet-coder` ×4 recommends Mediator/Wolverine/Shouldly/Mapperly and treats the banned stack as legacy-only; reviewer checklist flags it in new code.
- [ ] No coder skill contains a self-review protocol; "review" absent from coder triggers ×4.
- [ ] Architect triggers contain no feature-planning claims ×4.
- [ ] overmind-ceremony agent has an explicit model per side convention.
- [ ] Ceremony doc states BACKLOG.md is a materialized index; `planning_materialize` passes clean afterwards.
- [ ] `install.sh --dry-run` and `uninstall.sh --dry-run` are clean (manifest consistency).

## Constraints

- One commit, four sides (quadruple-edit rule); pre-commit checklist of SYNC-STRUCTURE.md run in full.
- English-only artifact text; no behavior changes outside the seven items above.

<log></log>

<log>
- 2026-09-27 [agent] — validated with human proof: notification 7454ac23-5610-49b6-bc57-f2b9cb49105d replied 2026-09-27 19:55:31 UTC
</log>
