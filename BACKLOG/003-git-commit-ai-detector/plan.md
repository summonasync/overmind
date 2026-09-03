---
feature: 003
slug: git-commit-ai-detector
status: approved    # draft | approved (approved = FROZEN)
version: 1
created: 2026-09-01
---

# git-commit-ai-detector Implementation Plan

> **For agentic workers:** execute via task-orchestrator, one task file at a time
> (`BACKLOG/003-git-commit-ai-detector/tasks/NN-*.md`). Executing sub-agent reads ONLY its task file.
> Steps use checkbox (`- [ ]`) syntax for tracking. NO commit steps — each task ends with
> «stop and report changed-file list» (AGENTS.md rule 6 overrides any commit step).

**Goal:** Add a `git-commit-ai-detector` skill + agent to the crew (all three runtimes) that forensically analyzes git history and reports per-commit AI-authorship verdicts with era clustering.

**Architecture:** Content-only feature — no code, no tests, no build. One master SKILL.md authored on the Kimi side, then mechanical ports to Claude and Antigravity via the translation tables in `SYNC-STRUCTURE.md`. Code signal catalog (S1–S7) is referenced from `ai-code-detector/SKILL.md`, never duplicated.

**Tech Stack:** Markdown skill/agent files; git porcelain commands (`git log --format`, `git show`, `git diff --cached`) inside the skill body.

## Global Constraints

- Read-only skill: never modifies code, history, or working tree; never commits (verbatim from spec).
- Model policy: Kimi `K3` → Claude `Opus` → Antigravity `strongest reasoning model` (per SYNC-STRUCTURE.md translation tables).
- Report artifact name: `audits/git-commit-ai-detection-YYYY-MM-DD.md` (provenance rule).
- Code signals S1–S7 are consumed by REFERENCE to `.kimi-code/skills/ai-code-detector/SKILL.md`; that file is NOT modified.
- Triple-edit: every runtime-side file exists on all three sides in the same change set; contamination greps from SYNC-STRUCTURE.md pre-commit checklist must be empty.
- SKILLFLOW.md is NOT touched (precedent: ai-code-detector / ai-obfuscator are not registered there).
- `ai-code-detector`'s existing S8 git-signal section stays as-is (it is a code-scan fallback signal, not a conflict).

---

### Task 01: Kimi master — SKILL.md + agent file

**Files:**
- Create: `.kimi-code/skills/git-commit-ai-detector/SKILL.md`
- Create: `.kimi-code/agents/git-commit-ai-detector.md`

**Interfaces:**
- Produces: the master content that Tasks 02/03 translate. Section order and signal IDs (G1–G5, S1–S7-by-reference) are contractual for the ports.
- Consumes: signal structure/scoring style of `.kimi-code/skills/ai-code-detector/SKILL.md` (read-only); agent file shape of `.kimi-code/agents/ai-code-detector.md`.

Deliverable: Kimi-side skill + agent, full content embedded in the task file. Verification: frontmatter parses, all spec-required sections present, provenance/report name correct, no Claude/Gemini leakage words.

### Task 02: Claude port

**Files:**
- Create: `.claude/skills/git-commit-ai-detector/SKILL.md`
- Create: `.claude/agents/git-commit-ai-detector.md`

**Interfaces:**
- Consumes: the two files from Task 01 + translation table «Kimi → Claude» in `SYNC-STRUCTURE.md`.
- Produces: semantically identical Claude-side skill + agent (`K3`→`Opus`, paths→`~/.claude/`, `Agent`→`Task`, etc.).

Verification: Claude contamination grep empty (`k2\.7|\bk3\b|kimi|agentswarm` in the new files).

### Task 03: Antigravity port

**Files:**
- Create: `.gemini/antigravity/skills/git-commit-ai-detector/SKILL.md`
- Create: `.gemini/antigravity/agents/git-commit-ai-detector.md`

**Interfaces:**
- Consumes: the two files from Task 01 + translation table «Kimi → Antigravity» in `SYNC-STRUCTURE.md` (role-based model wording, Agent Manager phrasing).
- Produces: semantically identical Antigravity-side skill + agent.

Verification: Antigravity contamination grep empty (`k2\.7|\bk3\b|kimi|agentswarm|config\.toml|sonnet|opus|haiku|\.claude` in the new files).

### Task 04: Sync metadata + verification gates

**Files:**
- Modify: `SYNC-STRUCTURE.md` — two file-mapping rows (skill dir + agent file, all three columns) + count parity line (20→21 agents, 22→23 skills).
- Modify: `BACKLOG.md` — index row status update for 003.

**Interfaces:**
- Consumes: files from Tasks 01–03 (they must exist before gates run).
- Produces: the pre-commit checklist evidence (grep outputs) recorded in the task file's `<log>`.

Verification (all must pass):
```bash
grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku\|\.claude' .gemini/   # empty
grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/                                                # empty
grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/                                            # empty
grep -rni 'gemini\|antigravity' .kimi-code/ .claude/                                                # empty
diff <(ls .kimi-code/skills) <(ls .claude/skills) && diff <(ls .kimi-code/skills) <(ls .gemini/antigravity/skills)  # no diff
diff <(ls .kimi-code/agents) <(ls .claude/agents) && diff <(ls .kimi-code/agents) <(ls .gemini/antigravity/agents)  # no diff
```

---

## Self-Review Notes

- Spec coverage: SKILL.md structure (Task 01), G1–G5 catalog (Task 01, embedded content), scoring/tooling downgrade (Task 01), both workflows + git commands (Task 01), report format + audits/ name (Task 01), agent file (Task 01), ports (02/03), SYNC-STRUCTURE rows + parity + gates (04), BACKLOG index (04). SKILLFLOW decision recorded in Global Constraints.
- No placeholders: Task 01 carries the full SKILL.md/agent content verbatim; ports are mechanical.
- Type consistency: signal IDs (G1–G5), report filename, section names are identical across plan, spec, and task files.

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version.
Читатели: оркестратор, ревьюер. Исполняющий субагент plan.md НЕ читает —
его контракт — tasks/NN-foo.md.
-->
