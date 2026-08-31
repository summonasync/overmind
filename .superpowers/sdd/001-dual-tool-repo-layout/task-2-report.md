# Task 02 Report — Port AGENTS.md to CLAUDE.md

## What was done

1. **`CLAUDE.md` created** (repo root) — full port of the updated `AGENTS.md` with all
   tool-specific swaps per plan.md `<replacement-table>` / `<model-mapping>` and the task
   brief's `<constraints>`:
   - Sub-Agent Rules rewritten: allowed models `sonnet` (default coder/reviewer) / `opus`
     (orchestration, audit, `ai-code-detector`, `ai-obfuscator`, `ai-obfuscation-reviewer`).
   - Enforcement block: TOML pool snippet replaced with the mandated sentence about the
     `model:` field in `.claude/agents/*.md` frontmatter.
   - Documentation Files table: `TASKS/nnn-slug/` row untouched; `SOCIAL_DICTIONARY.md`
     row points to `~/.claude/SOCIAL_DICTIONARY.md`; self-reference row is `CLAUDE.md`;
     `SKILLFLOW.md` row kept as-is (deployed copy will be `~/.claude/SKILLFLOW.md`).
   - Superpowers Integration adapted to "superpowers plugin for Claude Code"; rule 5
     pool wording swapped to `sonnet`/`opus`.
   - No semantic changes to tone/slang/stack/frontend/workflow sections.
   - Layout note included but WITHOUT naming the Kimi mirror (see Concerns).

2. **`AGENTS.md` updated** (repo root):
   - Layout note added directly under `# Global Agent Context`, verbatim per the brief:
     > Repo layout: this file's crew lives in `.kimi-code/` (agents, skills, dictionary, config). Claude Code mirror lives in `.claude/` + `CLAUDE.md`. TASKS/ stays in repo root (feature tracking). Dual-edit rule: see `SYNC-STRUCTURE.md`.
   - Documentation Files table: `SOCIAL_DICTIONARY.md` row → `~/.kimi-code/SOCIAL_DICTIONARY.md`.
   - Nothing else touched.

3. **No git commit performed** — stopping and reporting per workflow rule 6.

## Acceptance-criteria checks (verbatim output)

### 1. CLAUDE.md contains all AGENTS.md sections

```
$ grep -c '^## ' CLAUDE.md AGENTS.md
CLAUDE.md:15
AGENTS.md:15
```

Both files have 15 `##` sections (Identity, Communication Style, .NET Stack, Code Style,
Product & UX, Frontend Stack, Build & Run, AI/ML Context, Documentation Files,
Sub-Agent Rules, Deterministic Enforcement, Superpowers Integration, AI Coding Traces,
Coding Challenges, Development Workflow). PASS.

### 2. Kimi-side grep gate on CLAUDE.md

```
$ grep -in 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' CLAUDE.md; echo "exit=$?"
exit=1
```

Empty (grep exit 1 = no matches). PASS — see Concerns for what this cost.

### 3. Claude-side grep gate on AGENTS.md

```
$ grep -in 'claude\|sonnet\|opus\|haiku' AGENTS.md
3:> Repo layout: this file's crew lives in `.kimi-code/` (agents, skills, dictionary, config). Claude Code mirror lives in `.claude/` + `CLAUDE.md`. TASKS/ stays in repo root (feature tracking). Dual-edit rule: see `SYNC-STRUCTURE.md`.
```

Only hit is the new layout note (intentional). PASS.

### 4. Diff CLAUDE.md vs AGENTS.md — tool-specific only

Changed hunks (full list, nothing else differs):
- Line 3: layout note (mirrored; Kimi mention dropped on the Claude side).
- `## Product & UX Principles` / `## Build & Run Commands` headers: `AGENTS.md` → `CLAUDE.md` self-reference.
- Doc table: `AGENTS.md` row → `CLAUDE.md` row; `~/.kimi-code/SOCIAL_DICTIONARY.md` → `~/.claude/SOCIAL_DICTIONARY.md`.
- Sub-Agent Rules section: full model-mapping rewrite (Kimi K2.7/K3 → sonnet/opus, `primary` alias → "main agent's own model", obfuscation-reviewer added to opus list per brief).
- Enforcement block: 11-line TOML/experimental-flag block → single mandated frontmatter sentence.
- Superpowers intro line: `The \`superpowers\` plugin is installed.` → `The superpowers plugin for Claude Code is installed.`
- Superpowers rule 5: pool wording → `` `sonnet`/`opus` only per role mapping ``.

PASS — no tone/stack/workflow semantics changed.

### 5. Changed files reported, no commit

```
$ git status --short (relevant lines)
 M AGENTS.md
?? CLAUDE.md
?? .superpowers/
```

(All other status lines belong to task 01.) No commit run. PASS.

## Concerns

1. **CLAUDE.md layout note is asymmetric.** The brief's criterion "grep CLAUDE.md for
   kimi … — пусто" conflicts with a layout note that names the `.kimi-code/` mirror.
   The grep gate won: CLAUDE.md's note says only "this file's crew lives in `.claude/`"
   and defers to `SYNC-STRUCTURE.md` (task 04) for the dual-edit rule. If the orchestrator
   prefers the symmetric wording, the grep criterion must be relaxed to "hits only in
   the layout note", matching the AGENTS.md criterion.
2. **`SOCIAL_DICTIONARY.md` bare references** in the "Slang Dictionary (Agent Usage)"
   section body were left bare in both files — the brief only mandates the doc-table
   path updates. Deployed Claude agents will find the dictionary via the table row.
3. `SYNC-STRUCTURE.md` does not exist yet (task 04) — both layout notes reference it;
   expected at this stage.
