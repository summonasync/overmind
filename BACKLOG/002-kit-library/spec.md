---
feature: 002
slug: kit-library
status: approved        # draft | approved (approved = FROZEN)
version: 1
created: 2026-09-14
approved: 2026-09-14 — user approved ("Спека пуля, пиши план"); frozen snapshot, changes require re-validation
---

# Spec 002 — Kit library

<decisions>
Brainstorm 2026-09-14 (user answers) + follow-up design calls. D1–D6 are user decisions; D7–D12 are proposals from the brainstorm agent — veto at the spec gate.

- **D1 — Import = snapshot into DB.** Import parses the folder and creates ordinary `ContextEntry` rows (skills with per-platform `SkillFile`s, gates, guards) linked to a new `SkillKit`. FTS, `EntryVersion`, materializer, drift — all work unchanged. The source folder is not needed after import; no file-backed kits.
- **D2 — Scope split.** 002a = local folder import (this spec, detailed). 002b = single-file `.omkit` export/import (specified here at design level, planned separately). Git-linked kits = future v2 feature, tombstoned in feature.md.
- **D3 — Cross-platform: agnostic-by-default.** Recognized platform directories (from `platforms.json`, same source the materializer uses) map to per-platform variants. Skills outside any platform directory import as platform-agnostic and materialize to every known agent. Frontmatter normalization per target platform happens at materialization time, not import time — requires a format spike across agents (Claude, Kimi, Codex, Gemini, Qwen) before the materializer changes.
- **D4 — Import scope: skills + gates/guards.** Gates/guards have no agent-native format, so Overmind owns the convention: `gates/*.md` and `guards/*.md` at kit root, one file = one entry, title from H1 (fallback: filename).
- **D5 — Granularity: multi-kit detection.** The scanned folder may contain several kits: every top-level subdirectory containing skill content is a candidate kit; the folder root itself is a candidate if it directly contains skills. Import preview lists candidates with checkboxes, editable kit names and per-kit validation issues.
- **D6 — Re-import = update.** A kit remembers its source path; re-import re-scans and updates in place (no update flow was deferred with git — it lands now instead).
- **D7 — Origin model on `SkillKit`.** New fields: `SourceKind` enum (`manual` | `imported` | `bundled` — bundled reserved for 003), `SourcePath` (nullable, absolute path of the import source), `ImportedAtUtc` (nullable). Hand-made kits are `manual` with nulls — additive migration, no backfill needed.
- **D8 — Three-way re-import diff via per-entry import hash.** `SkillKitEntry` gains nullable `ImportedContentHash` (SHA-256 of normalized content at last import, same normalization as materialization hashes). On re-import per entry: file unchanged → keep local edits; entry unchanged since import → take file content (new `EntryVersion`); both changed → conflict: refuse that entry, keep local, report. New files → new entries; files gone since last import → entry removed from kit and deleted (it is kit-owned), reported as removed.
- **D9 — Name collision policy.** Preview pre-fills kit names from folder names and flags collisions with existing kits; the user renames in the preview. A collision that still reaches the API fails that one kit with an error in its report — other selected kits import normally (per-kit atomicity).
- **D10 — Two-phase API: scan then commit.** `POST /api/kits/import/scan` (path) → preview DTO (candidates, per-candidate entries with detected platforms/types, validation issues, name collisions, existing-kit match by `SourcePath`). `POST /api/kits/import` (path + selections + names) → per-kit import reports. `POST /api/kits/{id}/reimport` → update report. Scan is read-only and side-effect-free; commit is per-kit transactional.
- **D11 — `.omkit` = zip with manifest (002b).** Archive layout: `manifest.json` (format version, kit name/description/version, entry metadata: type, title, platform variants, relative paths, content hashes, exported-at, exporter app version) + entry files in the same folder convention as D4/D3. Import unpacks to a temp dir and rides the 002a folder pipeline unchanged — one canonical importer. Export adds `GET /api/kits/{id}/export` (file download) and the kits UI gets export/import-file buttons.
- **D12 — Import lives in the kits UI.** Kits page gets "Import from folder" (002a) / "Import file" + per-kit "Export" (002b). Kit detail shows origin badge (kind, source path, imported-at) and "Re-import" for imported kits. No MCP surface for import.
</decisions>

<open-questions>
- None blocking. D7–D12 are agent proposals awaiting user validation at this gate.
</open-questions>

<requirements>
Refined from feature.md through the brainstorm; baseline lives in feature.md.

## Scan & detection (002a)

- R1 — Scan recognizes a skill as a directory containing `SKILL.md`; nested files belong to the skill. A platform-marked skill sits under `<platform-dir>/skills/<skill>/` (platform dirs from `platforms.json`); any other skill directory is platform-agnostic.
- R2 — `gates/*.md` / `guards/*.md` at a kit candidate's root import as gate/guard entries (one file = one entry, H1 or filename as title). Files that fail to parse (no valid SKILL.md frontmatter where required, unreadable, empty) appear as per-file issues in the preview; broken files never abort the scan.
- R3 — A kit candidate is: the scanned folder itself (if it directly contains skills/gates/guards) and each top-level subdirectory that does. Nested kits inside candidates are not detected (one level only).
- R4 — Scan matches candidates against existing kits by `SourcePath` and flags them as "re-import" in the preview (import of such a candidate is rejected with a pointer to the re-import action — update goes through one door).

## Import commit (002a)

- R5 — Commit creates, per selected kit: `SkillKit` (SourceKind `imported`, SourcePath, ImportedAtUtc), entries, `SkillKitEntry` links with `ImportedContentHash`. Skills get `SkillFile`s per detected platform variant; platform-agnostic skills get a single agnostic variant.
- R6 — Each kit imports in its own transaction: a failing kit rolls back fully and reports; other kits are unaffected.
- R7 — Kit name comes from the (possibly user-edited) preview name; final collision → that kit fails with a name-collision error.

## Re-import (002a)

- R8 — Re-import applies the D8 three-way diff per entry and returns a report: added / updated (new `EntryVersion`) / kept-local / conflicted (refused, both changed) / removed (file gone).
- R9 — Re-import updates `ImportedAtUtc` and every touched `ImportedContentHash`; conflicts keep the old hash so the next re-import still sees the conflict.
- R10 — Removing the source folder or importing from a moved path: re-import of a missing path returns 409 with a clear error; the user can clear `SourcePath` (convert to manual kit) from the UI.

## Platform normalization (002a)

- R11 — Spike first: compare SKILL.md frontmatter across Claude / Kimi / Codex / Gemini / Qwen; record findings in the task log. Materialization normalizes per target platform only where the spike shows real differences; otherwise files pass through byte-identical (current behavior).

## 002b (design level — detailed spec when planned)

- R12 — `GET /api/kits/{id}/export` produces an `.omkit` (zip + manifest per D11); import of an `.omkit` file unpacks and rides the folder pipeline, including preview and re-import semantics (manifest entries carry hashes, so re-import from a newer file works like folder re-import).

## UI (002a)

- R13 — Kits page: "Import from folder" → OS folder picker → scan preview dialog (candidates with checkboxes, editable names, per-kit issue lists, re-import flags) → confirm → per-kit report view.
- R14 — Kit list/detail: origin badge (`imported` + source path + imported-at, `manual` otherwise), "Re-import" action on imported kits with the update report surfaced, "clear source" action per R10.
</requirements>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->
