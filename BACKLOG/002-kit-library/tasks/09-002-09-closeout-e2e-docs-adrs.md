---
feature: 002
task: 09
slug: kit-library
status: done
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
- [x] E2E scenario executed and recorded in `<log>` with observed outputs (incl. conflict cell of the re-import matrix)
- [x] Full build + test + frontend checks green
- [x] CONTEXT.md and TECHNICAL-SOLUTIONS.md updated (4 ADRs); BACKLOG/plan checkboxes synced
- [x] `audits/architect-review-2026-09-*-002-09.md` (or per skill naming) exists; zero open BLOCKERs
- [x] New non-blocking findings recorded as BACKLOG.md deferred lines
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
2026-09-15/16 — e2e executed live against the running Host (headless `--no-tray`, isolated HOME so the dev database is untouched; every call via the public HTTP API, no test doubles). Fixture: two candidates — `platform-kit` (`.claude/skills/deep-dark-fantasy`, `.kimi-code/skills/kimi-skill`) and `agnostic-kit` (`skills/boss-of-this-gym`, broken `skills/broken-skill/SKILL.md` without frontmatter, `gates/take-it-easy.md`, `guards/no-vanilla.md`).

Observed, step by step:
1. Scan — 2 candidates, `relativePath` echoes correctly (`agnostic-kit`, `platform-kit`); `deep-dark-fantasy` detected as platform `claude`, `kimi-skill` as `kimiCode`; agnostic skills `platform: null`; gates/guards listed with H1 titles; `invalidFrontmatter` issue on the broken skill; `truncated: false`. (Note: a first attempt used `.kimi/skills` — correctly NOT detected; the kimi dir is `.kimi-code` per `platforms.json`.)
2. Import — both kits in one request with one renamed (`agnostic-kit` → `Gym Boss Kit`); per-kit reports success, kit ids returned. Kit list shows `sourceKind: imported`, sourcePath, importedAtUtc.
3. Entries land in the auto-created "Kit Library" project: 4 + 2 entries incl. the broken skill (importer snapshots files with scan issues — the issue stays a scan-preview concern, documented behavior from 002/06).
4. Project `e2e-target` created, Gym Boss Kit activated → 18 files: every agnostic skill expanded to all 6 platform dirs (`.claude`, `.kimi-code`, `.codex`, `.gemini`, `.antigravity`, `.qwen`) + the built-in bootstrap skill. Gates/guards materialize as kit entries only (by 002a scope — materialization covers skills).
5. First re-import attempt EXPOSED A BLOCKER: skills matched by reconstructed key (`{skillsDir}/{title}` / `{title}`) vs the scanner's actual kit-root-relative path — agnostic skills under `skills/` fell out of matching: removed+added with total loss of the local edit and version history. Fixed the same day (2026-09-16): `SkillKitEntry.RelativePath` persisted (nullable column, migration `20260916083250_SkillKitEntryRelativePath`), canonical key = path from kit root (candidate prefix stripped at commit), legacy rows fall back to the old reconstruction. Pinned by `KitReimportTests` (subfolder matrix, prefix canonicalization, two-platform variant, null fallback).
6. Matrix re-run on a fresh database: local edit via `PUT /entries` (boss) + upstream edits on disk (boss + repaired broken) → re-import report: `conflicted boss-of-this-gym` (entryId 656ef8b8 RETAINED, local content preserved, upstream refused), `updated broken-skill` (same entryId, new body), `keptLocal` gate + guard, `added 0 / removed 0`. Verified via GET: boss content still contains the local edit, no upstream text; broken carries the repaired body. (broken's `currentVersion` did not bump — the known deferred `ContextEntry.UpdateContent` off-by-one, BACKLOG line "Normalize ContextEntry.UpdateContent…", pre-existing, not a 002 regression.)
7. Clear source — kit flips to `sourceKind: manual`, `sourcePath`/`importedAtUtc` null, 4 entries retained.

AC-1 (08's live-Host flow) is closed by the same run: folder → scan preview (2 candidates, issues, rename) → import → kits with origin badges data present for the UI contract.

Verification state at this point: `dotnet build Overmind.sln` 0 errors/warnings; full suite 495/495 (Core 94 / Infra 184 / Api 158 / Mcp 59) — one unrelated MCP flake (`ResourceTemplates_ContainTheThreePlanningUris`) on a first parallel run, green isolated and on rerun, recorded in microbatch queue. Web: lint 0 errors / 6 known warnings, tsc clean, static build ok (9 routes) — from the 002/08 fix pass; rerun confirmed after docs sync.
</log>
