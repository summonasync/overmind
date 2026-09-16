---
feature: 004
slug: projects-tree-view
status: done
version: 2
created: 2026-09-08
---

# Feature 004 — Projects tree view

# Feature 004 — Projects tree view

<goal>
The Projects section of the web UI mirrors the real disk. The sidebar's Projects item expands into a lazy-loaded folder tree of the machine: folders that are not Overmind projects render as plain inactive nodes (expandable, not clickable), folders whose path matches a registered project's DiskPath are highlighted and clickable. Any folder can be registered as a project in place — this is the sync flow between "what's on disk" and "what Overmind manages".
</goal>

<requirements>
- Sidebar Projects expands into a folder tree rooted at machine drives (Windows) / home + filesystem root (macOS).
- Tree loads lazily per expanded directory — no pre-scan of the disk.
- Non-project folders are inactive nodes: expandable, visually muted, not clickable as projects.
- Project folders are highlighted and clickable; clicking navigates to that project in the UI.
- A tree node offers "Register as project" — opens the existing create-project form prefilled with that disk path.
- Backend provides a disk-enumeration endpoint (directories only), bound to the existing loopback-only API.
</requirements>

<non-goals>
- Files in the tree (directories only).
- Filesystem watching / live refresh of the tree.
- Bulk registration, drag-and-drop, moving projects on disk.
- Changing how projects are stored (no DB schema changes).
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
Вход для spec gate: на его основе строится spec.md через superpowers:brainstorming.
-->
