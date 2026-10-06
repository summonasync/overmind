---
feature: 127
slug: project-tree-icons
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 127 — Project tree: purple project icons distinct from folder nodes

## Shipped

Project nodes read as projects at a glance. The lucide `Brain` glyph replaces `FolderOpen` (which rendered only on project nodes) in `web/src/features/projects/components/project-tree.tsx`; no new dependencies. Inactive project node: icon in dark purple via the `--color-primary-hover` token (#9333ea), purple persists on hover (identifier, not state — 065); label untouched (`text-muted`, brightens on hover as before). Active project node: treatment byte-unchanged (`border-primary bg-primary/10 text-primary` + `aria-current`), glyph inherits `text-primary`. Folder nodes and every other icon surface untouched.

## Verification

- Commit `e5cab94`; gates green in `web/`: lint, tsc, 263 tests, build.
- Light review APPROVED, 0 findings; string pins 4/4 (import swap, icon line, folder line, active class).

## For the human

- Browser eyeball of the tree: Brain readability at 14px and whether the inactive shade reads as the intended dark/muted purple.
- The optional active-state brightness bump («можно даже поярче») remains an open invitation — not shipped, one-line change if approved.

<log>
- 2026-10-06 [agent] — result draft (fast lane): single task done, e5cab94, light review 0 findings, pins 4/4
</log>