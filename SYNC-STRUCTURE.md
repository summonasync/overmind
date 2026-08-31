# SYNC-STRUCTURE — Dual-Edit Contract

This repo maintains one agent crew for two runtimes: **Kimi Code CLI** (`.kimi-code/` incl. `AGENTS.md`) and **Claude Code** (`.claude/` incl. `CLAUDE.md`). Both sides must stay semantically identical.

## Core rule

**Edit one side — edit the other in the same commit.** No asymmetric changes to crew behavior, policies, workflows, or skill logic. Divergence is allowed ONLY for tool-specific concerns:

- model names and aliases (`Kimi K2.7` / `kimi-code/kimi-for-coding` vs `Claude Sonnet` / `sonnet`)
- home directory paths (`~/.kimi-code/` vs `~/.claude/`)
- runtime-specific features (Kimi `AgentSwarm`, config.toml secondary-model pool; Claude Task tool, settings.json)
- superpowers plugin reference form (local managed path vs Claude Code plugin)

Everything else — rules, workflow phases, review gates, sub-agent policy, conventions — is mirrored verbatim.

## File mapping

| Kimi side | Claude side |
|-----------|-------------|
| `.kimi-code/AGENTS.md` | `.claude/CLAUDE.md` |
| `.kimi-code/SKILLFLOW.md` | `.claude/SKILLFLOW.md` |
| `.kimi-code/agents/ai-code-detector.md` | `.claude/agents/ai-code-detector.md` |
| `.kimi-code/agents/ai-obfuscation-reviewer.md` | `.claude/agents/ai-obfuscation-reviewer.md` |
| `.kimi-code/agents/ai-obfuscator.md` | `.claude/agents/ai-obfuscator.md` |
| `.kimi-code/agents/architect-review.md` | `.claude/agents/architect-review.md` |
| `.kimi-code/agents/audit-orchestrator.md` | `.claude/agents/audit-orchestrator.md` |
| `.kimi-code/agents/coding-challenge-orchestrator.md` | `.claude/agents/coding-challenge-orchestrator.md` |
| `.kimi-code/agents/documentation-writer.md` | `.claude/agents/documentation-writer.md` |
| `.kimi-code/agents/dotnet-architect.md` | `.claude/agents/dotnet-architect.md` |
| `.kimi-code/agents/dotnet-code-reviewer.md` | `.claude/agents/dotnet-code-reviewer.md` |
| `.kimi-code/agents/dotnet-coder.md` | `.claude/agents/dotnet-coder.md` |
| `.kimi-code/agents/dotnet-init.md` | `.claude/agents/dotnet-init.md` |
| `.kimi-code/agents/frontend-designer.md` | `.claude/agents/frontend-designer.md` |
| `.kimi-code/agents/maui-coder.md` | `.claude/agents/maui-coder.md` |
| `.kimi-code/agents/next-js-architect.md` | `.claude/agents/next-js-architect.md` |
| `.kimi-code/agents/next-js-code-reviewer.md` | `.claude/agents/next-js-code-reviewer.md` |
| `.kimi-code/agents/next-js-coder.md` | `.claude/agents/next-js-coder.md` |
| `.kimi-code/agents/task-orchestrator.md` | `.claude/agents/task-orchestrator.md` |
| `.kimi-code/agents/tutor-master.md` | `.claude/agents/tutor-master.md` |
| `.kimi-code/skills/ai-code-detector/` | `.claude/skills/ai-code-detector/` |
| `.kimi-code/skills/ai-obfuscation-reviewer/` | `.claude/skills/ai-obfuscation-reviewer/` |
| `.kimi-code/skills/ai-obfuscator/` | `.claude/skills/ai-obfuscator/` |
| `.kimi-code/skills/architect-review/` | `.claude/skills/architect-review/` |
| `.kimi-code/skills/audit-orchestrator/` | `.claude/skills/audit-orchestrator/` |
| `.kimi-code/skills/coding-challenge-orchestrator/` | `.claude/skills/coding-challenge-orchestrator/` |
| `.kimi-code/skills/documentation-writer/` | `.claude/skills/documentation-writer/` |
| `.kimi-code/skills/dotnet-architect/` | `.claude/skills/dotnet-architect/` |
| `.kimi-code/skills/dotnet-best-practices/` | `.claude/skills/dotnet-best-practices/` |
| `.kimi-code/skills/dotnet-code-reviewer/` | `.claude/skills/dotnet-code-reviewer/` |
| `.kimi-code/skills/dotnet-coder/` | `.claude/skills/dotnet-coder/` |
| `.kimi-code/skills/dotnet-init/` | `.claude/skills/dotnet-init/` |
| `.kimi-code/skills/frontend-designer/` | `.claude/skills/frontend-designer/` |
| `.kimi-code/skills/maui-coder/` | `.claude/skills/maui-coder/` |
| `.kimi-code/skills/next-js-architect/` | `.claude/skills/next-js-architect/` |
| `.kimi-code/skills/next-js-code-reviewer/` | `.claude/skills/next-js-code-reviewer/` |
| `.kimi-code/skills/next-js-coder/` | `.claude/skills/next-js-coder/` |
| `.kimi-code/skills/nuget-licensing-guard/` | `.claude/skills/nuget-licensing-guard/` |
| `.kimi-code/skills/task-orchestrator/` | `.claude/skills/task-orchestrator/` |
| `.kimi-code/skills/tutor-master/` | `.claude/skills/tutor-master/` |

## Non-paired entries

| Entry | Location | Rule |
|-------|----------|------|
| `TASKS/` | repo root only | Single source for feature tracking. The sync script deploys only `TASKS/_TEMPLATE/` into both tool homes; the rest of `TASKS/` is never copied. |
| `.kimi-code/SOCIAL_DICTIONARY.md` | `.kimi-code/` only in repo | Referenced by both sides via path. The sync script copies it into both tool homes. |
| `.kimi-code/config.toml` | `.kimi-code/` only | Kimi-only (secondary-model pool). Claude analog is `~/.claude/settings.json` — NOT content-synced; each side's runtime config is managed independently. |
| `sync.ps1` / `sync.sh` | repo root | Deployment scripts (planned, task 05). Not mirrored to each other — they are per-OS entry points to the same logic. |

## Known intentional differences

| Aspect | Kimi side | Claude side |
|--------|-----------|-------------|
| Sub-agent allowlists | `agents:` / model-pool restrictions enforced via config.toml + agent bodies | `subagents:` frontmatter allowlists were DROPPED — Claude agents do not support that key. Delegation policy lives in skill bodies (which agents may spawn which) and is prompt-enforced only. |
| Model pool enforcement | `[secondary_model]` in config.toml limits spawnable models | No config-level pool; policy text in CLAUDE.md + skill bodies |
| Parallel spawn primitive | `AgentSwarm` | Multiple `Task` tool calls in one message |

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

## Pre-commit checklist (run before committing any crew edit)

1. Both sides of every touched pair updated in the same commit.
2. No cross-runtime leakage:
   ```bash
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/   # must be empty
   grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/  # must be empty
   ```
3. `Known intentional differences` table still accurate — if a runtime gains/loses a capability, update this file, not just one side.
4. Count parity intact: 18 agent files + 20 skill dirs on each side.
