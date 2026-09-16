---
feature: 005
slug: tree-workspace
status: ready
version: 1
created: 2026-09-08
---

# Feature 005 — Tree workspace: user roots + active project context

# Feature 005 — Tree workspace: user roots + active project context

<goal>
The sidebar tree becomes the primary workspace. The user defines which disk roots the tree shows (instead of all drives) — roots are added via the folder picker, persisted, and removable with a confirmation. Clicking a project in the tree makes it the active project: the tree stays expanded, the project stays highlighted, and the Entries page becomes the entries view of the active project. Switching projects in the tree instantly switches the Entries context.
</goal>

<requirements>
- Tree roots are user-defined: multiple roots, added via the existing folder picker, persisted as a JSON array in AppSettings (`treeRoots`).
- Root removal requires confirmation; removal is FORBIDDEN (action disabled with a reason tooltip) when any registered project's DiskPath is the root or lives under it.
- Empty roots state: no drive enumeration fallback — the tree shows an "Add root folder" affordance instead.
- Clicking a project node navigates to `/entries?project=<id>` (Entries = active project's entries); the node is highlighted as active.
- Tree expansion state and the open/closed state of the Projects section survive MPA page reloads (sessionStorage, tab-scoped).
- Clicking the Projects nav label still opens the full projects list at `/`.
- The Entries nav item deep-links to the last active project when one exists in the session.
</requirements>

<non-goals>
- Cross-tab or cross-session persistence of tree state (sessionStorage only).
- Root reordering, nested root aliases, file watching.
- Changing the Entries page's own behavior (it already supports `?project=<id>`).
</non-goals>

<!--
LIFECYCLE: создаётся при постановке фичи в бэклог ДО старта работы.
Не замораживается гейтом, но изменения после status: ready проходят через пользователя (bump version).
Молчаливые правки запрещены.
-->
