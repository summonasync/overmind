---
feature: 004
slug: missing-language-coders
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Spec 004 — Missing language coders: js-ts frontend/backend split, python, go, solution-architect

Resolved by the Socratic session 2026-09-28 (this spec supersedes the OPEN QUESTION in the feature body). Every change lands ×4 runtimes (.kimi-code / .claude / .gemini/antigravity / .qwen) per SYNC-STRUCTURE.md; master side .kimi-code → port ×3, per 003's proven pattern. Phases ship in order A → B → C → D; each phase is independently verifiable.

## Resolved domain model (Socratic output — the routing contract)

| Lane | Owns | Never touches |
|---|---|---|
| web-coder | Next.js/React FULL-STACK apps: UI + RSC + Server Actions + BFF + API routes; SPAs; UI component libraries | standalone Node services; Electron/RN |
| node-coder | Standalone Node services (Express and derivatives: Fastify/Nest/Hono), workers, scripts/CLI/tooling, non-UI npm libraries | anything inside a Next.js repository; UI |
| js-platform-coder | Electron + React Native: main/preload processes, native modules, navigation, multi-platform (2–3 targets) builds | web and pure-backend code |

Monorepos containing a Next app + a standalone Node service: both coders, tasks split by app/repo boundary (never by file inside the Next app).

## Phase A — js-ts trio

Renames (git mv, frontmatter + body triggers broadened, ×4):
1. `next-js-coder` → **`web-coder`**: Next.js remains the primary framework (FSD/RSC/BFF/React Query sections stay at current depth); triggers broaden to full-stack web + SPA; explicit non-ownership lines: standalone Node services → node-coder, Electron/RN → js-platform-coder.
2. `next-js-architect` → **`web-architect`**: scope broadens the same way; ownership note (consulted BY feature-orchestrator) preserved.
3. `next-js-code-reviewer` → **`web-code-reviewer`**: checklist broadens (full-stack web: RSC boundaries, BFF compliance, TS strict, React Query; existing sections stay).

New (skill + agent ×4):
4. **`node-coder`** — standalone Node services (Express and derivatives), workers, scripts/CLI, non-UI libs. TS strict, ESM, structured logging, no-comments, English-only. Model pins: claude sonnet / qwen qwen3.7-plus / kimi pool / antigravity role wording.
5. **`node-code-reviewer`** — mirrored checklist (reviewer = separate agent, independence rule from 003).
6. **`node-architect`** — service boundaries, transport, deployment shape for Node backends; ownership note like other architects.
7. **`js-platform-coder`** — Electron/React Native per the routing table.
8. **`js-platform-code-reviewer`**.
9. Best-practices libraries ×3 — **`web-best-practices`**, **`node-best-practices`**, **`js-platform-best-practices`** — skills WITHOUT agents (BP-ID catalogs, mirror dotnet-best-practices shape), initial set = top enforceable rules per lane, grow over time.

Registration for every file: SKILLFLOW.md routes + web/node/platform routing table; crew contract files (CLAUDE.md/AGENTS.md/GEMINI.md/QWEN.md) skill/agent mentions; SYNC-STRUCTURE.md mapping rows + count parity (23→28 agents, 26→34 skills after Phase A).

## Phase B — python trio

10. **`python-coder`** + skill — senior Python: uv/poetry packaging, ruff, mypy strict, pytest, asyncio; ML-context note (PyTorch concepts where relevant, per global rules); no-comments, English-only.
11. **`python-code-reviewer`** + skill.
12. **`python-best-practices`** skill (no agent).
No python-architect — deferred until the lane grows (same rationale as the platform architect); solution-architect covers top-level. Counts: +2 agents, +3 skills per side.

## Phase C — go trio

13. **`go-coder`** + skill — idiomatic Go: stdlib-first, error handling, context propagation, table-driven tests, gofmt.
14. **`go-code-reviewer`** + skill.
15. **`go-best-practices`** skill (no agent).
No go-architect — deferred. Counts: +2 agents, +3 skills per side.

## Phase D — solution-architect

16. **`solution-architect`** + skill — owns initial solution architecture, technology selection (with TECHNICAL-SOLUTIONS.md ADR ritual), large additions/rewrites/migrations of services; consulted by feature-orchestrator; stack architects (dotnet/web/node) remain its Phase-1 specialists. Model: claude opus / qwen qwen3.8-max / kimi pool / antigravity strongest-reasoning wording. Counts: +1 agent, +1 skill per side.

## Cross-cutting rules

- Reviewer independence everywhere: reviewers are separate agents; coders never self-review (003 rule — do not regress).
- Best-practices libraries never get agents.
- feature-orchestrator Phase 2 delegation lists and routing lines updated to the new coder names (they were touched by 003 — keep consistent).
- Every phase updates SYNC-STRUCTURE count-parity text; final crew ≈ 33 agents / 41 skills per side.
- frontmatter YAML conventions per runtime (qwen folding, quoting rules) — same as 003.

## Acceptance criteria

- [ ] After Phase A: `grep -rn "next-js-"` across all four runtime dirs → 0 hits (excluding BACKLOG/ and audits/); web/node/platform routing table present in SKILLFLOW.md ×4.
- [ ] Routing table from this spec is encoded in the three coder skills' anti-scope/handoff sections ×4 (no two-coder ambiguity on any file class).
- [ ] After each phase: count parity text updated in SYNC-STRUCTURE.md and parity loop passes on all four sides.
- [ ] Reviewers exist as separate agents for every coder lane; best-practices libraries exist as skills without agents.
- [ ] All five SYNC-STRUCTURE contamination greps empty after every phase; YAML frontmatter grep empty.
- [ ] solution-architect owns ADR/tech-selection ritual; stack architects carry the ownership note, not the top-level mandate.

## Constraints

- One commit per phase (or one commit total if user prefers), four sides in the same commit (quadruple-edit rule).
- English-only artifacts; no runtime enforcement changes; no behavior changes outside the items above.

<log></log>

<log>
- 2026-09-27 [agent] — validated with human proof: notification ff240c87-11ab-4cef-b9a8-68c7f3267371 replied 2026-09-27 21:49:23 UTC
- 2026-09-28 [agent] — User correction 2026-09-28: qwen model pins are uniform `qwen3.8-flash` everywhere — no 3.7-plus/3.8-max differentiation exists in the current ModelStudio lineup. All qwen agent pins in this feature (and the existing crew) use qwen3.8-flash. Body not edited (frozen); execution follows this note.
- 2026-09-28 [agent] — Clarification 2026-09-28 (orchestrator, after re-reading the user's absolute correction): the uniform `qwen3.8-flash` pin covers solution-architect TOO — §Phase D's «qwen qwen3.8-max» wording is superseded. T13 executes with qwen3.8-flash; T14 verifies zero qwen3.7-plus/qwen3.8-max mentions repo-wide.
</log>
