---
feature: 003
task: 06
slug: closeout-e2e-docs
status: done
review: hard
created: 2026-09-16
---

# 003/06 — Closeout: e2e, docs, ADRs

<goal>
Feature 003a closes verified end-to-end from the unpacked archive and documented: seed → use → personalize → upgrade → no-reseed, CONTEXT.md updated, decisions recorded as ADRs, BACKLOG synced, hard architect review artifact in `audits/`.
</goal>

<context>
All code tasks (01–05) are done and green. This task proves the feature as a whole from the user's seat (unpack → run), writes the durable docs (house rule: docs update in the same change), and records the ADRs. Executor reads ONLY this file.
</context>

<scope>
- E2E scenario (scripted in `<log>` step by step, against a freshly packed linux archive unpacked into a clean isolated HOME):
  1. first boot → all bundled kits seeded as `Bundled`, seed flag stamped
  2. kit visible via API with origin fields; activate on a project → materialization works (agnostic expansion included)
  3. edit one bundled kit's entry content → kit flips to `manual` (origin cleared)
  4. bump `bundleVersion` in the unpacked `bundled/manifest.json`, tweak an untouched bundled kit's source file → restart → upgrade sync: new/updated bundled kit refreshed via the diff, the manual fork untouched, no duplicate kits
  5. delete all kits → restart → no re-seed (flag holds)
  6. corrupt the manifest → restart → host boots fine, logged no-op
- `CONTEXT.md` — distribution story (portable archives, install scripts, upgrade model), bundled-kits lifecycle (seed/upgrade/edit-fork) incl. the `LibraryInUse` nuance from the 003/02 hard review (no stamp on a non-empty flagless library — a later-emptied library still seeds; the once-guard is the stamped flag), entity/API deltas if any, test counter update
- `TECHNICAL-SOLUTIONS.md` — 3 ADRs: (1) portable-first distribution over MSIX/pkg-first (dev-host constraint, 003b split); (2) bundled = a source kind with edit-fork semantics (no copy-on-edit, in-place manual conversion, shared-entry exclusion); (3) boot seed/upgrade sync is swallow-and-log like planning sync (never blocks startup, once-flag guard)
- `BACKLOG.md` — 003 line updated by orchestrator after review; plan.md checkboxes ticked
- Hard architect review per the architect-review skill; artifact `audits/architect-review-<date>-003-06.md`; BLOCKERs fixed in this task, non-blockers to BACKLOG deferred lines
</scope>

<anti-scope>
- No new feature code beyond e2e-driven fixes
- No 003b (MSIX/pkg) work; no CI
- No fixing deferred hardening items discovered along the way — record, don't fix
</anti-scope>

<constraints>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` + web `npm run lint && npx tsc --noEmit && npm run build` all green at the end
- Docs in English; tombstones for superseded statements; no silent deletions
- No auto-commits
</constraints>

<acceptance-criteria>
- [x] E2E scenario executed and recorded in `<log>` with observed outputs (incl. the upgrade-sync and no-reseed cells)
- [x] Full build + test + frontend checks green
- [x] CONTEXT.md and TECHNICAL-SOLUTIONS.md updated (3 ADRs); BACKLOG/plan synced
- [x] Hard review artifact exists; zero open BLOCKERs
- [x] New non-blocking findings recorded as BACKLOG deferred lines
</acceptance-criteria>

<refs>
- @../spec.md — whole spec (reviewer reads, not executor)
- @../plan.md — task 06 row + `<verification>` section
- tasks/01..05 — everything this task verifies
</refs>

<obstacles>
</obstacles>

<log>
2026-09-16 — e2e executed live against the freshly packed distribution (`dist/overmind-0.3.0-linux-x64.tar.gz`, repacked after the 003/04 review fixes), unpacked into an isolated HOME; every call via the public HTTP API. Observed step by step:

1. **First boot seeds**: clean HOME → `run.sh --no-tray` → `GET /api/kits` returns exactly one kit: `crew`, `sourceKind: bundled`, `sourcePath` inside the unpacked archive (`…/host/bundled/crew`), 23 entries.
2. **Edit-fork**: content edit (appended an e2e marker) of a `dotnet-best-practices` entry owned exclusively by the bundled crew kit via `PUT /api/entries/{id}` → immediate `GET /api/kits/{id}` shows `sourceKind: manual`, `sourcePath: null`, all 23 entries retained.
3. **Upgrade sync**: bumped `host/bundled/manifest.json` to `bundleVersion: 2` + appended an upstream marker to the bundled `ai-code-detector/SKILL.md` → restart → the manual fork is untouched and NO duplicate kit appears (the "crew" name collides with the manual fork → upgrade path skips it — spec D4's "edited kit is never touched", and the scenario's "no duplicate kits" cell). Then renamed the fork to `crew-my-fork` (PUT with the version concurrency token — note: kit rename requires `version` in the body) and bumped the manifest to 3 → restart → **both** upgrade branches proven: `crew` re-imported as a fresh `Bundled` kit carrying the v2 upstream marker (verified in the entry content), `crew-my-fork` (manual, 23 entries) untouched. Library: exactly 2 kits.
4. **No re-seed**: deleted both kits (204s) → restart → `GET /api/kits` = `[]` — the stamped `BundledSeedVersion` flag holds; bundles do not come back on an emptied library.
5. **Corrupt manifest**: replaced `manifest.json` with invalid JSON → restart → host boots, `/api/health` ok, kits endpoint answers — swallow-and-log posture confirmed live.
6. **Activate/materialize cell** (added after the hard review flagged it as NIT-1): manifest fixed to v4 → restart → add-missing re-imports the bundled `crew` (name freed earlier) → `POST /api/projects/{id}/kits/{kitId}/activate` → 144 materialized files across all six platform dirs (`.claude`, `.kimi-code`, `.codex`, `.gemini`, `.antigravity`, `.qwen`) — agnostic expansion working live from the unpacked archive.

Verification state at this point: `dotnet build Overmind.sln` 0/0; full suite 526/526 (Core 100 / Infra 203 / Api 164 / Mcp 59 — includes tasks 01–03 test additions); web lint 0 errors / 6 known warnings, tsc clean, static build 9/9 routes (task 05 pass). Repacked archives verified: 4 archives + sidecars, smoke `SMOKE OK` on linux (task 04 fix pass).
</log>
