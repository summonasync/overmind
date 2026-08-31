# Dual-Tool Repo Layout (.kimi-code + .claude) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the repo into `.kimi-code/` (Kimi Code CLI) and `.claude/` (Claude Code) homes with adapted copies of all agents/skills, plus CLAUDE.md, README.md, SYNC-STRUCTURE.md and sync scripts.

**Architecture:** Physical `git mv` of existing content into `.kimi-code/`; full adapted copies under `.claude/` produced by a mechanical replacement pass + manual frontmatter rework; drift controlled by SYNC-STRUCTURE.md; deployment by `sync.ps1`/`sync.sh`.

**Tech Stack:** markdown, git mv, PowerShell (robocopy), bash.

**Spec:** `docs/superpowers/specs/2026-08-31-dual-tool-repo-layout-design.md`

## Global Constraints

- **No auto-commits:** commit steps below are suggestions; run `git commit` only after explicit user confirmation in the executing session. `git mv` (staging renames) is allowed.
- Do not lose files: 17 agent files, 18 skill dirs, `TASK/`, `SOCIAL_DICTIONARY.md`, `config.toml` must all land under `.kimi-code/`.
- `.claude/` content must contain ZERO occurrences of: `~/.kimi-code`, `K2.7`, `K3`, `kimi-code`, `AgentSwarm`, `config.toml`.
- `.kimi-code/` content must contain ZERO occurrences of: `~/.claude`, `sonnet`, `opus`, `haiku`, `Claude Code`.
- Model mapping: K2.7 → `sonnet` (default coder/reviewer); K3 → `opus` (orchestration, audit, ai-code-detector, ai-obfuscator). `haiku` only where a skill mentions cheap/lightweight auxiliary work.
- Content of code-style/workflow rules is tool-agnostic — copy verbatim, replace only tool-specific fragments.
- All repo file content in English; conversational language with the user is Russian.

## Replacement Table (used by Tasks 4-6)

Apply in this exact order (longest/most specific first):

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.claude/skills` |
| 2 | `~/.kimi-code/plugins/managed/superpowers` | `superpowers plugin for Claude Code` |
| 3 | `~/.kimi-code/` | `~/.claude/` |
| 4 | `kimi-code/kimi-for-coding-highspeed` | `haiku` |
| 5 | `kimi-code/kimi-for-coding` | `sonnet` |
| 6 | `kimi-code/kimi-k3` | `opus` |
| 7 | `kimi-code/k3-256k` | `opus` |
| 8 | `kimi-code/k3` | `opus` |
| 9 | `Kimi K2.7` | `Claude Sonnet` |
| 10 | `Kimi K3` | `Claude Opus` |
| 11 | `K2.7` | `Sonnet` |
| 12 | `K3` | `Opus` |
| 13 | `Kimi Code CLI` | `Claude Code` |
| 14 | `kimi-code` | `claude` (only in path/identifier contexts — review each hit) |
| 15 | `AgentSwarm` | `multiple Task tool calls in one message` |
| 16 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |

Rule 14 is the only fuzzy one: every hit must be eyeballed; in prose like "kimi-code's config" it becomes "Claude Code's settings".

---

### Task 1: Move existing content into `.kimi-code/`

**Files:**
- Create: `.kimi-code/` (via git mv)
- Modify: none

**Interfaces:**
- Produces: `.kimi-code/agents/` (17 files), `.kimi-code/skills/` (18 dirs), `.kimi-code/TASK/`, `.kimi-code/SOCIAL_DICTIONARY.md`, `.kimi-code/config.toml` — consumed by Tasks 3-6 as the adaptation source.

- [ ] **Step 1: git mv everything**

```bash
mkdir -p .kimi-code
git mv agents .kimi-code/agents
git mv skills .kimi-code/skills
git mv TASK .kimi-code/TASK
git mv SOCIAL_DICTIONARY.md .kimi-code/SOCIAL_DICTIONARY.md
git mv config.toml .kimi-code/config.toml
```

- [ ] **Step 2: Verify nothing lost**

Run: `git status --short | grep -c '^R'` and `ls .kimi-code/agents | wc -l && ls -d .kimi-code/skills/*/ | wc -l`
Expected: renames staged; 17 agents, 18 skills. Root contains only: `.kimi-code/`, `docs/`, `.gitignore`, `AGENTS.md`, `.git/`.

- [ ] **Step 3: Verify history follows**

Run: `git log --follow --oneline -1 .kimi-code/agents/dotnet-coder.md`
Expected: shows prior commit, not "new file".

- [ ] **Step 4: Commit (after user confirmation)**

```bash
git commit -m "Move agent crew files into .kimi-code home directory"
```

---

### Task 2: Fix self-references inside `.kimi-code/`

**Files:**
- Modify: `.kimi-code/agents/*.md` (all 17), `.kimi-code/skills/*/SKILL.md`, `.kimi-code/AGENTS-related refs`

**Interfaces:**
- Consumes: Task 1 layout.
- Produces: `.kimi-code/` files whose internal `~/.kimi-code/...` references are consistent (they already use `~/.kimi-code` absolute paths, so likely no change needed — this task is a verification gate).

- [ ] **Step 1: Grep for stale relative references**

Run: `grep -rn 'skills/' .kimi-code/agents/ | grep -v '~/.kimi-code/skills'`
Expected: no hits (agent files already reference `~/.kimi-code/skills/<name>/SKILL.md` absolutely). If hits appear, fix each to the absolute `~/.kimi-code/...` form.

- [ ] **Step 2: Grep for references to repo-root paths that moved**

Run: `grep -rn 'SOCIAL_DICTIONARY\|config.toml' .kimi-code/agents/ .kimi-code/skills/ | grep -v '~/\.kimi-code'`
Expected: only acceptable hits (e.g. prose mentioning the file name). Any path pointing to old repo location gets corrected to `~/.kimi-code/...`.

- [ ] **Step 3: Commit (after user confirmation)** — only if steps 1-2 produced edits.

---

### Task 3: Port AGENTS.md → CLAUDE.md

**Files:**
- Create: `CLAUDE.md` (repo root)
- Modify: `AGENTS.md` (root) — add layout note, update Documentation Files paths

**Interfaces:**
- Consumes: `AGENTS.md` (root, current version with TASK/ workflow), Replacement Table.
- Produces: `CLAUDE.md` — Claude-side global memory; sync script copies it to `~/.claude/CLAUDE.md`.

- [ ] **Step 1: Create CLAUDE.md as adapted copy**

Copy `AGENTS.md` content, then apply:
- Title stays `# Global Agent Context`.
- Apply Replacement Table rules 1-16.
- Rewrite the `**Enforcement (config, not just prompt):**` block — replace the `config.toml` TOML snippet with:

