---
feature: 003
slug: bundled-installer
status: done
priority: p2
version: 5
created: 2026-09-06
---

# Feature 003 — Installer with pre-bundled kits

<goal>
Overmind ships as a proper installable distribution that comes with a curated set of pre-installed kits — first run is immediately useful, no empty library. Bundled kits ride the same library/import pipeline as user-imported ones (feature 002), so "bundled" is just a kit source, not a separate mechanism.
</goal>

<requirements>
- <!-- replaced 2026-09-16 — "Windows and macOS installers (MSIX/pkg or equivalent)": split per the 2026-09-16 brainstorm; the dev host is Linux and cannot build MSIX/pkg -->
- 003a: portable distribution — self-contained archives (win/linux/osx) + install scripts (user decision 2026-09-16).
- 003b: proper Windows/macOS installers (MSIX/pkg) — scheduled separately, blocked on a Windows/macOS build host or CI.
- A curated set of default kits is embedded and materialized into the library on first run (automatic on boot, user decision 2026-09-16).
- Bundled kits are indistinguishable from imported ones: editable, removable <!-- removed 2026-09-16 — "refreshable if repo-linked": git-linked kits moved to a future v2 feature (user decision 2026-09-14, tombstone in 002/feature.md); bundled refresh = upgrade sync from the embedded snapshot -->.
- Upgrade path: installing a newer version updates bundled kits without clobbering user-modified copies (first edit forks the kit to manual — user decision 2026-09-16).
- Uninstall <!-- replaced 2026-09-16 for 003a: portable uninstall = delete the app folder; `~/.overmind` data cleanup is manual/documented — "cleans app data only on explicit user consent" moves to 003b proper installers -->.
</requirements>

<non-goals>
- Auto-update of the app itself.
- Code signing / store distribution (separate ops concern).
- Linux packages.
</non-goals>

<refs>
- Rides `BACKLOG/002-kit-library/` — import pipeline and kit-source model (shipped 2026-09-16 as 002a).
- Open question from 002/02 hard review (NIT-3): `ClearSource()` vs bundled kits — resolved by spec D5 (typed refusal; edit-fork is the exit).
</refs>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
-->
