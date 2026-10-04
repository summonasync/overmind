---
feature: 107
slug: project-tab-title
status: inProgress
priority: p1
version: 1
created: 2026-10-04
---

# Feature 107 — Tab title shows the open project («{Project} — Overmind»)

<goal>
Browser tab shows which project is open: «{Project name} — Overmind» instead of the static «Overmind», resolved from the project URL parameter (the name resolver from the 101 wave already exists). Awaiting counter (087/093) keeps working on top.
</goal>

<context>
User request 2026-10-04 (p1 intake). Today document.title is static «Overmind» in layout.page.tsx; use-awaiting-title.ts captures the base ONCE on mount (microbatch #134 flagged exactly this as a latent constraint — per-page/dynamic titles make the base stale). This feature ACTIVATES that constraint: the base becomes reactive, the awaiting hook must compose with it.
</context>

<open-questions>
None blocking: single title owner (one hook computing title = f(projectName, awaitingCount)) is the obvious design; challenge on grooming.
</open-questions>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
</log>
