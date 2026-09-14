---
feature: 002
task: 09
slug: closeout-e2e-docs
status: todo
review: hard
created: 2026-09-14
---

# 002/09 — Closeout: e2e, docs, ADRs

<goal>
Feature 002a closes verified end-to-end and documented: the full import lifecycle proven on a real scenario, CONTEXT.md updated, architectural decisions recorded in TECHNICAL-SOLUTIONS.md, BACKLOG statuses synced, hard architect review artifact in audits/.
</goal>

<context>
All code tasks (01–08) are done and green. This task proves the feature as a whole, writes the durable docs (house rule: CONTEXT.md/STRUCTURE.md update in the same change that alters what they describe), and records the ADRs so future agents don't re-litigate the decisions. Executor reads ONLY this file.
</context>

<scope>
- E2E scenario (manual, scripted in `<log>` step by step): fixture folder with two kit candidates — one with platform-marked skills (`.claude/skills/...`), one with agnostic skills + `gates/` + `guards/`, plus one broken skill file → scan preview shows both candidates + issue → import → activate a kit on a project → verify materialized files under multiple platform dirs (agnostic expansion) → edit one skill in the UI and another skill's file on disk → re-import → report shows conflict (refused, local kept) + updated + kept-local → clear source → badge flips to manual
- `CONTEXT.md` — update the skill-kits section: kit origin model, import pipeline (scan → commit, re-import), agnostic expansion; entity list gains the new fields
- `TECHNICAL-SOLUTIONS.md` — ADRs: (1) import = snapshot into DB over file-backed kits; (2) agnostic-by-default cross-platform mapping + the spike verdict from `tasks/01-format-spike.md`; (3) three-way re-import diff via `ImportedContentHash`, conflicts = refuse + report; (4) two-phase scan/commit API with per-kit transactions
- `BACKLOG.md` — tick 002a scope status per outcome (002 line updated by orchestrator after review), plan.md `<tasks>` checkboxes ticked
- Hard architect review run per the architect-review skill; artifact lands in `audits/` with the skill prefix; BLOCKERs fixed in this task, non-blockers recorded to BACKLOG.md deferred lines
</scope>

<anti-scope>
- No new feature code beyond e2e-driven fixes
- No 002b (`.omkit`) work
- No fixing of deferred hardening items discovered along the way — record them, don't fix mid-task
</anti-scope>

<constraints>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` + web `npm run lint && npx tsc --noEmit && npm run build` all green at the end
- Docs in English (house rule); removed/changed decisions get tombstones, never silent deletion
- No auto-commits
</constraints>

<acceptance-criteria>
- [ ] E2E scenario executed and recorded in `<log>` with observed outputs (incl. conflict cell of the re-import matrix)
- [ ] Full build + test + frontend checks green
- [ ] CONTEXT.md and TECHNICAL-SOLUTIONS.md updated (4 ADRs); BACKLOG/plan checkboxes synced
- [ ] `audits/architect-review-2026-09-*-002-09.md` (or per skill naming) exists; zero open BLOCKERs
- [ ] New non-blocking findings recorded as BACKLOG.md deferred lines
</acceptance-criteria>

<refs>
- @../spec.md — whole spec (reviewer reads, not executor)
- @../plan.md — task 09 row + `<verification>` section
- tasks/01-format-spike.md — spike verdict for ADR (2)
- tasks/02..08 — everything this task verifies
</refs>

<obstacles>
</obstacles>

<log>
</log>
