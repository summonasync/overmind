---
feature: 002
slug: kit-library
status: done
version: 2
created: 2026-09-06
---

# Feature 002 — Kit library

# Feature 002 — Kit library

<goal>
Users can grow their kit collection from outside the app: import kits from a local folder (multi-kit detection, cross-platform skill mapping) and later exchange kits as a single file. Imported kits become first-class kits: versioned, linkable to projects, materializable exactly like hand-made ones. Bundled kits (feature 003) ride this same import pipeline.
</goal>

<requirements>
- A user can import kits from a local folder: the folder is scanned for skill structures (SKILL.md trees), top-level kit candidates are detected, the user picks what to import from a preview with per-kit validation reports.
- Cross-platform import: recognized agent platform directories (`.claude`, `.kimi`, `.codex`, ... per `platforms.json`) map to per-platform skill variants; skills without a platform marker import as platform-agnostic and materialize to every known agent.
- Kits can carry gates and guards, imported from an Overmind-owned convention (`gates/*.md`, `guards/*.md`) — agents have no native format for these.
- Re-import from the same source path updates the existing kit: re-scan, three-way diff (file vs last import vs local edits), per-entry report; conflicts are refused and reported, never silently overwritten.
- Import validation: broken/incomplete skill structures are reported, not silently swallowed.
- (002b) A kit can be exported to and imported from a single `.omkit` file (zip + manifest), riding the same import pipeline.
- Library view in UI: all kits with origin (hand-made/imported/bundled later), source path and re-import action for imported kits.
</requirements>

<non-goals>
- Public marketplace / kit sharing hub.
- Git-linked kits (clone/pull, private repos, stored credentials) — future v2 feature, see tombstones below.
- Auto-update of imported kits (manual re-import only).
- Conflict resolution UI beyond per-entry "report and refuse".
- MCP/agent-facing import — import is UI/API-driven only.
</non-goals>

<!-- removed scope (user decision 2026-09-14, brainstorm for spec v1): -->
<!-- <removed 2026-09-14 — git repo URL kits (clone/pull into managed storage): moved to a future v2 feature "git-linked kits"> -->
<!-- <removed 2026-09-14 — private repo credentials (PAT/SSH, OS keychain storage): rides the future git-linked kits feature; when it lands, credentials delegate to the git credential helper (user preference 2026-09-14)> -->

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
