---
feature: 002
slug: kit-library
status: draft
version: 1
created: 2026-09-06
---

# Feature 002 — Kit library

<goal>
Users can grow their kit collection from outside the app: import a kit from a local folder, or link a kit to a git repository. Repo-linked kits support private sources — the user authenticates (stored credentials/token) and needs access to the repo. Imported kits become first-class kits: versioned, linkable to projects, materializable exactly like hand-made ones.
</goal>

<requirements>
- A user can import a kit from a local folder: the folder is scanned for skill structures (SKILL.md trees), parsed into a kit with per-platform variants where detectable.
- A user can add a kit by git repository URL; the app clones/pulls it into managed storage.
- Private repos work: credentials (PAT/SSH/what-the-platform-uses) are entered by the user, stored securely (OS keychain/credential manager, not plaintext in SQLite), and used for fetch.
- A linked kit can be refreshed (pull) to pick up upstream changes; version/change visibility for the user.
- Import validation: broken/incomplete skill structures are reported, not silently swallowed.
- Library view in UI: all kits with origin (local/imported/repo), refresh action for repo kits.
</requirements>

<non-goals>
- Public marketplace / kit sharing hub.
- Auto-update of repo kits (manual refresh only in this feature).
- Conflict resolution UI beyond "report and refuse".
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
