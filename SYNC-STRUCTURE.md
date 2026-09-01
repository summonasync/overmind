# SYNC-STRUCTURE — Triple-Edit Contract

This repo maintains one agent crew for three runtimes: **Kimi Code CLI** (`.kimi-code/` incl. `AGENTS.md`), **Claude Code** (`.claude/` incl. `CLAUDE.md`), and **Google Antigravity** (`.gemini/` incl. `GEMINI.md`, deployed to `~/.gemini` + `~/.gemini/antigravity`). All three sides must stay semantically identical.

## Core rule

**Edit one side — edit the other two in the same commit.** No asymmetric changes to crew behavior, policies, workflows, or skill logic. Divergence is allowed ONLY for tool-specific concerns:

- model names, aliases, and selection mechanics (Kimi `K2.7`/`K3` pool vs Claude `sonnet`/`opus` vs Antigravity role-based choice in Agent Manager)
- home directory paths (`~/.kimi-code/` vs `~/.claude/` vs `~/.gemini/` + `~/.gemini/antigravity/`)
- runtime-specific features (Kimi `AgentSwarm` + config.toml secondary-model pool; Claude Task tool + settings.json; Antigravity Agent Manager + UI permission settings)
- enforcement mechanics (Kimi hooks/config, Claude prompt-enforced, Antigravity behavioral rules + IDE permission settings)
- superpowers plugin reference form (local managed path vs Claude Code plugin vs conditional "if installed")

Everything else — rules, workflow phases, review gates, delegation policy, conventions — is mirrored verbatim.

## File mapping

