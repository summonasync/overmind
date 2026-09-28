---
feature: 004
slug: missing-language-coders
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Plan 004 — Missing language coders: js-ts frontend/backend split, python, go, solution-architect

Master side .kimi-code → port ×3, per the 003-proven pattern. Phases ship A → B → C → D, each phase ends verifiable on its own. Model pins: coders/reviewers/stack-architects = claude sonnet / qwen qwen3.7-plus / kimi pool / antigravity role wording; solution-architect = claude opus / qwen qwen3.8-max. Best-practices libraries = skills WITHOUT agents.

## Phase A — js-ts trio (target: 28 agents / 34 skills per side)

- **T1 (master):** renames git mv ×3 — next-js-coder→web-coder, next-js-architect→web-architect, next-js-code-reviewer→web-code-reviewer; frontmatter/body broadened per spec routing table (Next full-stack stays primary; non-ownership lines: standalone Node → node-coder, Electron/RN → js-platform-coder); next-js-architect agent stub «and planning» cleaned here.
- **T2 (ports ×3):** the same renames with runtime frontmatter conventions.
- **T3 (master):** new node-coder + node-code-reviewer + node-architect (skills + agents) — standalone Express-derivative services, workers, scripts/CLI, non-UI libs; TS strict/ESM baseline.
- **T4 (ports ×3):** node trio with model pins.
- **T5 (master):** new js-platform-coder + js-platform-code-reviewer (Electron/React Native) + best-practices libraries ×3 (web/node/js-platform, BP-ID catalogs, no agents).
- **T6 (ports ×3):** platform pair + 3 libraries.
- **T7 (all sides):** registration sweep — SKILLFLOW routing table (web/node/platform lanes) + coder delegation lines in feature-orchestrator Phase 2 + contract files mentions + SYNC-STRUCTURE rows/count parity text.

## Phase B — python trio (30 agents / 37 skills)

- **T8 (master):** python-coder + python-code-reviewer (skills + agents) + python-best-practices (uv/poetry, ruff, mypy strict, pytest, asyncio; ML-context note).
- **T9 (ports ×3):** python trio.

## Phase C — go trio (32 agents / 40 skills)

- **T10 (master):** go-coder + go-code-reviewer + go-best-practices (stdlib-first, error handling, context, table tests, gofmt).
- **T11 (ports ×3):** go trio.

## Phase D — solution-architect (33 agents / 41 skills)

- **T12 (master):** solution-architect skill + agent — solution-level architecture, technology selection + TECHNICAL-SOLUTIONS.md ADR ritual, large service additions/rewrites; stack architects are its Phase-1 specialists; opus-tier model pins.
- **T13 (ports ×3):** solution-architect.

## Final

- **T14:** full SYNC-STRUCTURE pre-commit checklist + spec AC sweep (grep next-js- → 0 outside BACKLOG/audits; routing table present ×4; reviewer-per-lane present; best-practices without agents; counts 33/41 ×4; contamination greps; dry runs).

Verification per task: grep/count/diff checks written into each task artifact; every task review-gated by the orchestrator like 003.

<log></log>

<log>
- 2026-09-27 [agent] — validated with human proof: notification 4e92d3f7-6ba5-48a7-85da-90eefb2a6735 replied 2026-09-27 22:56:45 UTC
- 2026-09-28 [agent] — User correction 2026-09-28: qwen model pins uniform `qwen3.8-flash` everywhere (no 3.7-plus/3.8-max). Applies to every task mentioning qwen pins (T4/T6/T9/T11/T13) and to a new unification sweep task (order 15) covering existing .qwen/agents pins, QWEN.md Sub-Agent Rules and SYNC-STRUCTURE tables. Body not edited (frozen); execution follows this note.
</log>
