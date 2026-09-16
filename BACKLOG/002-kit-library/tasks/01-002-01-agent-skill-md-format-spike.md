---
feature: 002
task: 01
slug: kit-library
status: done
review: light
created: 2026-09-14
---

# 002/01 — Agent SKILL.md format spike

<goal>
A recorded verdict: do the SKILL.md frontmatter formats of Claude Code, Kimi Code, Codex, Gemini CLI and Qwen differ in ways that require per-platform normalization when one agnostic skill is materialized to multiple agents? The verdict and exact deltas feed task 05.
</goal>

<context>
Feature 002a imports skill folders and treats platform-unmarked skills as platform-agnostic: they materialize to EVERY known agent. That is only safe if all agents accept the same SKILL.md frontmatter; if formats differ, the materializer (task 05) must normalize per target platform. This spike produces the checklist task 05 works against. Executor reads ONLY this file.
</context>

<scope>
- Web research: official docs/specs of the skills format for Claude Code (Agent Skills), Kimi Code, OpenAI Codex, Gemini CLI, Qwen Code — frontmatter fields, required vs optional, file layout expectations
- Local inspection if agents/skills exist on this machine (`~/.claude/skills`, `~/.kimi-code/skills`, etc.) — read-only
- Read `src/Overmind.Infrastructure/Materialization/platforms.json` to see which platforms Overmind maps today
- Append a `<findings>` section to THIS task file: per-platform frontmatter table (field × platform, required/optional), concrete differences, and the verdict
- Append a one-line verdict to `<log>`
</scope>

<anti-scope>
- No production code changes anywhere (src/, tests/, web/)
- No changes to other task files, spec.md, plan.md, CONTEXT.md
- No new packages, no installs
</anti-scope>

<constraints>
- Cite a source URL for every claim about a platform's format in `<findings>`
- Verdict is binary and explicit: `pass-through` (no normalization needed, differences cosmetic/absent) or `normalize` (with an exact per-platform transform list)
</constraints>

<acceptance-criteria>
- [ ] `<findings>` section appended to this file with the field × platform matrix and source URLs
- [ ] Explicit verdict (`pass-through` or `normalize` + transform list) recorded in `<findings>` and `<log>`
- [ ] Zero changes outside this task file (`git status` shows only this file modified)
</acceptance-criteria>

<refs>
- @../spec.md — D3, R11 (reviewer reads, not executor)
- @../plan.md — task 01 row
- tasks/05-materializer-agnostic.md — consumes this spike's verdict
</refs>

<obstacles>
<!-- Заполняется исполнителями ПО ХОДУ работы -->
</obstacles>

<findings>
Researched 2026-09-14 from official docs (URLs inline) + local inspection. Local state: `~/.claude/skills` (23 skills), `~/.kimi-code/skills` (23), `~/.qwen/skills` (23) all present with byte-identical frontmatter (`name`, `description`, `type: prompt`, `whenToUse`); inter-platform diffs are body-only (model names Opus↔K3, absolute tool paths). `~/.codex`, `~/.gemini`, `~/.antigravity`, `~/.agents/skills` absent on this machine.

### Common core (portable, zero transforms)

All five platforms parse the same base unit: a directory `<name>/SKILL.md`, first line `---`, YAML frontmatter with `name` + `description`, then a Markdown body. Unknown frontmatter keys are ignored by every on-disk loader — no strict-schema rejection locally; Claude Code's six-field restriction applies only to claude.ai upload / Skills API / `package_skill.py` validation, not to `.claude/skills` loading (https://code.claude.com/docs/en/skills, "Using skill frontmatter outside Claude Code"). Resource layout (`scripts/`, `references/`, `assets/`) is identical across the open standard, Claude, Codex, Gemini, Qwen; Kimi recommends the same directory form (https://www.kimi.com/code/docs/en/kimi-code-cli/customization/skills.html).

### Field × platform matrix

Legend: **R** = required (skill fails to load without it), **r** = required by spec but locally lenient, **O** = supported optional, **o** = accepted but ignored/no-op, **—** = not documented.

