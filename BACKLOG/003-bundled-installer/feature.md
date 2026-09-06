---
feature: 003
slug: bundled-installer
status: draft
version: 1
created: 2026-09-06
---

# Feature 003 — Installer with pre-bundled kits

<goal>
Overmind ships as a proper Windows/macOS installer that comes with a curated set of pre-installed kits — first run is immediately useful, no empty library. Bundled kits ride the same library/import pipeline as user-imported ones (feature 002), so "bundled" is just a kit source, not a separate mechanism.
</goal>

<requirements>
- Windows and macOS installers (MSIX/pkg or equivalent) install the tray host, MCP adapter, and web UI assets.
- A curated set of default kits is embedded and materialized into the library on first run.
- Bundled kits are indistinguishable from imported ones: editable (as copies on edit), removable, refreshable if repo-linked.
- Upgrade path: installing a newer version updates bundled kits without clobbering user-modified copies.
- Uninstall cleans app data only on explicit user consent.
</requirements>

<non-goals>
- Auto-update of the app itself.
- Code signing / store distribution (separate ops concern).
- Linux packages.
</non-goals>

<refs>
- Blocked by BACKLOG/002-kit-library/ — bundled kits reuse its import pipeline and kit-source model.
</refs>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
-->
