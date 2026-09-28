---
name: pipeline-doctor
description: |
  Self-healing CI/CD: investigate a failing pipeline, fix it, retrigger, watch, repeat — bounded loop.
  Use when a CI/CD pipeline fails and the user wants an agent to diagnose and fix it autonomously
  (GitHub Actions, GitLab CI, Azure DevOps — transport-agnostic), without manual log digging.
type: prompt
---

# Pipeline Doctor — Bounded Self-Healing Loop

## Security Perimeter (non-negotiable)
- The agent works with the **default limited token scope** of the Git CLI (`gh` / `glab`): read/write repo, trigger pipelines. NEVER request or use widened scopes (no delete rights on packages/repos).
- Destructive git operations stay blocked per AGENTS.md deny-list. Branch protection is the user's job — flag it if absent.
- Sandbox/test environments only for fully autonomous mode. Production pipelines: every fix is proposed, applied only on user approval.

## Preconditions
- Confirm the codebase is **available locally** — say so to the sub-agents, or they will read files through the Git API instead of the workspace.
- AGENTS.md / SKILLFLOW.md give project context — skip codebase exploration for the fix itself.

## The Loop (max 3 iterations — HARD CAP)
1. **Diagnose.** Spawn a log-reader sub-agent (qwen3.8-flash): fetch the failing job logs via `gh run view --log` (or glab equivalent). Output: root cause hypothesis + affected files. Apply root-cause-first debugging (`overmind-ceremony` techniques) — no fixes without root cause.
2. **Fix.** Coder sub-agent (qwen3.8-flash) applies the minimal fix locally. No scope creep: only what the failing stage needs.
3. **Trigger & watch.** Push the fix branch / trigger the pipeline (`gh workflow run` or push). Watch until completion (`gh run watch`).
4. **If green** → go to Review. **If red** → logs back to step 1. **After 3 failed iterations → STOP**: report root-cause hypotheses tried, current error, and why it's likely outside the repo (secrets, quotas, infra) — human takes over.

## Review Gate
- A separate reviewer sub-agent (qwen3.8-flash) reviews the accumulated fix diff before reporting success: the fix must address the root cause, not mask it (no skipped tests, no `continue-on-error`, no pinned-to-old-version hacks without explicit user sign-off).
- Mask-style fixes are BLOCKERS — present to user with the trade-off, never merge silently.

## Output Contract
Per AGENTS.md: **Done** (root cause, fix diff summary, iterations used, pipeline URL) → **Next step** (usually: user reviews and merges) → **Alternatives** (e.g. audit-orchestrator if failure smells systemic). Then STOP.
- No auto-commits/pushes beyond the fix branch trigger — the merge decision is always the user's.
