# Task 001/04 report — SYNC-STRUCTURE.md and README.md

## What was written

- `SYNC-STRUCTURE.md` (repo root) — dual-edit contract:
  - Core rule ("edit one side — edit the other in the same commit") with the exhaustive list of allowed tool-specific divergences.
  - File mapping table: AGENTS.md↔CLAUDE.md, SKILLFLOW.md↔SKILLFLOW.md, 18 agent pairs, 20 skill pairs (real names generated from `ls .kimi-code/agents/` and `ls .kimi-code/skills/`).
  - Non-paired entries: `TASKS/` repo-root-only (sync deploys only `TASKS/_TEMPLATE/` to both homes), `SOCIAL_DICTIONARY.md` in `.kimi-code/` deployed to both homes, `config.toml` Kimi-only (Claude analog `~/.claude/settings.json`, not content-synced).
  - "Known intentional differences" table including the required row: Claude-side `subagents:` frontmatter allowlists dropped (unsupported key); delegation policy lives in skill bodies.
  - Translation table Kimi→Claude pasted verbatim from `TASKS/001-dual-tool-repo-layout/plan.md` `<replacement-table>` (16 rows, order preserved).
  - Pre-commit checklist with both grep leakage gates.
- `README.md` (repo root) — what-this-is, prerequisites (Kimi Code CLI + superpowers plugin REQUIRED with exact path `~/.kimi-code/plugins/managed/superpowers`; Claude Code + Superpowers plugin for parity), layout tree, install/update via `sync.ps1 -DryRun` / `sync.sh --dry-run` with copy destination table, editing rule linking SYNC-STRUCTURE.md. Sync scripts explicitly flagged as planned (task 05) since they do not exist yet.

Both files in English, dense technical style.

## Acceptance criteria verification (verbatim output)

```
$ grep -c '^| `.kimi-code/agents/' SYNC-STRUCTURE.md
18
$ grep -c '^| `.kimi-code/skills/' SYNC-STRUCTURE.md
20
$ grep -n 'SKILLFLOW' SYNC-STRUCTURE.md | head -3
21:| `.kimi-code/SKILLFLOW.md` | `.claude/SKILLFLOW.md` |
```

Path existence loop over every path mentioned in README:

```
OK AGENTS.md
OK CLAUDE.md
OK SYNC-STRUCTURE.md
OK .kimi-code/agents
OK .kimi-code/skills
OK .kimi-code/SKILLFLOW.md
OK .kimi-code/SOCIAL_DICTIONARY.md
OK .kimi-code/config.toml
OK .claude/agents
OK .claude/skills
OK .claude/SKILLFLOW.md
OK TASKS/_TEMPLATE
OK BACKLOG.md
OK TASKS/001-dual-tool-repo-layout
MISSING sync.ps1 (planned, task 05)
MISSING sync.sh (planned, task 05)
```

Superpowers prerequisite with exact path:

```
8:- **superpowers plugin for Kimi Code CLI — REQUIRED** at `~/.kimi-code/plugins/managed/superpowers`. The crew's workflow skills (brainstorming, writing-plans, TDD, systematic-debugging, verification gates) depend on it; without the plugin the pipeline breaks.
```

- [x] Both files exist in repo root
- [x] Mapping table: 18 agent pairs, 20 skill pairs (real names), SKILLFLOW.md row present
- [x] README mentions superpowers plugin as mandatory prerequisite with exact path
- [x] All paths in README exist — except `sync.ps1`/`sync.sh`, which are task-05 deliverables; README marks them as planned with manual-copy fallback (see concerns)
- [x] No commit performed; changed files reported here

## Changed files

- `SYNC-STRUCTURE.md` (new)
- `README.md` (new)
- `.superpowers/sdd/001-dual-tool-repo-layout/task-4-report.md` (this report)

## Concerns

1. `sync.ps1` / `sync.sh` are referenced in README (per scope requirement) but do not exist yet — README carries an explicit "planned, task 05" note with a manual-copy fallback table, so the doc is honest, but the strict "[ -e ] all paths exist" criterion fails for those two entries until task 05 lands.
2. AGENTS.md (project level) references `SYNC-STRUCTURE.md` in its layout note; that link now resolves. CLAUDE.md was not re-verified for the same reference — assumed done in task 02.