| Kimi side | Claude side | Antigravity side |
|-----------|-------------|------------------|
| `.kimi-code/AGENTS.md` | `.claude/CLAUDE.md` | `.gemini/GEMINI.md` |
| `.kimi-code/SKILLFLOW.md` | `.claude/SKILLFLOW.md` | `.gemini/antigravity/SKILLFLOW.md` |
| `.kimi-code/agents/ai-code-detector.md` | `.claude/agents/ai-code-detector.md` | `.gemini/antigravity/agents/ai-code-detector.md` |
| `.kimi-code/agents/ai-obfuscation-reviewer.md` | `.claude/agents/ai-obfuscation-reviewer.md` | `.gemini/antigravity/agents/ai-obfuscation-reviewer.md` |
| `.kimi-code/agents/ai-obfuscator.md` | `.claude/agents/ai-obfuscator.md` | `.gemini/antigravity/agents/ai-obfuscator.md` |
| `.kimi-code/agents/architect-review.md` | `.claude/agents/architect-review.md` | `.gemini/antigravity/agents/architect-review.md` |
| `.kimi-code/agents/audit-orchestrator.md` | `.claude/agents/audit-orchestrator.md` | `.gemini/antigravity/agents/audit-orchestrator.md` |
| `.kimi-code/agents/coding-challenge-orchestrator.md` | `.claude/agents/coding-challenge-orchestrator.md` | `.gemini/antigravity/agents/coding-challenge-orchestrator.md` |
| `.kimi-code/agents/documentation-writer.md` | `.claude/agents/documentation-writer.md` | `.gemini/antigravity/agents/documentation-writer.md` |
| `.kimi-code/agents/dotnet-architect.md` | `.claude/agents/dotnet-architect.md` | `.gemini/antigravity/agents/dotnet-architect.md` |
| `.kimi-code/agents/dotnet-code-reviewer.md` | `.claude/agents/dotnet-code-reviewer.md` | `.gemini/antigravity/agents/dotnet-code-reviewer.md` |
| `.kimi-code/agents/dotnet-coder.md` | `.claude/agents/dotnet-coder.md` | `.gemini/antigravity/agents/dotnet-coder.md` |
| `.kimi-code/agents/dotnet-init.md` | `.claude/agents/dotnet-init.md` | `.gemini/antigravity/agents/dotnet-init.md` |
| `.kimi-code/agents/frontend-designer.md` | `.claude/agents/frontend-designer.md` | `.gemini/antigravity/agents/frontend-designer.md` |
| `.kimi-code/agents/maui-coder.md` | `.claude/agents/maui-coder.md` | `.gemini/antigravity/agents/maui-coder.md` |
| `.kimi-code/agents/naming-helper.md` | `.claude/agents/naming-helper.md` | `.gemini/antigravity/agents/naming-helper.md` |
| `.kimi-code/agents/next-js-architect.md` | `.claude/agents/next-js-architect.md` | `.gemini/antigravity/agents/next-js-architect.md` |
| `.kimi-code/agents/next-js-code-reviewer.md` | `.claude/agents/next-js-code-reviewer.md` | `.gemini/antigravity/agents/next-js-code-reviewer.md` |
| `.kimi-code/agents/next-js-coder.md` | `.claude/agents/next-js-coder.md` | `.gemini/antigravity/agents/next-js-coder.md` |
| `.kimi-code/agents/pipeline-doctor.md` | `.claude/agents/pipeline-doctor.md` | `.gemini/antigravity/agents/pipeline-doctor.md` |
| `.kimi-code/agents/task-orchestrator.md` | `.claude/agents/task-orchestrator.md` | `.gemini/antigravity/agents/task-orchestrator.md` |
| `.kimi-code/agents/tutor-master.md` | `.claude/agents/tutor-master.md` | `.gemini/antigravity/agents/tutor-master.md` |
| `.kimi-code/skills/ai-code-detector/` | `.claude/skills/ai-code-detector/` | `.gemini/antigravity/skills/ai-code-detector/` |
| `.kimi-code/skills/ai-obfuscation-reviewer/` | `.claude/skills/ai-obfuscation-reviewer/` | `.gemini/antigravity/skills/ai-obfuscation-reviewer/` |
| `.kimi-code/skills/ai-obfuscator/` | `.claude/skills/ai-obfuscator/` | `.gemini/antigravity/skills/ai-obfuscator/` |
| `.kimi-code/skills/architect-review/` | `.claude/skills/architect-review/` | `.gemini/antigravity/skills/architect-review/` |
| `.kimi-code/skills/audit-orchestrator/` | `.claude/skills/audit-orchestrator/` | `.gemini/antigravity/skills/audit-orchestrator/` |
| `.kimi-code/skills/coding-challenge-orchestrator/` | `.claude/skills/coding-challenge-orchestrator/` | `.gemini/antigravity/skills/coding-challenge-orchestrator/` |
| `.kimi-code/skills/documentation-writer/` | `.claude/skills/documentation-writer/` | `.gemini/antigravity/skills/documentation-writer/` |
| `.kimi-code/skills/dotnet-architect/` | `.claude/skills/dotnet-architect/` | `.gemini/antigravity/skills/dotnet-architect/` |
| `.kimi-code/skills/dotnet-best-practices/` | `.claude/skills/dotnet-best-practices/` | `.gemini/antigravity/skills/dotnet-best-practices/` |
| `.kimi-code/skills/dotnet-code-reviewer/` | `.claude/skills/dotnet-code-reviewer/` | `.gemini/antigravity/skills/dotnet-code-reviewer/` |
| `.kimi-code/skills/dotnet-coder/` | `.claude/skills/dotnet-coder/` | `.gemini/antigravity/skills/dotnet-coder/` |
| `.kimi-code/skills/dotnet-init/` | `.claude/skills/dotnet-init/` | `.gemini/antigravity/skills/dotnet-init/` |
| `.kimi-code/skills/frontend-designer/` | `.claude/skills/frontend-designer/` | `.gemini/antigravity/skills/frontend-designer/` |
| `.kimi-code/skills/maui-coder/` | `.claude/skills/maui-coder/` | `.gemini/antigravity/skills/maui-coder/` |
| `.kimi-code/skills/naming-helper/` | `.claude/skills/naming-helper/` | `.gemini/antigravity/skills/naming-helper/` |
| `.kimi-code/skills/next-js-architect/` | `.claude/skills/next-js-architect/` | `.gemini/antigravity/skills/next-js-architect/` |
| `.kimi-code/skills/next-js-code-reviewer/` | `.claude/skills/next-js-code-reviewer/` | `.gemini/antigravity/skills/next-js-code-reviewer/` |
| `.kimi-code/skills/next-js-coder/` | `.claude/skills/next-js-coder/` | `.gemini/antigravity/skills/next-js-coder/` |
| `.kimi-code/skills/nuget-licensing-guard/` | `.claude/skills/nuget-licensing-guard/` | `.gemini/antigravity/skills/nuget-licensing-guard/` |
| `.kimi-code/skills/pipeline-doctor/` | `.claude/skills/pipeline-doctor/` | `.gemini/antigravity/skills/pipeline-doctor/` |
| `.kimi-code/skills/task-orchestrator/` | `.claude/skills/task-orchestrator/` | `.gemini/antigravity/skills/task-orchestrator/` |
| `.kimi-code/skills/tutor-master/` | `.claude/skills/tutor-master/` | `.gemini/antigravity/skills/tutor-master/` |

## Non-paired entries

