---
feature: 127
task: 01
slug: project-tree-icons
status: done
review: light
created: 2026-10-06
brief: Swap the project-node glyph FolderOpen→Brain and give the inactive icon text-primary-hover in web/src/features/projects/components/project-tree.tsx per plan 7827c7bc; keep folder nodes byte-identical; 4 string pins hold; lint/tsc/test/build green in web/.
---

# Task 127/01 — Project node: Brain glyph + purple inactive icon

<goal>
Project rows in the project tree read as projects at a glance: lucide `Brain` instead of `FolderOpen`, inactive icon in the dark-purple `--color-primary-hover`, active state and folder rows untouched — one file, visual layer only.
</goal>

<scope>
All edits in `web/src/features/projects/components/project-tree.tsx`:

1. **Import block** (~lines 4–13): add `Brain` to the lucide-react import, remove `FolderOpen` — it renders only on project nodes, so leaving it would be an unused-import lint break.
2. **Project node icon** (~line 290, `entry.projectId !== null` branch). Exactly:

   from `<FolderOpen size={14} aria-hidden className="shrink-0" />`

   to `<Brain size={14} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />`

   `cn` is already imported in the file. Inactive: icon gets `text-primary-hover` (#9333ea) and deliberately keeps it on hover — the parent link's `hover:text-foreground` no longer reaches the icon (purple = identifier, not state; 065). Active: no extra class, the glyph inherits `text-primary`; the `border-primary bg-primary/10 text-primary` treatment and `aria-current="page"` are not edited.
3. **Everything else in the file: zero edits.** Directory node (~line 317), chevrons, register `Plus`, `FolderPlus` buttons, all handlers and hooks stay as they are.

String pins (reviewer checks these hold):
- pin `Brain` present and `FolderOpen` absent from the lucide import block;
- pin the project-node icon line as `<Brain size={14} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />` (whitespace-insensitive);
- pin `<Folder size={14} aria-hidden className="shrink-0 opacity-70" />` unchanged;
- pin the active-branch class string `"border-primary bg-primary/10 text-primary"` unchanged.
</scope>

<anti-scope>
- No structural extract or refactor of `project-tree.tsx` (audit F5 is a separate follow-up).
- No tree behavior changes: expand/collapse, selection, session-state and data hooks untouched.
- No new dependencies — `Brain` comes from the installed lucide-react ^1.42.0.
- No changes to directory nodes or any other icon surface (`FolderPlus`, `RefreshCw`, `X`, `Plus`, chevrons).
- No label color edits in either state — inactive label stays `text-muted` via the parent, active stays `text-primary`.
- No active-state brightness bump — that is human-gated at result validation, not part of this task.
</anti-scope>

<constraints>
- Colors from `@theme` tokens only (`text-primary-hover`, inherited `text-primary`); no hardcoded hex.
- Glyph footprint matches its neighbors: `size={14}`, `aria-hidden`, `shrink-0`.
- Conditional classes via the existing `cn` helper.
- Glyph and shade decisions (Brain, #9333ea) were validated by the human — do not re-litigate, do not substitute sibling glyphs.
</constraints>

<acceptance-criteria>
- [x] Project rows render the `Brain` glyph; folder rows still render `Folder` — distinguishable by icon alone.
- [x] Inactive project node: icon `text-primary-hover`, purple persists on hover; label `text-muted`, brightens on hover as today.
- [x] Active project node: `border-primary bg-primary/10 text-primary` + `aria-current="page"` exactly as before, `Brain` inheriting `text-primary`.
- [x] Directory node line and all other icon surfaces byte-identical to before (string pins hold).
- [x] No unused imports; `npm run lint`, `npx tsc --noEmit`, `npm run test`, `npm run build` all green in `web/`.
</acceptance-criteria>

<refs>
- Spec 102d1ef6 (validated; glyph + shade rationale in its obstacles).
- Plan 7827c7bc (validated; pins source).
- `web/src/features/projects/components/project-tree.tsx` — the only file.
- `web/src/app/globals.css` `@theme` — `--color-primary-hover: #9333ea`, `--color-primary: #a855f7`.
</refs>

<obstacles>
None at drafting — glyph and shade flags from the spec were resolved by validation (Brain + text-primary-hover). Patch here if the tree file drifted from the pinned lines.
</obstacles>

<log>
- 2026-10-06 [agent] — task drafted from validated plan 7827c7bc (single task, review light)
- 2026-10-06 [agent] — 127 coding round started
- 2026-10-06 [agent] — coding round done — file matched all 4 plan pins, no drift; diff = import swap (FolderOpen→Brain) + one icon line with cn("shrink-0", !active && "text-primary-hover"); gates green in web/ (lint 0 errors/6 pre-existing img warnings, tsc clean, 263 tests pass, next build ok); commit e5cab94 on main, not pushed
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, zero findings. 4/4 string pins, 5/5 AC verified in code, active branch byte-untouched, token check clean (primary-hover #9333ea in @theme), spot-check 263/263. Commit e5cab94.
</log>