```markdown
**Enforcement (config, not just prompt):** sub-agent model choice is enforced by the `model` field
in each `.claude/agents/*.md` frontmatter (`sonnet` for coder/reviewer agents, `opus` for
orchestration/audit/ai-obfuscator agents) — not by prompt text alone.
```

- In `Documentation Files` table: `TASK/NNN-slug.md` row path becomes `~/.kimi-code/TASK/NNN-slug.md` on the Kimi side; in CLAUDE.md write `~/.claude/TASK/NNN-slug.md`.
- Keep tone/slang/stack/workflow sections verbatim.

- [ ] **Step 2: Verify no Kimi leakage**

Run: `grep -in 'k2\.7\|k3\|kimi\|agentswarm\|config\.toml' CLAUDE.md`
Expected: zero hits.

- [ ] **Step 3: Update root AGENTS.md**

Add at the top, directly under `# Global Agent Context`:

```markdown
> Repo layout: this file's crew lives in `.kimi-code/` (agents, skills, TASK, dictionary, config).
> Claude Code mirror lives in `.claude/` + `CLAUDE.md`. Dual-edit rule: see `SYNC-STRUCTURE.md`.
```

In the `Documentation Files` table, update the `TASK/NNN-slug.md` row to `~/.kimi-code/TASK/NNN-slug.md` and `SOCIAL_DICTIONARY.md` mention to `~/.kimi-code/SOCIAL_DICTIONARY.md`.

- [ ] **Step 4: Verify AGENTS.md has no Claude leakage**

Run: `grep -in 'claude\|sonnet\|opus\|haiku' AGENTS.md`
Expected: hits only inside the new layout note (`.claude/`, `CLAUDE.md`, SYNC-STRUCTURE mention) — those are intentional.

- [ ] **Step 5: Commit (after user confirmation)**

---

### Task 4: Create `.claude/skills/` adapted copies

**Files:**
- Create: `.claude/skills/<name>/SKILL.md` for all 18 skills, plus any extra files each skill dir contains

**Interfaces:**
- Consumes: `.kimi-code/skills/` (post-Task-2), Replacement Table.
- Produces: Claude-side skills, referenced by `.claude/agents/*.md` (Task 5) and `CLAUDE.md`.

- [ ] **Step 1: Copy the tree**

```bash
mkdir -p .claude/skills
cp -r .kimi-code/skills/* .claude/skills/
```

- [ ] **Step 2: Apply mechanical replacements (rules 1-13, 15-16)**

```bash
cd .claude/skills
grep -rl '' . | while read f; do
  sed -i \
    -e 's|~/\.kimi-code/skills|~/.claude/skills|g' \
    -e 's|~/\.kimi-code/plugins/managed/superpowers|superpowers plugin for Claude Code|g' \
    -e 's|~/\.kimi-code/|~/.claude/|g' \
    -e 's|kimi-code/kimi-for-coding-highspeed|haiku|g' \
    -e 's|kimi-code/kimi-for-coding|sonnet|g' \
    -e 's|kimi-code/kimi-k3|opus|g' \
    -e 's|kimi-code/k3-256k|opus|g' \
    -e 's|kimi-code/k3|opus|g' \
    -e 's|Kimi K2\.7|Claude Sonnet|g' \
    -e 's|Kimi K3|Claude Opus|g' \
    -e 's|K2\.7|Sonnet|g' \
    -e 's|K3|Opus|g' \
    -e 's|Kimi Code CLI|Claude Code|g' \
    -e 's|AgentSwarm|multiple Task tool calls in one message|g' \
    "$f"
done
```

- [ ] **Step 3: Handle fuzzy rule 14 and config-pool mentions manually**

Run: `grep -rn 'kimi' .claude/skills/ -i`
For each hit: eyeball and fix contextually (paths → `~/.claude`, product mentions → Claude Code, the `config.toml` secondary_model pool block → rewritten as `.claude/agents/*.md` `model` frontmatter enforcement, same wording as Task 3 Step 1).

- [ ] **Step 4: Verification gate (must be empty)**

Run: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/skills/`
Expected: zero hits. Any hit = go back to Step 3.

- [ ] **Step 5: Verify skill count parity**

Run: `ls -d .kimi-code/skills/*/ | wc -l && ls -d .claude/skills/*/ | wc -l`
Expected: 18 and 18.

- [ ] **Step 6: Commit (after user confirmation)**

---

### Task 5: Create `.claude/agents/` adapted copies

**Files:**
- Create: `.claude/agents/*.md` (17 files)

**Interfaces:**
- Consumes: `.kimi-code/agents/`, `.claude/skills/` paths from Task 4.
- Produces: agents loadable by Claude Code; frontmatter fields `name`, `description`, `model` consumed by Claude Code runtime.

- [ ] **Step 1: Copy and mechanically replace**

```bash
mkdir -p .claude/agents
cp .kimi-code/agents/*.md .claude/agents/
```

Apply the same sed script as Task 4 Step 2 against `.claude/agents/`.

- [ ] **Step 2: Rework frontmatter per file**

Current frontmatter shape:

```yaml
---
name: dotnet-coder
description: Senior .NET/C# engineer that writes and refactors production code
whenToUse: Creating or modifying C# code, services, endpoints, DTOs, tests
---
```

Claude Code shape (per file):

```yaml
---
name: dotnet-coder
description: Senior .NET/C# engineer that writes and refactors production code. Use when creating or modifying C# code, services, endpoints, DTOs, tests.
model: sonnet
---
```

Rules: merge `whenToUse` into `description` as a "Use when ..." sentence (Claude Code has no `whenToUse` field); set `model:` per Global Constraints mapping — `opus` for: `audit-orchestrator`, `coding-challenge-orchestrator`, `task-orchestrator`, `ai-code-detector`, `ai-obfuscator`, `ai-obfuscation-reviewer`; `sonnet` for everything else.

- [ ] **Step 3: Manual pass on remaining kimi hits**

Run: `grep -rni 'kimi\|k2\.7\|\bk3\b\|agentswarm\|config\.toml' .claude/agents/`
Fix each contextually (same rules as Task 4 Step 3). Expected after pass: zero hits.

- [ ] **Step 4: Verify count parity**

Run: `ls .kimi-code/agents/*.md | wc -l && ls .claude/agents/*.md | wc -l`
Expected: 17 and 17.

- [ ] **Step 5: Commit (after user confirmation)**

---

### Task 6: Mirror TASK/ into `.claude/TASK/`

**Files:**
- Create: `.claude/TASK/_TEMPLATE.md`

**Interfaces:**
- Consumes: `.kimi-code/TASK/_TEMPLATE.md`.
- Produces: verbatim mirror referenced by CLAUDE.md (`~/.claude/TASK/...`).

- [ ] **Step 1: Copy**

```bash
mkdir -p .claude/TASK
cp .kimi-code/TASK/_TEMPLATE.md .claude/TASK/_TEMPLATE.md
```

- [ ] **Step 2: Grep for tool-specific refs in template**

Run: `grep -ni 'kimi\|claude' .claude/TASK/_TEMPLATE.md`
Expected: zero hits (template is tool-agnostic). If a hit exists, generalize the wording.

- [ ] **Step 3: Commit (after user confirmation)**

---

### Task 7: Write SYNC-STRUCTURE.md

**Files:**
- Create: `SYNC-STRUCTURE.md` (root)

**Interfaces:**
- Consumes: final file lists from Tasks 1-6.
- Produces: the dual-edit contract referenced by AGENTS.md, CLAUDE.md, README.md.

- [ ] **Step 1: Write the file**

Content skeleton (fill the mapping table with the real 17+18 pairs — generate it with `ls` output, one row per pair):

```markdown
# Sync Structure Contract

This repo serves two runtimes: Kimi Code CLI (`.kimi-code/`) and Claude Code (`.claude/`).

## The Rule

**Edit one side — edit the other in the same commit.** Content differences between the sides
are allowed ONLY for tool-specific fragments (model names, tool-home paths, runtime features).

## Mapping

| Kimi side | Claude side | Notes |
|-----------|-------------|-------|
| `AGENTS.md` | `CLAUDE.md` | CLAUDE.md = adapted port |
| `.kimi-code/agents/<name>.md` | `.claude/agents/<name>.md` | Claude adds `model:` frontmatter |
| `.kimi-code/skills/<name>/` | `.claude/skills/<name>/` | full copy, tool refs swapped |
| `.kimi-code/TASK/` | `.claude/TASK/` | verbatim mirror |
| `.kimi-code/SOCIAL_DICTIONARY.md` | — | shared via AGENTS.md/CLAUDE.md reference to the Kimi copy? NO — see below |
| `.kimi-code/config.toml` | `~/.claude/settings.json` | tool-local, not synced by content |

## Translation table (Kimi → Claude)

[paste Replacement Table from the plan/spec]

## Pre-commit checklist

- [ ] Pairs in sync (diff both sides of any file you touched)?
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/` empty?
- [ ] `grep -rni 'claude\|sonnet\|opus\|haiku' .kimi-code/` empty?
```

Note: SOCIAL_DICTIONARY.md is referenced by both AGENTS.md and CLAUDE.md but lives once under `.kimi-code/`; the sync script copies it to both homes. State that explicitly in the file.

- [ ] **Step 2: Generate the real mapping rows**

Run: `ls .kimi-code/skills/` and `ls .kimi-code/agents/` — paste as table rows.

- [ ] **Step 3: Commit (after user confirmation)**

---

### Task 8: Write README.md with prerequisites

**Files:**
- Create: `README.md` (root)

**Interfaces:**
- Consumes: layout from Tasks 1-7.
- Produces: entry doc; superpowers prerequisite stated here.

- [ ] **Step 1: Write README.md**

Required sections (write full prose, English):

1. **What this is** — single source of truth for the agent crew across Kimi Code CLI and Claude Code.
2. **Prerequisites:**
   - Kimi Code CLI, with the `superpowers` plugin installed at `~/.kimi-code/plugins/managed/superpowers` (workflow skills — brainstorming, writing-plans, TDD, etc. — depend on it).
   - Claude Code; for workflow parity, the Superpowers plugin for Claude Code.
3. **Layout** — the tree from the spec (`.kimi-code/`, `.claude/`, CLAUDE.md, AGENTS.md, SYNC-STRUCTURE.md, sync scripts).
4. **Install / update** — `sync.ps1` (Windows) / `sync.sh` (*nix), dry-run flag, what gets copied where.
5. **Editing rule** — pointer to SYNC-STRUCTURE.md.

- [ ] **Step 2: Verify links resolve**

Run: `grep -o '\`[^\`]*\`' README.md | tr -d '\`' | while read p; do [ -e "$p" ] || echo "MISSING: $p"; done`
Expected: no MISSING lines (paths referenced in README exist in repo).

- [ ] **Step 3: Commit (after user confirmation)**

---

### Task 9: Write sync scripts

**Files:**
- Create: `sync.ps1`, `sync.sh` (root)

**Interfaces:**
- Consumes: final layout.
- Produces: `sync.ps1 [-DryRun]`, `sync.sh [--dry-run]` — idempotent deploy to `~/.kimi-code` and `~/.claude`.

- [ ] **Step 1: Write sync.ps1**

```powershell
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$kimiHome = Join-Path $HOME '.kimi-code'
$claudeHome = Join-Path $HOME '.claude'
$rc = if ($DryRun) { '/L' } else { '/MIR' }

function Mirror($src, $dst) {
    robocopy $src $dst /MIR $(if ($DryRun) { '/L' }) /NFL /NDL /NJH | Out-Null
}

Mirror (Join-Path $root '.kimi-code/agents')  (Join-Path $kimiHome 'agents')
Mirror (Join-Path $root '.kimi-code/skills')  (Join-Path $kimiHome 'skills')
Mirror (Join-Path $root '.kimi-code/TASK')    (Join-Path $kimiHome 'TASK')
Copy-Item (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $kimiHome -Force:$(-not $DryRun) -WhatIf:$DryRun
Copy-Item (Join-Path $root '.kimi-code/config.toml') $kimiHome -Force:$(-not $DryRun) -WhatIf:$DryRun

Mirror (Join-Path $root '.claude/agents') (Join-Path $claudeHome 'agents')
Mirror (Join-Path $root '.claude/skills') (Join-Path $claudeHome 'skills')
Mirror (Join-Path $root '.claude/TASK')   (Join-Path $claudeHome 'TASK')
Copy-Item (Join-Path $root 'CLAUDE.md') $claudeHome -Force:$(-not $DryRun) -WhatIf:$DryRun

if ($DryRun) { Write-Host 'Dry run complete — no changes made.' }
```

- [ ] **Step 2: Dry-run test**

Run: `powershell -File sync.ps1 -DryRun`
Expected: prints robocopy `/L` listings, exit code < 8, no filesystem changes (`~/.kimi-code` untouched).

- [ ] **Step 3: Write sync.sh**

```bash
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=""
[[ "${1:-}" == "--dry-run" ]] && DRY="echo"

$DRY cp -r "$ROOT/.kimi-code/agents" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.kimi-code/skills" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.kimi-code/TASK" "$HOME/.kimi-code/"
$DRY cp "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$ROOT/.kimi-code/config.toml" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.claude/agents" "$ROOT/.claude/skills" "$ROOT/.claude/TASK" "$HOME/.claude/"
$DRY cp "$ROOT/CLAUDE.md" "$HOME/.claude/CLAUDE.md"
```

`chmod +x sync.sh`.

- [ ] **Step 4: Dry-run test**

Run: `./sync.sh --dry-run`
Expected: echoes the cp commands, no changes.

- [ ] **Step 5: Commit (after user confirmation)**

---

### Task 10: Final cross-contamination gate

**Files:** none (verification only)

- [ ] **Step 1: Claude side clean**

Run: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/ CLAUDE.md`
Expected: zero hits.

- [ ] **Step 2: Kimi side clean**

Run: `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/`
Expected: zero hits (word `claude` alone may appear nowhere either — check).

- [ ] **Step 3: Structure check**

Run: `ls .kimi-code .claude`
Expected: `.kimi-code`: agents skills TASK SOCIAL_DICTIONARY.md config.toml; `.claude`: agents skills TASK.

- [ ] **Step 4: Update .gitignore if needed**

Check `.gitignore` — no entries should exclude the new dirs. No change expected.

- [ ] **Step 5: Final commit (after user confirmation)**

---

## Self-Review Notes

- Spec coverage: git mv (T1), self-refs (T2), CLAUDE.md+AGENTS.md (T3), .claude/skills (T4), .claude/agents (T5), TASK mirror (T6), SYNC-STRUCTURE.md (T7), README+superpowers prerequisite (T8), sync scripts (T9), verification gates (T10). All spec sections covered.
- Placeholder scan: SYNC-STRUCTURE.md mapping table is generated from live `ls` output at execution — intentional, command provided. No other placeholders.
- Type consistency: replacement strings identical between Global Constraints, Replacement Table, sed script, and grep gates.