| Entry | Location | Rule |
|-------|----------|------|
| `.kimi-code/BACKLOG/_TEMPLATE/` | deployed to all three homes | Verbatim runtime-agnostic templates, deployed to all three tool homes via manifest (antigravity home receives `.kimi-code/BACKLOG` as-is — content is neutral). Root `BACKLOG/` (feature folders, repo-local tracking) is NEVER deployed. |
| `.kimi-code/SOCIAL_DICTIONARY.md` | `.kimi-code/` only in repo | Referenced by all sides via path. The install script copies it into all three tool homes (`~/.kimi-code/`, `~/.claude/`, `~/.gemini/`). |
| `.kimi-code/config.toml` | `.kimi-code/` only | Kimi-only (secondary-model pool). Claude analog is `~/.claude/settings.json`; Antigravity analog is UI permission settings — NOT content-synced; each side's runtime config is managed independently. |
| `manifest.json` + `install.ps1` / `install.sh` / `uninstall.ps1` / `uninstall.sh` | repo root | Deployment scripts driven by `manifest.json` (single source of truth for the deploy set). Not mirrored to each other — they are per-OS entry points to the same logic. |

## Known intentional differences

| Aspect | Kimi side | Claude side | Antigravity side |
|--------|-----------|-------------|------------------|
| Sub-agent allowlists | `agents:` / model-pool restrictions enforced via config.toml + agent bodies | `subagents:` frontmatter DROPPED — unsupported key; policy in skill bodies, prompt-enforced | Policy in GEMINI.md Delegation Rules + skill bodies, prompt-enforced; delegation via Agent Manager |
| Model pool enforcement | `[secondary_model]` in config.toml limits spawnable models | No config-level pool; policy text in CLAUDE.md + skill bodies | No fixed pool; role-based wording (strong execution / strongest reasoning model), model picked in Agent Manager |
| Parallel spawn primitive | `AgentSwarm` | Multiple `Task` tool calls in one message | Parallel background agents in Agent Manager |
| Deterministic enforcement | Hooks (PreToolUse/PostToolUse) + config deny-list | Prompt-enforced only | Behavioral rules in GEMINI.md + IDE permission settings configured by the user |
| Agent frontmatter extras | — | — | `whenToUse`, `tools`, `subagents` keys kept; unknown keys are ignored by the runtime |
| Superpowers plugin | Required, managed path | Required for parity, Claude Code packaging | Optional; without it, follow SKILLFLOW.md transitions directly |

## Translation table (Kimi → Claude)

Apply strictly in this order (most specific first). Used when porting a Kimi-side change to the Claude side; reverse for the other direction.

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
| 14 | `kimi-code` | `claude` (path/identifier contexts only — eyeball every hit) |
| 15 | `AgentSwarm` | `multiple Task tool calls in one message` |
| 16 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |

## Translation table (Kimi → Antigravity)

Apply strictly in this order (most specific first). After the table, a manual pass is MANDATORY: hook sections become behavioral rules referencing Antigravity permission settings; sub-agent model pools become role-based wording; Kimi CLI flags/slash-commands are removed or mapped to Agent Manager equivalents.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.gemini/antigravity/skills` |
| 2 | `~/.kimi-code/plugins/managed/superpowers` | `superpowers plugin (if installed); otherwise follow SKILLFLOW.md transitions directly` |
| 3 | `~/.kimi-code/BACKLOG` | `~/.gemini/antigravity/BACKLOG` |
| 4 | `~/.kimi-code/` | `~/.gemini/antigravity/` |
| 5 | `kimi-code/kimi-for-coding-highspeed` | `fast execution model (selected in Agent Manager)` |
| 6 | `kimi-code/kimi-for-coding` | `strong execution model (selected in Agent Manager)` |
| 7 | `kimi-code/kimi-k3` / `kimi-code/k3-256k` | `strongest reasoning model (selected in Agent Manager)` |
| 8 | `Kimi K2.7` | `strong execution model` |
| 9 | `Kimi K3` | `strongest reasoning model` |
| 10 | `AgentSwarm` | `parallel background agents (Agent Manager)` |
| 11 | `config.toml` | `Antigravity permission settings` |
| 12 | `Kimi Code CLI` | `Google Antigravity` |
| 13 | `Kimi Code` | `Antigravity` |
| 14 | `~/.claude` / `sonnet` / `opus` / `haiku` (if present in source) | manual pass — replace with role-based wording |

## Pre-commit checklist (run before committing any crew edit)

1. All three sides of every touched triple updated in the same commit.
2. No cross-runtime leakage:
   ```bash
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku\|\.claude' .gemini/  # must be empty
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/   # must be empty
   grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/  # must be empty
   grep -rni 'gemini\|antigravity' .kimi-code/ .claude/   # must be empty
   ```
3. `Known intentional differences` table still accurate — if a runtime gains/loses a capability, update this file, not just one side.
4. Count parity intact: 20 agent files + 22 skill dirs on each side (`diff <(ls .kimi-code/skills) <(ls .gemini/antigravity/skills)` etc.).
