---
feature: 127
slug: project-tree-icons
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 127 — Project tree: purple project icons distinct from folder nodes

<goal>
One file, one task: swap the project node glyph to lucide `Brain` and set its inactive/active icon colors in `project-tree.tsx`, folders untouched.
</goal>

<approach>
Everything happens in `web/src/features/projects/components/project-tree.tsx`.

1. **Import block** (~lines 4–13): add `Brain`, remove `FolderOpen` — it renders only on project nodes today, so after the swap it would be an unused-import lint break.
2. **Project node icon** (~line 290, inside the `entry.projectId !== null` branch). Today:

```tsx
<FolderOpen size={14} aria-hidden className="shrink-0" />
```

becomes:

```tsx
<Brain size={14} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />
```

`cn` is already imported in the file. Inactive: icon takes `text-primary-hover` (#9333ea) and deliberately keeps it on hover (parent's `hover:text-foreground` no longer reaches the icon — identifier, not state). Active (`active === true`): no extra class, glyph inherits `text-primary` from the link — the `border-primary bg-primary/10 text-primary` treatment and `aria-current="page"` are not touched.
3. **Directory node** (~line 317) and every other icon surface: zero edits.

Pins (string-pins for coder + reviewer):
- pin `import { Brain }` present, `FolderOpen` absent from the lucide import block;
- pin the project-node icon line exactly: `<Brain size={14} aria-hidden className={cn("shrink-0", !active && "text-primary-hover")} />` (whitespace-insensitive);
- pin `<Folder size={14} aria-hidden className="shrink-0 opacity-70" />` unchanged;
- pin the active-branch class string `"border-primary bg-primary/10 text-primary"` unchanged.
</approach>

<sequence>
- 01 (web coder): the three moves above in `project-tree.tsx`; gates below; report changed files. Review: light. Result after.
</sequence>

<verification>
- In `web/`: `npm run lint`, `npx tsc --noEmit`, `npm run test`, `npm run build` — all green.
- Static/visual sanity: tree with at least one registered project among folders — project rows show the `Brain` glyph in dark purple (label stays muted), active project row shows today's purple treatment with the same glyph, folder rows are pixel-identical to before. Human eyeballs at result validation.
</verification>

<refs>
- Spec 102d1ef6 (glyph + shade decisions and human flags live in its obstacles).
- `web/src/features/projects/components/project-tree.tsx` — the only file.
- `web/src/app/globals.css` `@theme` — `--color-primary-hover`, `--color-primary`.
</refs>

<log>
- 2026-10-06 [agent] — plan draft (1 task, string-pins on the tree file)
- 2026-10-06 [agent] — validated with human proof: notification c0de16d0-33a7-48ad-aa03-0c02d3767f26 replied 2026-10-06 18:53:16 UTC
</log>