| Field | Claude Code | Kimi Code | Codex | Gemini CLI | Qwen Code |
|---|---|---|---|---|---|
| `name` | R — spec: ≤64 chars, `[a-z0-9-]`, no lead/trail `-`, no `--`, = dir name; local loader lenient (dir name drives the command) | R in directory form (parse fails if missing; case-insensitive) | R — 64-char limit enforced (issue openai/codex#25042) | R — should match directory name | R — non-empty, must match `/^[\p{L}\p{N}_:.-]+$/u`; parse-time reject of whitespace/slashes/brackets |
| `description` | R per spec; locally "recommended", falls back to first non-empty body line; `description`+`when_to_use` truncated at 1,536 chars in listings | R in directory form; flat `.md` falls back to first body line (≤240 chars) | R — trigger for implicit invocation | R — "CRITICAL", the trigger mechanism | R — non-empty |
| `license` | O (spec; accepted, no-op) | o | o — undocumented, treat as ignored | o | o |
| `compatibility` | O (≤500 chars) | o | o — undocumented | o | o |
| `metadata` | O (string→string map) | o | o — undocumented | o | o |
| `allowed-tools` | O — honored as per-turn permission grant | o (not documented) | o — undocumented | o | o — Qwen's own docs spell it `allowedTools` (camelCase) |
| `when_to_use` | O (Claude extension, snake_case) | O — `whenToUse` + aliases `when-to-use`/`when_to_use` (alias covers Claude's spelling) | o | o | o |
| `disable-model-invocation` | O (kebab-case) | O — `disableModelInvocation` + kebab/snake aliases | — (inverse lives in sidecar: `allow_implicit_invocation` in `agents/openai.yaml`) | o | O — kebab-case, same spelling as Claude |
| `user-invocable` | O | o | o | o | O — same spelling as Claude |
| `arguments` | O (list; `$name` placeholders) | O (array or space-separated string; same `$name` placeholders) | o | o | o |
| `type` | o (ignored) | O — `prompt` (default) / `inline` / `flow`; **any other value = skill silently skipped** | o | o | o |
| `priority` | o | o | o | o | O (finite number; sorts `/skills` listing only) |
| `paths` | O (path-gated loading; field name confirmed in docs) | o | o | o | O (glob list gates model discovery) |
| `hooks` (in SKILL.md frontmatter) | — (hooks live in settings, not skill frontmatter) | — | — | — | O (PreToolUse etc., session-scoped; project skills only in trusted workspaces) |
| `context` / `agent` / `background` / `shell` | O (subagent execution extensions) | o | o | o | o |
| `source` | o | o | o | o | O (`learned` / `auto-skill` markers for auto-maintenance) |

Sources: Claude Code https://code.claude.com/docs/en/skills + open standard https://agentskills.io/specification · Kimi Code https://www.kimi.com/code/docs/en/kimi-code-cli/customization/skills.html (+ parser behavior https://github.com/MoonshotAI/kimi-code/pull/657) · Codex https://developers.openai.com/codex/skills/ + gap list https://www.skillsboard.sh/codex-skills + name-limit issue https://github.com/openai/codex/issues/25042 · Gemini CLI https://geminicli.com/docs/cli/creating-skills/ · Qwen Code https://qwenlm.github.io/qwen-code-docs/en/users/features/skills/

### Concrete behavioral deltas (the ones that bite)

1. **Kimi `type` skip rule** — `type` outside `prompt`/`inline`/`flow` makes Kimi skip the skill while every other platform loads it fine. Guard, not transform: agnostic skills omit `type` or keep `type: prompt`.
2. **Name-charset strictness ordering** — Claude/open spec is strictest (ASCII `[a-z0-9-]`, ≤64, = dir name); Qwen admits Unicode letters/digits plus `_ : . -`; Kimi is case-insensitive with no documented charset; Gemini treats dir-name match as recommendation; Codex enforces the 64-char cap. Any name valid for Claude is valid for all five.
3. **`description` limits** — spec ≤1024; Kimi flat-form fallback 240 (directory form has no documented cap); Claude listing truncates `description`+`when_to_use` at 1,536; Codex shortens descriptions when the startup list exceeds 2% of context window / 8,000 chars. Keep ≤1024 — everything beyond is cosmetic truncation.
4. **Invocation-control convergence** — `disable-model-invocation` and `user-invocable` are spelled identically in Claude Code and Qwen; Kimi honors the same fields via camelCase + aliases; Codex keeps the inverse flag in the `agents/openai.yaml` sidecar (`policy.allow_implicit_invocation`); Gemini has neither. Kebab-case usage behaves on Claude+Qwen+Kimi and is inert (ignored, not rejected) on Codex+Gemini.
5. **Codex extras never live in SKILL.md** — display metadata, invocation policy, MCP tool dependencies go to `agents/openai.yaml` beside SKILL.md, not into frontmatter (https://developers.openai.com/codex/skills/).
6. **Body placeholders are not fully portable** — `$ARGUMENTS`/`$0`/`$1`/`$name` are shared by Claude Code and Kimi; skill-dir env vars differ per platform (`${CLAUDE_SKILL_DIR}` vs `${KIMI_SKILL_DIR}` vs `$QWEN_SKILL_ROOT`). A body referencing its own directory is not byte-portable — kit skills should avoid env-var-specific paths. (Task-05 concern, outside the frontmatter verdict.)

### platforms.json today (read per scope)

Current map: `claude`→`.claude/skills`, `kimiCode`→`.kimi-code/skills`, `codex`→`.codex/skills`, `gemini`→`.gemini/skills`, `antigravity`→`.antigravity/skills`.

- **qwen is missing** — add `qwen: { "skillsDir": ".qwen/skills" }` (personal tier; project tier `.qwen/skills/`), https://qwenlm.github.io/qwen-code-docs/en/users/features/skills/.
- **codex path needs verification** — current official Codex docs list `$CWD/.agents/skills` (walk to repo root), `$HOME/.agents/skills`, `/etc/codex/skills`, bundled; `~/.codex/skills` appears only in community guides / older tables (e.g. https://juejin.cn/post/7599543345980555291, https://gitee.com/ai_37/superpowers/blob/main/docs/README.codex.md). Task 05 must confirm against the Codex version Overmind targets before materializing there.
- `antigravity` is mapped but outside this spike's five platforms — needs its own format check before materializing.
- Kimi and Gemini additionally read `~/.agents/skills` (shared alias) — a possible single-materialization shortcut, but per-platform dirs are the documented primary.

### Verdict: `pass-through`

One agnostic SKILL.md (opening `---` on line 1, `name` + `description` frontmatter, Markdown body, `<name>/SKILL.md` layout) materializes **byte-identical** to all five targets; no per-platform transform list is required. Observed deltas are (a) additive optional fields ignored off-platform and (b) narrower validation on some platforms — both are authoring/import constraints, not materialization transforms.

Binding import-time gates for the kit library (keep task 05 a pure copy):

- **G1**: canonical `name` — ≤64 chars, `[a-z0-9-]` only, no leading/trailing hyphen, no `--`, equals directory name (open-spec rule; simultaneously satisfies Qwen's regex, Codex's 64-limit, Kimi's presence check, Gemini's convention).
- **G2**: `description` — non-empty, ≤1024 chars.
- **G3**: `type` omitted or `prompt` (Kimi skip rule). Current kit skills already carry `type: prompt` — passes as-is.
- **G4**: frontmatter opens on file line 1; valid YAML, spaces not tabs.
- **G5**: agnostic skills must not *depend* on platform-native fields (`allowed-tools`, `when_to_use`, `arguments`, `paths`, `priority`, `hooks`) — off-platform they are inert (ignored, never rejected) by every loader.

Optional per-target injection for task 05 (never required; recorded to avoid re-research):

- → Kimi: `whenToUse:`, `arguments:` (aliases already cover Claude spellings).
- → Qwen: `priority`, `paths`, `hooks`, `source` (+ kebab `disable-model-invocation`/`user-invocable` shared with Claude).
- → Claude: `when_to_use`, `context`/`agent`/`background`/`shell`, `disallowed-tools`.
- → Codex: `agents/openai.yaml` sidecar (display / policy / dependencies).
- → Gemini: nothing beyond the common core.

Empirical confirmation: the same 23 skills are installed under `~/.claude/skills`, `~/.kimi-code/skills` and `~/.qwen/skills` with byte-identical frontmatter (body text differs per platform only) — the intersection format demonstrably loads on 3/5 targets today; Codex and Gemini accept the same open-standard file per their docs.
</findings>

<log>
<!-- Одна строка на запись: YYYY-MM-DD — кто — что сделано. -->
2026-09-14 — sub-agent (K2.7) — spike complete: 5 platforms researched (official docs + local inspection), verdict PASS-THROUGH with import gates G1–G5; <findings> appended, ready for review.
</log>
