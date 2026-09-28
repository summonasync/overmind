---
feature: 004
slug: missing-language-coders
status: draft
created: 2026-09-28
---

# Result 004 — Missing language coders: js-ts frontend/backend split, python, go, solution-architect

All 19 tasks done (14 planned + T15 qwen-pin sweep + T18 003-residue fixes + T19 F1 delegation fix), spec + plan validated, executed 2026-09-28 via sonnet executors under the briefing contract, every task review-gated by the orchestrator.

## What shipped (×4 runtimes in every case)

- **Phase A — js-ts trio:** next-js-coder/next-js-architect/next-js-code-reviewer renamed to web-coder/web-architect/web-code-reviewer with full-stack triggers and the spec's routing contract baked into anti-scopes; NEW node-coder + node-code-reviewer + node-architect (Node 24 LTS, TS strict, zod boundaries, pino field-logging, layered services, AbortSignal discipline, transport table); NEW js-platform-coder + js-platform-code-reviewer (Electron security model, IPC hygiene, RN navigation/native modules, 2–3 target builds); 3 BP-ID catalogs (web 25 / node 20 / platform 20 rules).
- **Phase B — python trio:** python-coder (uv/ruff/mypy strict/pytest-asyncio + ML/DL section: PyTorch discipline, ONNX export for .NET consumption), python-code-reviewer, python-best-practices (BP-01..63).
- **Phase C — go trio:** go-coder (stdlib-first net/http, %w wrapping, goroutine ownership, table tests), go-code-reviewer, go-best-practices (38 rules).
- **Phase D — solution-architect:** senior-most authority — greenfield decomposition, technology selection with TECHNICAL-SOLUTIONS.md ADR ritual, strangler migrations with rollback + stack co-sign; stack architects are its Phase-1 specialists.
- **Registration:** lanes routing table in SKILLFLOW ×4; feature-orchestrator delegation covers all 7 lanes (.NET/web/node/Electron-RN/MAUI/python/go) + docs/learning; SYNC-STRUCTURE mapping complete, parity 33/41; solution-architect registered in SKILLFLOW/delegation/escalation notes ×4.

## Corrections absorbed mid-flight (all logged with provenance)

- **Qwen pins unified to `model: qwen3.8-flash`** (user absolute correction): 51 old-pin hits swept (T15), every task briefed, T14 verified 0 old-pin mentions repo-wide. Claude pins: sonnet for coders/reviewers/stack-architects, opus for solution-architect; gemini role wording.
- **003 residues fixed** (T18): sprint-planner route mirrored into 3 port SKILLFLOWs (003-T4 had wrongly treated SKILLFLOW as root-wide); stale «no BACKLOG.md index» replaced in 4 contract files (003-T9 had fixed only the ceremony skill).
- **F1:** python/go lanes added to feature-orchestrator delegation/review ×4 (T19).

## Verification

- T14 final sweep: 10/10 PASS (contamination greps, parity 33/41 ×4, YAML, dry runs, next-js- residue 0, pin sweep, lanes table, reviewer-per-lane, bp agentless, delegation table).
- Final state: **33 agents / 41 skills on every side**; target from the spec reached exactly.

## Deviations / errata

- New BP libraries = 5 (web/node/js-platform/python/go); the «6» in T14's report includes dotnet-best-practices.
- SYNC-STRUCTURE frontmatter affected set corrected from the documented 11 to the real 16.
- Solution-architect architect-level file count erratum (T11 log said 6 for the go trio; actually 5).

## Orchestration incidents (documented, no damage)

- Session restart orphaned a T5 executor mid-flight (re-dispatched; the orphan kept running and completed the task — duplicate dispatch stopped before file damage).
- Overmind API blip (connection refused, ~15 s) — atomic batch retried clean.
- Two junk task duplicates (orders 15/16) await UI deletion — MCP cannot delete tasks.

## Follow-ups

- Human: review diff → commit → push → re-run install.sh (deploys the new wave to the four homes; gemini side still pending the home existing).
- 006-crew-package-manager (parked idea): marker-based ownership + `--check` audit — the deploy/orphan pain 005 felt is now doubled by the crew size.

<log></log>