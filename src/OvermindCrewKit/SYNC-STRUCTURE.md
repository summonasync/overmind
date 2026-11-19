# SYNC-STRUCTURE — Quadruple-Edit Contract

This repo maintains one agent crew for four runtimes: **Kimi Code CLI** (`.kimi-code/` incl. `AGENTS.md`), **Claude Code** (`.claude/` incl. `CLAUDE.md`), **Qwen Code** (`.qwen/` incl. `QWEN.md`, deployed to `~/.qwen`), and **OpenAI Codex CLI** (`.codex/` incl. `AGENTS.md`, deployed to `~/.codex`). All four sides must stay semantically identical.

## Core rule

**Edit one side — edit the other three in the same commit.** No asymmetric changes to crew behavior, policies, workflows, or skill logic. Divergence is allowed ONLY for tool-specific concerns:

- model names, aliases, and selection mechanics (Kimi `K2.7`/`K3` pool vs Claude `sonnet`/`opus` vs Qwen `qwen3.8-flash` pin vs Codex `gpt-6-luna`/`gpt-6.1-sol` tiers)
- home directory paths (`~/.kimi-code/` vs `~/.claude/` vs `~/.qwen/` vs `~/.codex/`)
- tool identifier names inside agent `tools:` allowlists (Claude Code `Read`/`Grep`/`Glob`/`Bash`/`WebSearch`/`FetchURL` vs canonical Qwen `read_file`/`grep_search`/`glob`/`run_shell_command`/`web_fetch`) — the *restriction* is mirrored, only the spelling follows the runtime; Codex agent roles are TOML and carry no allowlist at all — there is no `tools` field there and no per-role restriction text either (measured 2026-11-19: zero `mcp__` tokens across the 46 role files), so the restriction lives in `.codex/AGENTS.md` and the skill bodies
- agent file format (Codex agent roles are standalone TOML files with exactly `name`, `description`, `model`, `developer_instructions` — strict deserialization: any other field drops the role, and the drop is not silent: the loader warns `Ignoring malformed agent role definition` on stderr at session start (measured codex-cli 0.160.1, 2026-11-19; a `tools` key additionally collides with Codex's tool-configuration namespace, so per-role restriction cannot live in the role container — see KIT-MEMBERSHIP "Codex per-role tool restriction"))
- runtime-specific features (Kimi `AgentSwarm` + config.toml secondary-model pool; Claude Task tool + settings.json; Qwen `agent` tool + `subagent_type: "fork"` + settings.json `permissions`/`hooks`; Codex `spawn_agent` tool + TOML role pins + 4 concurrency slots, full-history forks inherit the parent model)
- enforcement mechanics (Kimi hooks/config, Claude prompt-enforced, Qwen `permissions.deny`/`ask` + `PostToolUse` hooks in `~/.qwen/settings.json`, Codex behavioral/prompt-only by crew decision — codex hooks exist in the runtime but the kit ships none and never writes `~/.codex/config.toml`)
- Overmind MCP is the planning backend on all four sides — identical tool names (`mcp__overmind__*`), hard requirement everywhere

Everything else — rules, workflow phases, review gates, delegation policy, conventions — is mirrored verbatim.

## Frontmatter convention (all four sides)

A single-line `description:` / `whenToUse:` value that contains `: ` (colon + space) is **invalid YAML** — the value parses as a nested mapping and strict parsers reject the whole frontmatter block. Qwen Code then falls back to a naive line parser and logs a `YAML_PARSER` warning on every session start.

**Rule: wrap such a value in double quotes.** Double quotes, not single quotes and not a `>-`/`|-` block scalar:

- double quotes are valid YAML *and* are correctly unquoted by naive fallback parsers (Qwen's `parseValue` strips `"` and unescapes `\"`/`\\`); single quotes are not handled there
- a block scalar splits the value over two lines, which line-oriented frontmatter parsers in other runtimes may mishandle
- keep it one line so the diff stays one line per file

Escaping: `"` → `\"`, and never write a bare `\` inside a double-quoted value (`C:\foo` becomes an escape sequence) — switch that value to a `>-` block scalar instead. Multi-line `description: |` block scalars (used by all crew skills) are already valid and need no quotes.

Applies to every markdown frontmatter (SKILL.md on all four sides, agent files on the three md-based sides). Codex agent roles are TOML — not covered by this YAML rule; their one-line `description` is a TOML basic string and needs `"` escaped as `\"` instead. As of this writing the affected set is 16 agents — `architect-review` (`review: hard` in backticks), `dotnet-architect`, `dotnet-code-reviewer`, `dotnet-init`, `feature-orchestrator`, `frontend-designer`, `go-code-reviewer`, `js-platform-code-reviewer`, `node-architect`, `node-code-reviewer`, `overmind-ceremony`, `python-code-reviewer`, `solution-architect`, `sprint-planner`, `web-architect`, `web-code-reviewer` — quoted in the `description:` field on the Claude/Qwen sides and in `whenToUse:` for `architect-review` on the Kimi side, where the description is not folded.

## File mapping

| Kimi side | Claude side | Qwen side |
|-----------|-------------|-----------|
| `.kimi-code/AGENTS.md` | `.claude/CLAUDE.md` | `.qwen/QWEN.md` |
| `.kimi-code/SKILLFLOW.md` | `.claude/SKILLFLOW.md` | `.qwen/SKILLFLOW.md` |
| `.kimi-code/agents/ai-code-detector.md` | `.claude/agents/ai-code-detector.md` | `.qwen/agents/ai-code-detector.md` |
| `.kimi-code/agents/ai-obfuscation-reviewer.md` | `.claude/agents/ai-obfuscation-reviewer.md` | `.qwen/agents/ai-obfuscation-reviewer.md` |
| `.kimi-code/agents/ai-obfuscator.md` | `.claude/agents/ai-obfuscator.md` | `.qwen/agents/ai-obfuscator.md` |
| `.kimi-code/agents/architect-review.md` | `.claude/agents/architect-review.md` | `.qwen/agents/architect-review.md` |
| `.kimi-code/agents/audit-orchestrator.md` | `.claude/agents/audit-orchestrator.md` | `.qwen/agents/audit-orchestrator.md` |
| `.kimi-code/agents/coding-challenge-orchestrator.md` | `.claude/agents/coding-challenge-orchestrator.md` | `.qwen/agents/coding-challenge-orchestrator.md` |
| `.kimi-code/agents/documentation-writer.md` | `.claude/agents/documentation-writer.md` | `.qwen/agents/documentation-writer.md` |
| `.kimi-code/agents/dotnet-architect.md` | `.claude/agents/dotnet-architect.md` | `.qwen/agents/dotnet-architect.md` |
| `.kimi-code/agents/dotnet-code-reviewer.md` | `.claude/agents/dotnet-code-reviewer.md` | `.qwen/agents/dotnet-code-reviewer.md` |
| `.kimi-code/agents/dotnet-coder.md` | `.claude/agents/dotnet-coder.md` | `.qwen/agents/dotnet-coder.md` |
| `.kimi-code/agents/dotnet-init.md` | `.claude/agents/dotnet-init.md` | `.qwen/agents/dotnet-init.md` |
| `.kimi-code/agents/fast-fixer-coder.md` | `.claude/agents/fast-fixer-coder.md` | `.qwen/agents/fast-fixer-coder.md` |
| `.kimi-code/agents/frontend-designer.md` | `.claude/agents/frontend-designer.md` | `.qwen/agents/frontend-designer.md` |
| `.kimi-code/agents/go-code-reviewer.md` | `.claude/agents/go-code-reviewer.md` | `.qwen/agents/go-code-reviewer.md` |
| `.kimi-code/agents/go-coder.md` | `.claude/agents/go-coder.md` | `.qwen/agents/go-coder.md` |
| `.kimi-code/agents/maui-coder.md` | `.claude/agents/maui-coder.md` | `.qwen/agents/maui-coder.md` |
| `.kimi-code/agents/naming-sensei.md` | `.claude/agents/naming-sensei.md` | `.qwen/agents/naming-sensei.md` |
| `.kimi-code/agents/js-platform-code-reviewer.md` | `.claude/agents/js-platform-code-reviewer.md` | `.qwen/agents/js-platform-code-reviewer.md` |
| `.kimi-code/agents/js-platform-coder.md` | `.claude/agents/js-platform-coder.md` | `.qwen/agents/js-platform-coder.md` |
| `.kimi-code/agents/node-architect.md` | `.claude/agents/node-architect.md` | `.qwen/agents/node-architect.md` |
| `.kimi-code/agents/node-code-reviewer.md` | `.claude/agents/node-code-reviewer.md` | `.qwen/agents/node-code-reviewer.md` |
| `.kimi-code/agents/node-coder.md` | `.claude/agents/node-coder.md` | `.qwen/agents/node-coder.md` |
| `.kimi-code/agents/python-code-reviewer.md` | `.claude/agents/python-code-reviewer.md` | `.qwen/agents/python-code-reviewer.md` |
| `.kimi-code/agents/python-coder.md` | `.claude/agents/python-coder.md` | `.qwen/agents/python-coder.md` |
| `.kimi-code/agents/rust-architect.md` | `.claude/agents/rust-architect.md` | `.qwen/agents/rust-architect.md` |
| `.kimi-code/agents/rust-code-reviewer.md` | `.claude/agents/rust-code-reviewer.md` | `.qwen/agents/rust-code-reviewer.md` |
| `.kimi-code/agents/rust-coder.md` | `.claude/agents/rust-coder.md` | `.qwen/agents/rust-coder.md` |
| `.kimi-code/agents/rust-init.md` | `.claude/agents/rust-init.md` | `.qwen/agents/rust-init.md` |
| `.kimi-code/agents/solution-architect.md` | `.claude/agents/solution-architect.md` | `.qwen/agents/solution-architect.md` |
| `.kimi-code/agents/web-architect.md` | `.claude/agents/web-architect.md` | `.qwen/agents/web-architect.md` |
| `.kimi-code/agents/web-code-reviewer.md` | `.claude/agents/web-code-reviewer.md` | `.qwen/agents/web-code-reviewer.md` |
| `.kimi-code/agents/web-coder.md` | `.claude/agents/web-coder.md` | `.qwen/agents/web-coder.md` |
| `.kimi-code/agents/overmind-ceremony.md` | `.claude/agents/overmind-ceremony.md` | `.qwen/agents/overmind-ceremony.md` |
| `.kimi-code/agents/pipeline-doctor.md` | `.claude/agents/pipeline-doctor.md` | `.qwen/agents/pipeline-doctor.md` |
| `.kimi-code/agents/feature-orchestrator.md` | `.claude/agents/feature-orchestrator.md` | `.qwen/agents/feature-orchestrator.md` |
| `.kimi-code/agents/sprint-planner.md` | `.claude/agents/sprint-planner.md` | `.qwen/agents/sprint-planner.md` |
| `.kimi-code/agents/wave-marshal.md` | `.claude/agents/wave-marshal.md` | `.qwen/agents/wave-marshal.md` |
| `.kimi-code/agents/tutor-master.md` | `.claude/agents/tutor-master.md` | `.qwen/agents/tutor-master.md` |
| `.kimi-code/skills/ai-code-detector/` | `.claude/skills/ai-code-detector/` | `.qwen/skills/ai-code-detector/` |
| `.kimi-code/skills/ai-obfuscation-reviewer/` | `.claude/skills/ai-obfuscation-reviewer/` | `.qwen/skills/ai-obfuscation-reviewer/` |
| `.kimi-code/skills/ai-obfuscator/` | `.claude/skills/ai-obfuscator/` | `.qwen/skills/ai-obfuscator/` |
| `.kimi-code/skills/architect-review/` | `.claude/skills/architect-review/` | `.qwen/skills/architect-review/` |
| `.kimi-code/skills/audit-orchestrator/` | `.claude/skills/audit-orchestrator/` | `.qwen/skills/audit-orchestrator/` |
| `.kimi-code/skills/coding-challenge-orchestrator/` | `.claude/skills/coding-challenge-orchestrator/` | `.qwen/skills/coding-challenge-orchestrator/` |
| `.kimi-code/skills/documentation-writer/` | `.claude/skills/documentation-writer/` | `.qwen/skills/documentation-writer/` |
| `.kimi-code/skills/dotnet-architect/` | `.claude/skills/dotnet-architect/` | `.qwen/skills/dotnet-architect/` |
| `.kimi-code/skills/dotnet-best-practices/` | `.claude/skills/dotnet-best-practices/` | `.qwen/skills/dotnet-best-practices/` |
| `.kimi-code/skills/dotnet-code-reviewer/` | `.claude/skills/dotnet-code-reviewer/` | `.qwen/skills/dotnet-code-reviewer/` |
| `.kimi-code/skills/dotnet-coder/` | `.claude/skills/dotnet-coder/` | `.qwen/skills/dotnet-coder/` |
| `.kimi-code/skills/dotnet-init/` | `.claude/skills/dotnet-init/` | `.qwen/skills/dotnet-init/` |
| `.kimi-code/skills/fast-fixer-coder/` | `.claude/skills/fast-fixer-coder/` | `.qwen/skills/fast-fixer-coder/` |
| `.kimi-code/skills/frontend-designer/` | `.claude/skills/frontend-designer/` | `.qwen/skills/frontend-designer/` |
| `.kimi-code/skills/go-best-practices/` | `.claude/skills/go-best-practices/` | `.qwen/skills/go-best-practices/` |
| `.kimi-code/skills/go-code-reviewer/` | `.claude/skills/go-code-reviewer/` | `.qwen/skills/go-code-reviewer/` |
| `.kimi-code/skills/go-coder/` | `.claude/skills/go-coder/` | `.qwen/skills/go-coder/` |
| `.kimi-code/skills/maui-coder/` | `.claude/skills/maui-coder/` | `.qwen/skills/maui-coder/` |
| `.kimi-code/skills/naming-sensei/` | `.claude/skills/naming-sensei/` | `.qwen/skills/naming-sensei/` |
| `.kimi-code/skills/js-platform-best-practices/` | `.claude/skills/js-platform-best-practices/` | `.qwen/skills/js-platform-best-practices/` |
| `.kimi-code/skills/js-platform-code-reviewer/` | `.claude/skills/js-platform-code-reviewer/` | `.qwen/skills/js-platform-code-reviewer/` |
| `.kimi-code/skills/js-platform-coder/` | `.claude/skills/js-platform-coder/` | `.qwen/skills/js-platform-coder/` |
| `.kimi-code/skills/node-architect/` | `.claude/skills/node-architect/` | `.qwen/skills/node-architect/` |
| `.kimi-code/skills/node-best-practices/` | `.claude/skills/node-best-practices/` | `.qwen/skills/node-best-practices/` |
| `.kimi-code/skills/node-code-reviewer/` | `.claude/skills/node-code-reviewer/` | `.qwen/skills/node-code-reviewer/` |
| `.kimi-code/skills/node-coder/` | `.claude/skills/node-coder/` | `.qwen/skills/node-coder/` |
| `.kimi-code/skills/python-best-practices/` | `.claude/skills/python-best-practices/` | `.qwen/skills/python-best-practices/` |
| `.kimi-code/skills/python-code-reviewer/` | `.claude/skills/python-code-reviewer/` | `.qwen/skills/python-code-reviewer/` |
| `.kimi-code/skills/python-coder/` | `.claude/skills/python-coder/` | `.qwen/skills/python-coder/` |
| `.kimi-code/skills/rust-architect/` | `.claude/skills/rust-architect/` | `.qwen/skills/rust-architect/` |
| `.kimi-code/skills/rust-best-practices/` | `.claude/skills/rust-best-practices/` | `.qwen/skills/rust-best-practices/` |
| `.kimi-code/skills/rust-code-reviewer/` | `.claude/skills/rust-code-reviewer/` | `.qwen/skills/rust-code-reviewer/` |
| `.kimi-code/skills/rust-coder/` | `.claude/skills/rust-coder/` | `.qwen/skills/rust-coder/` |
| `.kimi-code/skills/rust-init/` | `.claude/skills/rust-init/` | `.qwen/skills/rust-init/` |
| `.kimi-code/skills/cargo-licensing-guard/` | `.claude/skills/cargo-licensing-guard/` | `.qwen/skills/cargo-licensing-guard/` |
| `.kimi-code/skills/solution-architect/` | `.claude/skills/solution-architect/` | `.qwen/skills/solution-architect/` |
| `.kimi-code/skills/web-architect/` | `.claude/skills/web-architect/` | `.qwen/skills/web-architect/` |
| `.kimi-code/skills/web-best-practices/` | `.claude/skills/web-best-practices/` | `.qwen/skills/web-best-practices/` |
| `.kimi-code/skills/web-code-reviewer/` | `.claude/skills/web-code-reviewer/` | `.qwen/skills/web-code-reviewer/` |
| `.kimi-code/skills/web-coder/` | `.claude/skills/web-coder/` | `.qwen/skills/web-coder/` |
| `.kimi-code/skills/nuget-licensing-guard/` | `.claude/skills/nuget-licensing-guard/` | `.qwen/skills/nuget-licensing-guard/` |
| `.kimi-code/skills/overmind-ceremony/` | `.claude/skills/overmind-ceremony/` | `.qwen/skills/overmind-ceremony/` |
| `.kimi-code/skills/pipeline-doctor/` | `.claude/skills/pipeline-doctor/` | `.qwen/skills/pipeline-doctor/` |
| `.kimi-code/skills/squeezr-ops/` | `.claude/skills/squeezr-ops/` | `.qwen/skills/squeezr-ops/` |
| `.kimi-code/skills/feature-orchestrator/` | `.claude/skills/feature-orchestrator/` | `.qwen/skills/feature-orchestrator/` |
| `.kimi-code/skills/sprint-planner/` | `.claude/skills/sprint-planner/` | `.qwen/skills/sprint-planner/` |
| `.kimi-code/skills/wave-marshal/` | `.claude/skills/wave-marshal/` | `.qwen/skills/wave-marshal/` |
| `.kimi-code/skills/tutor-master/` | `.claude/skills/tutor-master/` | `.qwen/skills/tutor-master/` |

## Codex side mapping

The Codex side (`.codex/`) mirrors every row of the file-mapping table above with the same names:

| Kimi side | Codex side |
|-----------|------------|
| `.kimi-code/AGENTS.md` | `.codex/AGENTS.md` |
| `.kimi-code/SKILLFLOW.md` | `.codex/SKILLFLOW.md` |
| `.kimi-code/agents/<name>.md` | `.codex/agents/<name>.toml` |
| `.kimi-code/agents/augur-orchestrator.md` | `.claude/agents/augur-orchestrator.md` | `.qwen/agents/augur-orchestrator.md` |
| `.kimi-code/agents/augur-researcher.md` | `.claude/agents/augur-researcher.md` | `.qwen/agents/augur-researcher.md` |
| `.kimi-code/agents/augur-architect.md` | `.claude/agents/augur-architect.md` | `.qwen/agents/augur-architect.md` |
| `.kimi-code/agents/augur-estimator.md` | `.claude/agents/augur-estimator.md` | `.qwen/agents/augur-estimator.md` |
| `.kimi-code/agents/augur-challenger.md` | `.claude/agents/augur-challenger.md` | `.qwen/agents/augur-challenger.md` |
| `.kimi-code/agents/umbraco-sensei.md` | `.claude/agents/umbraco-sensei.md` | `.qwen/agents/umbraco-sensei.md` |
| `.kimi-code/skills/<name>/` | `.codex/skills/<name>/` |
| `.kimi-code/skills/augur-orchestrator/` | `.claude/skills/augur-orchestrator/` | `.qwen/skills/augur-orchestrator/` |
| `.kimi-code/skills/augur-researcher/` | `.claude/skills/augur-researcher/` | `.qwen/skills/augur-researcher/` |
| `.kimi-code/skills/augur-architect/` | `.claude/skills/augur-architect/` | `.qwen/skills/augur-architect/` |
| `.kimi-code/skills/augur-estimator/` | `.claude/skills/augur-estimator/` | `.qwen/skills/augur-estimator/` |
| `.kimi-code/skills/augur-challenger/` | `.claude/skills/augur-challenger/` | `.qwen/skills/augur-challenger/` |
| `.kimi-code/skills/augur-method/` | `.claude/skills/augur-method/` | `.qwen/skills/augur-method/` |
| `.kimi-code/skills/umbraco-sensei/` | `.claude/skills/umbraco-sensei/` | `.qwen/skills/umbraco-sensei/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | deployed to `~/.codex/` from `.kimi-code/` (single source, four homes) |

The agent row is the one format break in the kit: md frontmatter on the four md-based sides becomes a standalone TOML role file on the Codex side (fields exactly `name`, `description`, `model`, `developer_instructions`; body → `developer_instructions`). Semantics stay identical; only the container changes.

## Non-paired entries

| Entry | Location | Rule |
|-------|----------|------|
| `.kimi-code/BACKLOG/_TEMPLATE/` + `.kimi-code/TASKS/_TEMPLATE/` | <removed 2026-09-24 — artifact format is owned by Overmind materialization; BACKLOG deploy entries removed from manifest.json> | Planning state lives on the Overmind board; `BACKLOG/` in project roots is a read-only materialized mirror, never deployed by this repo |
| `.kimi-code/SOCIAL_DICTIONARY.md` | `.kimi-code/` only in repo | Referenced by all sides via path. The install script copies it into all four tool homes (`~/.kimi-code/`, `~/.claude/`, `~/.qwen/`, `~/.codex/`). |
| `.kimi-code/WAKE-CHANNEL.md` | `.kimi-code/` only in repo | Referenced by all sides via path; install script copies it into all four tool homes. Runtime-agnostic recipe: the 190 wake tick reads the Host's wake drop file `~/.overmind/wake/<lowercased alias>.json` before `overmind_wake_peek` (Host contract, feature 201). |
| `.kimi-code/config.toml` | `.kimi-code/` only | Kimi-only (secondary-model pool). Claude analog is `~/.claude/settings.json`; Qwen analog is `~/.qwen/settings.json`; Codex analog is `~/.codex/config.toml` — NOT content-synced; each side's runtime config is managed independently. The kit never writes the Codex config: model pins live in `.codex/agents/*.toml` role files, and the config itself stays human-owned. |
| `.qwen/settings-snippet.jsonc` | `.qwen/` only | Qwen-only. Merge-ready `permissions` + `hooks` layer, deployed to `~/.qwen/` as a **reference copy**. It is NEVER installed as `settings.json` — merging is a human action (config self-modification is human-only per the crew contract). |
| `.qwen/hooks/post-edit-verify.ps1` / `.sh` | `.qwen/` only | Qwen-only. `PostToolUse` verification hook (dotnet build after `*.cs`, lint + `tsc --noEmit` after frontend edits). Fail-open by design. Referenced by the settings snippet; Windows entry is active, the bash variant is a documented swap. |
| `manifest.json` + `install.ps1` / `install.sh` / `uninstall.ps1` / `uninstall.sh` | repo root | Deployment scripts driven by `manifest.json` (single source of truth for the deploy set). Not mirrored to each other — they are per-OS entry points to the same logic. Every entry carries an explicit `mode` (see Deploy modes below). |

## Known intentional differences

| Aspect | Kimi side | Claude side | Qwen side | Codex side |
|--------|-----------|-------------|-----------|------------|
| Sub-agent allowlists | `agents:` / model-pool restrictions enforced via config.toml + agent bodies | `subagents:` frontmatter DROPPED — unsupported key; policy in skill bodies, prompt-enforced | `subagents:`/`whenToUse:` DROPPED — unsupported keys; `tools:` allowlists KEPT (Qwen supports them) with canonical Qwen tool names; delegation policy in QWEN.md + skill bodies | No frontmatter at all; allowlist restrictions do not live in the roles (verified 2026-11-19: no `tools` field, no grant text in the 42 role files) — they live in `.codex/AGENTS.md` and the skill bodies |
| Model pool enforcement | `[secondary_model]` in config.toml limits spawnable models | No config-level pool; policy text in CLAUDE.md + skill bodies | `model:` frontmatter pin per agent (`qwen3.8-flash` uniform), resolved through `modelProviders.openai` in settings.json; `inherit` forbidden for crew sub-agents | `model` field pin per TOML role (`gpt-6-luna` coders/fixer, `gpt-6.1-sol` heavy roles); complexity escalation via `spawn_agent(model="gpt-6.1-sol", fork_turns="none")` sanctioned by AGENTS.md; full-history forks forbidden (inherit parent model, reject overrides); 4 concurrency slots |
| Parallel spawn primitive | `AgentSwarm` | Multiple `Task` tool calls in one message | Multiple `agent` tool calls in one message; `subagent_type: "fork"` when the child needs parent context | Multiple `spawn_agent` calls in one turn; `fork_turns` controls context propagation (`"none"`/numeric for model overrides) |
| Deterministic enforcement | Hooks (PreToolUse/PostToolUse) + config deny-list | Prompt-enforced only | `permissions.deny`/`ask` + `PostToolUse` hooks in `~/.qwen/settings.json`, shipped as `.qwen/settings-snippet.jsonc` for hand-merging. Read/Edit deny rules also cover equivalent shell commands, so there is no `cat .env` bypass | Behavioral/prompt-only by crew decision: codex has hooks in the runtime (feature flag `hooks`, stable) but the kit ships none and never writes `~/.codex/config.toml`; verification discipline (build/lint in the same turn) replaces PostToolUse hooks |
| Agent frontmatter extras | — | — | `tools` kept (canonical names); `whenToUse` folded into `description` (Qwen delegates on `description`) | Strict TOML: EXACTLY `name`, `description`, `model`, `developer_instructions` — any extra field makes the loader drop the role (measured 2026-11-19 on 0.160.1: session-start stderr warning `Ignoring malformed agent role definition`, so fail-soft and observable, not silent; a `tools` value additionally parses against the tool-config namespace, never an allowlist) |
| MCP tools in allowlists (2026-10-15) | `mcp__overmind__*` listed verbatim in every `tools:` allowlist — MCP tool names are identical on every runtime | same | same; `Skill` and `Write` have Qwen ids `write_file` only — `Skill` is dropped, the augur researcher loads pdf/docx helpers through `run_shell_command` | no allowlist field and no per-role grant — the 46 TOML roles carry zero `mcp__` tokens (re-measured 2026-11-19 after rust parity); MCP access is declared in `.codex/AGENTS.md` and the skill bodies, and a `tools` field cannot be added there (the strict loader drops the role — measured, not folklore) |
| Tool names in allowlists | Kimi tool ids | Claude Code tool ids (`Read`, `Grep`, `Glob`, `Bash`, `WebSearch`, `FetchURL`) | Canonical Qwen tool ids (`read_file`, `grep_search`, `glob`, `run_shell_command`, `web_fetch`). `WebSearch` has no Qwen equivalent — it and `FetchURL` collapse into a single `web_fetch` entry | No allowlist field — restrictions carried in `.codex/AGENTS.md` and the skill bodies, not per role |

## Translation table (Kimi → Claude)

Apply strictly in this order (most specific first). Used when porting a Kimi-side change to the Claude side; reverse for the other direction.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.claude/skills` |
| 2 | `~/.kimi-code/` | `~/.claude/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` | `haiku` |
| 4 | `kimi-code/kimi-for-coding` | `sonnet` |
| 5 | `kimi-code/kimi-k3` | `opus` |
| 6 | `kimi-code/k3-256k` | `opus` |
| 7 | `kimi-code/k3` | `opus` |
| 8 | `Kimi K2.7` | `Claude Sonnet` |
| 9 | `Kimi K3` | `Claude Opus` |
| 10 | `K2.7` | `Sonnet` |
| 11 | `K3` | `Opus` |
| 12 | `Kimi Code CLI` | `Claude Code` |
| 13 | `kimi-code` | `claude` (path/identifier contexts only — eyeball every hit) |
| 14 | `AgentSwarm` | `multiple Task tool calls in one message` |
| 15 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

## Translation table (Kimi → Qwen)

Apply strictly in this order (most specific first). After the table, a manual pass is MANDATORY: hook/config sections become `permissions` + `hooks` in `~/.qwen/settings.json` (point at `.qwen/settings-snippet.jsonc`); agent frontmatter tool names become canonical Qwen ids.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.qwen/skills` |
| 2 | `~/.kimi-code/` | `~/.qwen/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` / `kimi-code/kimi-for-coding` | `qwen3.8-flash` |
| 4 | `kimi-code/kimi-k3` / `kimi-code/k3-256k` / `kimi-code/k3` | `qwen3.8-flash` |
| 5 | `Kimi K2.7` / `K2.7` | `qwen3.8-flash` |
| 6 | `Kimi K3` / `K3` | `qwen3.8-flash` |
| 7 | `Kimi Code CLI` / `Kimi Code` | `Qwen Code` |
| 8 | `kimi-code` | `qwen` (path/identifier contexts only — eyeball every hit) |
| 9 | `AgentSwarm` | `multiple agent tool calls in one message` |
| 10 | `config.toml` | `~/.qwen/settings.json` (crew ships `.qwen/settings-snippet.jsonc` to merge by hand) |
| 11 | agent frontmatter `Read` / `Grep` / `Glob` / `Bash` / `WebSearch` / `FetchURL` | `read_file` / `grep_search` / `glob` / `run_shell_command` / `web_fetch` / `web_fetch` — then dedupe (`WebSearch` has no Qwen equivalent, it collapses into `web_fetch`) |
| 12 | `AGENTS.md` **as the name of the crew contract file** (global-context header, repo-layout line) | `QWEN.md`. Leave `AGENTS.md` alone where skill bodies cite it generically as the crew contract — all four sides do that identically, and Qwen Code reads a project `AGENTS.md` natively |
| 13 | `~/.claude` / `sonnet` / `opus` / `haiku` (if porting from the Claude side instead) | `~/.qwen` / `qwen3.8-flash` |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

Porting from the **Claude side** is the shortest path for agents and skills: only rows 11–13 plus `~/.claude/skills` → `~/.qwen/skills` apply.

## Translation table (Kimi → Codex)

Apply strictly in this order (most specific first). After the table, a manual pass is MANDATORY for agent ports: the md frontmatter+body becomes a TOML role file (`name`, `description`, `model`, `developer_instructions`) — that structural step has no find/replace form.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.codex/skills` |
| 2 | `~/.kimi-code/` | `~/.codex/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` / `kimi-code/kimi-for-coding` | `gpt-6-luna` |
| 4 | `kimi-code/kimi-k3` / `kimi-code/k3-256k` / `kimi-code/k3` | `gpt-6.1-sol` |
| 5 | `Kimi K2.7` / `K2.7` | `gpt-6-luna` |
| 6 | `Kimi K3` / `K3` | `gpt-6.1-sol` |
| 7 | `Kimi Code CLI` / `Kimi Code` | `Codex CLI` / `Codex` |
| 8 | `kimi-code` | `codex` (path/identifier contexts only — eyeball every hit) |
| 9 | `AgentSwarm` | `parallel spawn_agent calls` |
| 10 | `Agent tool` / `` `Agent` `` | `spawn_agent tool` / `` `spawn_agent` `` |
| 11 | `config.toml` (as the Kimi secondary-model pool) | role `model` pins in `.codex/agents/*.toml`; `~/.codex/config.toml` is human-owned and never written by the kit |
| 12 | `primary` alias ban (as the implicit-main-model ban) | full-history fork ban: forks with complete parent history inherit the parent model and reject overrides — spawn roles with `fork_turns: "none"` or a numeric string instead |
| 13 | agent file `.md` (YAML frontmatter + body) | `.toml` role: `name`/`description` one-line strings (escape `"` as `\"`), `model` per pin table, `developer_instructions` = body as a `"""` block — no other fields, strict loader |
| 14 | `~/.claude` / `sonnet` / `opus` / `haiku` (if porting from the Claude side instead) | `~/.codex` / `gpt-6-luna` (coders, fixer) / `gpt-6.1-sol` (everything else) |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

Model pin table for agent ports (verified codex-cli 0.160.1, 2026-10-07): `gpt-6-luna` → `fast-fixer-coder` + 8 language coders (dotnet, go, node, web, python, js-platform, maui, rust); `gpt-6.1-sol` → all remaining roles (orchestrators, reviewers, architects, audit, detection, obfuscation, ceremony, planners, helpers). Complex coder tasks escalate the same luna-pinned role via `spawn_agent(model="gpt-6.1-sol", fork_turns="none")` per AGENTS.md.

## Deploy modes (`manifest.json`)

Every manifest entry carries an explicit `mode` — the `install.sh`/`uninstall.sh` no-python fallback groups fields positionally, so a missing `mode` desynchronises the whole list.

| Mode | Install | Uninstall | Use for |
|------|---------|-----------|---------|
| `mirror` | whole-directory mirror: `robocopy /MIR` (Windows) / `rsync -a --delete` (Unix) | removes the deployed directory | crew-owned destinations (`~/.kimi-code/skills`, `~/.claude/agents`, `~/.codex/skills`, `~/.codex/agents`, …) |
| `merge` | each immediate child mirrored separately | removes only the children the repo still has | shared destinations that also hold third-party content — `~/.qwen/skills`, `~/.qwen/agents`, `~/.qwen/hooks` |

`merge` exists because `~/.qwen/` hosts non-crew content (e.g. `bailian-*` skills installed by `bl skill init`) that a whole-directory mirror would delete. Known limitation: `merge` uninstall only removes children that still exist in the repo — a crew skill deleted from the repo leaves an orphan in the home until removed by hand.

`~/.claude/skills` is **merge since 2026-10-15**: the home also hosts Anthropic-synced skills (`synced/`) that a whole-directory mirror would delete.

`~/.codex/skills` and `~/.codex/agents` are **mirror by user decision (2026-10-07)**: they are crew-owned; third-party skills or roles dropped there are deleted on the next install.

**Codex skills-directory semantics (T0 decision, feature 250, 2026-11-19).** Three surfaces, don't conflate them: (1) the kit repo side stays `.codex/skills/` — source of the mirror deploy; (2) Overmind *materialization* of codex skills lands in the target project's `.agents/skills/` (`platforms.json` codex entry — the documented current path: repo `.agents/skills` scanned CWD→repo root, user `$HOME/.agents/skills`, admin `/etc/codex/skills`, system bundled; 002 ADR amended in TECHNICAL-SOLUTIONS.md); (3) the home deploy still targets `~/.codex/skills/` — measured live on codex-cli 0.160.1 (2026-11-19): `$CODEX_HOME/skills` is scanned as a skill root even though current docs no longer list it, so the legacy home keeps working on the installed CLI; relocating the install target to `$HOME/.agents/skills` is an open follow-up (manifest/install), and `$HOME/.agents/skills` is shared with Kimi (`~/.agents/skills`, per its docs, merged with Kimi-specific skills) and Gemini (`.agents/skills` alias per its docs) — byte-identical agnostic content makes the sharing a duplication, not a collision; duplicate names stay visible (Codex docs: shared names are not merged). Context budget, measured 2026-11-19 on the same CLI: the 57 kit skills + 4 system-bundled render an initial list of ~22.1k chars (~5.5k tokens), no description shortened, no skill omitted — `skills.max_context_tokens` needs no tuning today; recheck after a CLI upgrade since the default budget is 2% of the model's context window.

**No gemini/antigravity mirror sides (feature 250 T5, owner-scope exclusion).** `platforms.json` carries `gemini` (`.gemini/skills`) and `antigravity` (`.antigravity/skills`) as *materialization targets* only — an Overmind kit lands skills for those platforms in the target project, and that is the surface the crew supports for them. The quadruple-edit contract stays four-sided by owner ruling (spec anti-scope: no native home skills loader for these two at kit level). Recorded against the docs so nobody "fixes" this by inventing a side: Gemini CLI itself does read user-level `~/.gemini/skills/` (alias `~/.agents/skills/`) and project `.gemini/skills/` — adding a fifth mirror side is a scope change, not a gap repair; the shared `.agents/skills` alias already serves those runtimes the moment materialization or the user home populates it.

## Skill authoring rules

Applies to creating or editing any skill in this repo. The verification half lives in the pre-commit checklist below — this is the authoring half.

- The frontmatter `description` is a TRIGGER, not a summary: name the situations and the user phrasings that should fire the skill. If no real user sentence would match it, rewrite the description, not the docs.
- One skill, one job. The moment a draft grows an "and also", split it — the crew prefers many small skills over God-skills.
- Write for a cold reader: exact tool names, exact commands, exact file paths. "As appropriate" inside a skill body is a bug.
- Anti-scope is as load-bearing as scope: state what the skill refuses and where it hands off.
- A skill is done when it passes the pre-commit checklist below and, where practical, has been dogfooded — the feature that adds a skill exercises it at least once.

## Pre-commit checklist (run before committing any crew edit)

1. All four sides of every touched quadruple updated in the same commit.
2. No cross-runtime leakage. **Known benign exceptions:** `skills/umbraco-sensei/references/breaking-changes.md` carries the Umbraco API name `JsonNetSerializer` (its substring `sonNetS` trips the case-insensitive `sonnet` grep — filter `umbraco-sensei` out); `skills/coding-challenge-orchestrator/SKILL.md` names every runtime on purpose (its AI-leakage deny-list must stay identical on all four sides); `skills/squeezr-ops/SKILL.md` is a product doc that names several runtimes on purpose (a Claude settings example in its hook section, qwen/kimi client notes) and is identical on all four sides. Filter all three out of every grep below.
   ```bash
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|qwen3\.\|\.qwen' .claude/                                     | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   grep -rni 'claude code\|sonnet\|opus\|haiku\|qwen3\.\|\.qwen' .kimi-code/                                 | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|sonnet\|opus\|haiku\|\.claude' .qwen/  | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   grep -rni 'qwen' .kimi-code/ .claude/                                                | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   grep -rniE 'k2\.7|\bk3\b|kimi|agentswarm|sonnet|opus|haiku|\.claude|qwen3\.|\.qwen' .codex/ | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   grep -rniE 'codex|gpt-6' .kimi-code/ .claude/ .qwen/                                             | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr | grep -v umbraco-sensei   # must be empty
   ```
   ripgrep-based tooling skips dot-directories by default — pass the runtime dir explicitly, or add `--hidden`.
3. `Known intentional differences` table still accurate — if a runtime gains/loses a capability, update this file, not just one side.
4. Count parity intact: 46 agent files + 57 skill dirs on all four runtime sides (rust parity landed 2026-11-19 — the `.claude`-only 57/46 vs 51/42 asymmetry is closed; the pre-existing qwen gap — `git-commit-ai-detector` — was closed 2026-11-01).
   ```bash
   for d in .kimi-code .claude .qwen .codex; do printf '%-22s agents=%s skills=%s\n' "$d" "$(ls "$d/agents" | wc -l)" "$(ls "$d/skills" | wc -l)"; done
   ```
5. Deploy set still consistent: `manifest.json` parses, both parsers agree, and dry runs are clean.
   ```bash
   powershell -File install.ps1 -DryRun      # Windows
   ./install.sh --dry-run                    # Unix / Git Bash
   powershell -File uninstall.ps1 -DryRun
   ./uninstall.sh --dry-run
   ```
6. The Qwen model pin still exists in the configured provider — if the ModelStudio lineup changes, `.qwen/agents/*.md` `model:` values, QWEN.md «Sub-Agent Rules» and the model rows (3–6, 13) of the Kimi→Qwen table are updated together.
7. Frontmatter stays valid YAML on the four md-based sides (see «Frontmatter convention»; Codex agents are TOML and outside this rule) — an unquoted `: ` in a single-line value is a silent regression:
   ```bash
   grep -rnE '^(description|whenToUse): [^"|>-].*: ' .kimi-code/agents .claude/agents .qwen/agents   # must be empty
   ```
8. Codex role files stay minimal TOML — exactly `name`, `description`, `model`, `developer_instructions`; anything else drops the role (loader warns on stderr — measured 2026-11-19, see KIT-MEMBERSHIP):
   ```bash
   python3 - <<'EOF'
import glob, tomllib, sys
bad = []
for p in glob.glob('.codex/agents/*.toml'):
    d = tomllib.load(open(p, 'rb'))
    if set(d) != {'name', 'description', 'model', 'developer_instructions'}:
        bad.append((p, sorted(d)))
print(bad if bad else 'clean')
sys.exit(1 if bad else 0)
EOF
   ```
